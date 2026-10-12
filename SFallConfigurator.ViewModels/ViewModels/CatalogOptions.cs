namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.Json;
	using Options;

	/// <summary>
	/// The options of one page of the catalog, built from its data. The page asks for each option by its id, adds what
	/// data cannot say (what to do when it changes, where else to read it), and then builds its groups.
	/// </summary>
	internal sealed class CatalogOptions
	{
		private readonly CatalogPage _page;
		private readonly Dictionary<String, ConfigOption> _options = new(StringComparer.Ordinal);

		public CatalogOptions(CatalogPage page, OptionContext context)
		{
			_page = page;
			foreach (CatalogItem item in page.Groups.SelectMany(group => group.Items).Where(item => item.Type is not ("note" or "custom" or "column")))
			{
				ConfigOption option = Create(item, context);
				option.Id = item.Id ?? throw new InvalidOperationException("An option of the page " + page.Id + " has no id: " + item.Key);
				_options.Add(option.Id, option);
			}
		}

		/// <summary>
		/// Every option of the page, in the order of the catalog.
		/// </summary>
		public IReadOnlyList<ConfigOption> All => _page.Groups
			.SelectMany(group => group.Items)
			.Where(item => item.Id is not null && _options.ContainsKey(item.Id))
			.Select(item => _options[item.Id!])
			.ToList();

		public T Get<T>(String id)
			where T : ConfigOption
		{
			return _options.TryGetValue(id, out ConfigOption? option) && option is T typed
				? typed
				: throw new InvalidOperationException("The page " + _page.Id + " of the catalog has no option " + id + " of the type " + typeof(T).Name + ".");
		}

		/// <summary>
		/// The groups of the page. The items that the catalog calls "custom" are the ones that the page gives by id.
		/// </summary>
		public IReadOnlyList<OptionGroup> BuildGroups(IReadOnlyDictionary<String, Object>? custom = null)
		{
			return _page.Groups.Select(group => new OptionGroup(group.Title, group.Items.Where(item => !item.Hidden).Select(item => GetItem(item, custom)).ToArray())).ToList();
		}

		private Object GetItem(CatalogItem item, IReadOnlyDictionary<String, Object>? custom)
		{
			return item.Type switch
			{
				"note" => new OptionNote(item.Text ?? String.Empty),
				"column" => new OptionColumn((item.Members ?? []).Select(id => _options[id]).ToList()),
				"custom" => custom is not null && item.Id is not null && custom.TryGetValue(item.Id, out Object? value)
					? value
					: throw new InvalidOperationException("The page " + _page.Id + " gives no item " + item.Id + "."),
				_ => _options[item.Id!],
			};
		}

		private static OptionLocation GetLocation(CatalogItem item, OptionContext context)
		{
			OptionLocation location = item.Source switch
			{
				"sfall" => context.Sfall,
				"sfallMods" => context.SfallMods,
				"sfallModsAdd" => context.SfallModsAdd,
				"hires" => context.HiRes,
				"gameCfg" => context.GameCfg,
				"translations" => context.Translations,
				_ => throw new InvalidOperationException("Unknown file " + item.Source + " for the option " + item.Key + "."),
			};
			return item.Section is null ? location : location.In(item.Section);
		}

		internal static ConfigOption Create(CatalogItem item, OptionContext context)
		{
			OptionLocation location = GetLocation(item, context);
			String key = item.Key ?? String.Empty;
			String label = item.Label ?? String.Empty;
			ConfigOption option = item.Type switch
			{
				"bool" => new BoolOption(location, key, label),
				"number" => new NumberOption(location, key, label, item.Minimum ?? 0, item.Maximum ?? 0),
				"choice" => new ChoiceOption(location, key, label, item.Items ?? []),
				"windowMode" => new WindowModeOption(location, label),
				"barArt" => new BarArtOption(location, key, label),
				"key" => new KeyOption(location, key, label, item.DefaultCode ?? 0, context.Interaction),
				"text" => new TextOption(location, key, label),
				"path" => new PathOption(location, key, label, Enum.Parse<PathKind>(item.Kind ?? nameof(PathKind.File), ignoreCase: true), context.Configuration.GamePath, context.Interaction),
				_ => throw new InvalidOperationException("Unknown type " + item.Type + " for the option " + item.Key + "."),
			};

			option.ToolTip = item.ToolTip;
			if (item.Properties is not null)
			{
				foreach ((String name, JsonElement value) in item.Properties)
				{
					SetProperty(option, name, value);
				}
			}

			option.AddToSection = item.AddToSection;
			option.FileName = GetFileName(item, context);
			option.Origin = item.Source == "sfallModsAdd" ? "sfallMods" : item.Source;
			option.KnownDefault = item.Default ?? FindMarkedDefault(option);
			return option;
		}

		private static String? GetFileName(CatalogItem item, OptionContext context)
		{
			return item.Source switch
			{
				"sfall" => context.Configuration.SfallFileName ?? "ddraw.ini",
				"sfallMods" or "sfallModsAdd" => "sfall-mods.ini",
				"hires" => "f2_res.ini",
				"gameCfg" => "fallout2.cfg",
				"translations" => "translations.ini",
				_ => null,
			};
		}

		/// <summary>
		/// The raw value of the item of a choice that says "(default)", when there is one.
		/// </summary>
		private static String? FindMarkedDefault(ConfigOption option)
		{
			if (option is not ChoiceOption choice)
			{
				return null;
			}

			Int32 index = choice.Items.ToList().FindIndex(text => text.Contains("(default)", StringComparison.OrdinalIgnoreCase) || text.Contains("(par défaut)", StringComparison.OrdinalIgnoreCase));
			return index < 0 ? null : choice.Values is not null && index < choice.Values.Count ? choice.Values[index] : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		private static void SetProperty(ConfigOption option, String name, JsonElement value)
		{
			PropertyInfo property = option.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
				?? throw new InvalidOperationException("The option " + option.Key + " has no property " + name + ".");
			property.SetValue(option, Convert(value, property.PropertyType));
		}

		private static Object? Convert(JsonElement value, Type type)
		{
			if (value.ValueKind == JsonValueKind.Null)
			{
				return null;
			}

			Type target = Nullable.GetUnderlyingType(type) ?? type;
			if (target.IsEnum)
			{
				return Enum.Parse(target, value.GetString()!, ignoreCase: true);
			}

			if (target == typeof(String))
			{
				return value.GetString();
			}

			if (target == typeof(Int32))
			{
				return value.GetInt32();
			}

			if (target == typeof(Boolean))
			{
				return value.GetBoolean();
			}

			if (target == typeof(IReadOnlyList<String>) || target == typeof(String[]))
			{
				return value.EnumerateArray().Select(element => element.GetString()!).ToArray();
			}

			throw new InvalidOperationException("A property of the type " + type.Name + " cannot be set from the catalog.");
		}
	}
}