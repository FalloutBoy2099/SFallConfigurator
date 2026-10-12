namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.Json;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;

	/// <summary>
	/// Describes the options that the program builds, in the format of the catalog. It is how the catalog was first written
	/// from the pages, and how a test checks that the catalog and the options that are built stay the same.
	/// </summary>
	internal static class CatalogDumper
	{
		/// <summary>
		/// The settings of an option that the catalog may state.
		/// </summary>
		private static readonly String[] DeclarativeProperties =
		[
			"TrueValue", "FalseValue", "DefaultValue", "Presentation", "Values", "Increment", "MinimumLabel", "MaximumLabel",
			"HasToggle", "AllowModifiers", "FileFilter", "TrailingSeparator", "ActionLabel", "ActionToolTip", "SecondaryActionLabel", "SecondaryActionToolTip",
		];

		public static IReadOnlyList<CatalogPage> Dump(MainViewModel viewModel)
		{
			OptionContext context = new(viewModel.Configuration, new AppServices(new TestInteraction()));
			List<CatalogPage> pages = [];
			foreach (OptionPage page in viewModel.Sfall.Pages.Concat(viewModel.HiRes.Pages))
			{
				pages.Add(DumpPage(GetPageId(page), page.RawHeader, page.Groups, viewModel.Configuration, context));
			}

			return pages;
		}

		public static CatalogPage DumpPage(String id, String title, IReadOnlyList<OptionGroup> groups, GameConfiguration configuration, OptionContext context)
		{
			CatalogPage page = new() { Id = id, Title = title };
			Dictionary<String, Int32> used = new(StringComparer.Ordinal);
			foreach (OptionGroup group in groups)
			{
				CatalogGroup dumped = new() { Title = group.RawTitle };
				foreach (Object item in group.Items)
				{
					dumped.Items.Add(DumpItem(item, configuration, context, used));
				}

				page.Groups.Add(dumped);
			}

			return page;
		}

		public static String GetPageId(OptionPage page)
		{
			String name = page.GetType().Name;
			name = name.EndsWith("Page", StringComparison.Ordinal) ? name[..^4] : name;
			name = name.Replace("HiRes", "hires", StringComparison.Ordinal);
			return Char.ToLowerInvariant(name[0]) + name[1..];
		}

		private static CatalogItem DumpItem(Object item, GameConfiguration configuration, OptionContext context, Dictionary<String, Int32> used)
		{
			if (item is OptionNote note)
			{
				return new CatalogItem { Type = "note", Text = note.RawText };
			}

			if (item is not ConfigOption option)
			{
				return new CatalogItem { Type = "custom", Id = Unique(item.GetType().Name, used) };
			}

			CatalogItem dumped = new()
			{
				Type = GetType(option),
				Id = Unique(Char.ToLowerInvariant(option.Key[0]) + option.Key[1..], used),
				Source = GetSource(option, configuration),
				Section = option.Section,
				Key = option.Key,
				Label = option.RawLabel,
				ToolTip = option.RawToolTip,
			};

			switch (option)
			{
				case NumberOption number:
					dumped.Minimum = number.Minimum;
					dumped.Maximum = number.Maximum;
					break;
				case ChoiceOption choice when option is not WindowModeOption:
					dumped.Items = [.. choice.Items];
					break;
				case KeyOption key:
					dumped.DefaultCode = key.DefaultCode;
					break;
				case PathOption path:
					dumped.Kind = path.Kind.ToString();
					break;
			}

			dumped.Properties = DumpProperties(option, dumped, context);
			return dumped;
		}

		private static Dictionary<String, JsonElement>? DumpProperties(ConfigOption option, CatalogItem item, OptionContext context)
		{
			CatalogItem plain = new()
			{
				Type = item.Type,
				Id = item.Id,
				Source = item.Source,
				Section = item.Section,
				Key = item.Key,
				Label = item.Label,
				Minimum = item.Minimum,
				Maximum = item.Maximum,
				Items = item.Items,
				DefaultCode = item.DefaultCode,
				Kind = item.Kind,
			};
			ConfigOption baseline = CatalogOptions.Create(plain, context);
			Dictionary<String, JsonElement> properties = [];
			foreach (String name in DeclarativeProperties)
			{
				PropertyInfo? property = option.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
				if (property is null)
				{
					continue;
				}

				Object? value = property.GetValue(option);
				Object? original = property.GetValue(baseline);
				if (Same(value, original))
				{
					continue;
				}

				Object? plainValue = value is Enum ? value.ToString() : value is IReadOnlyList<String> list ? list.ToArray() : value;
				properties[Char.ToLowerInvariant(name[0]) + name[1..]] = JsonSerializer.SerializeToElement(plainValue);
			}

			return properties.Count == 0 ? null : properties;
		}

		private static Boolean Same(Object? left, Object? right)
		{
			if (left is IReadOnlyList<String> a && right is IReadOnlyList<String> b)
			{
				return a.SequenceEqual(b);
			}

			return Equals(left, right);
		}

		private static String GetType(ConfigOption option)
		{
			return option switch
			{
				BoolOption => "bool",
				NumberOption => "number",
				WindowModeOption => "windowMode",
				ChoiceOption => "choice",
				KeyOption => "key",
				PathOption => "path",
				TextOption => "text",
				BarArtOption => "barArt",
				_ => throw new InvalidOperationException("Unknown option " + option.GetType().Name),
			};
		}

		private static String GetSource(ConfigOption option, GameConfiguration configuration)
		{
			if (ReferenceEquals(option.Source, configuration.SfallSource))
			{
				return "sfall";
			}

			if (ReferenceEquals(option.Source, configuration.SfallModsSource))
			{
				return "sfallMods";
			}

			if (ReferenceEquals(option.Source, configuration.HiResSource))
			{
				return "hires";
			}

			if (ReferenceEquals(option.Source, configuration.GameCfgSource))
			{
				return "gameCfg";
			}

			return ReferenceEquals(option.Source, configuration.TranslationsSource) ? "translations" : "sfallModsAdd";
		}

		private static String Unique(String id, Dictionary<String, Int32> used)
		{
			used[id] = used.GetValueOrDefault(id) + 1;
			return used[id] == 1 ? id : id + used[id];
		}
	}
}