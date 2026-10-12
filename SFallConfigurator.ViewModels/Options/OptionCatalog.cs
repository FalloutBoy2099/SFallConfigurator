namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>
	/// One entry of the catalog: an option of a configuration file, a paragraph of explanation, or the place of an item
	/// that the page builds itself (an editor, a button).
	/// </summary>
	internal sealed class CatalogItem
	{
		/// <summary>
		/// bool, number, choice, key, text, path, windowMode, barArt, note, column or custom.
		/// </summary>
		public String Type { get; set; } = String.Empty;

		/// <summary>
		/// The name of the item in its page. The page asks for an option by it.
		/// </summary>
		public String? Id { get; set; }

		/// <summary>
		/// The file: sfall, sfallMods, sfallModsAdd (the same file, where a missing key is added), hires, gameCfg or translations.
		/// </summary>
		public String? Source { get; set; }

		public String? Section { get; set; }

		/// <summary>
		/// Where the key is added when the file lacks it, when the option has no section of its own (see the option).
		/// </summary>
		public String? AddToSection { get; set; }

		public String? Key { get; set; }

		public String? Label { get; set; }

		public String? ToolTip { get; set; }

		/// <summary>
		/// The text of a note.
		/// </summary>
		public String? Text { get; set; }

		public Int32? Minimum { get; set; }

		public Int32? Maximum { get; set; }

		/// <summary>
		/// The items of a choice.
		/// </summary>
		public List<String>? Items { get; set; }

		/// <summary>
		/// The key that a key option shows when the file has none.
		/// </summary>
		public Int32? DefaultCode { get; set; }

		/// <summary>
		/// The raw value that the program uses when the key is not set, when it is known. A choice whose item says "(default)"
		/// needs none.
		/// </summary>
		public String? Default { get; set; }

		/// <summary>
		/// What a path option accepts: File, Folder or FileOrFolder.
		/// </summary>
		public String? Kind { get; set; }

		/// <summary>
		/// The ids of the options that a column shows, one under the other. They are hidden items of the page.
		/// </summary>
		public List<String>? Members { get; set; }

		/// <summary>
		/// The other settings of the option, by the name of the property that they set.
		/// </summary>
		public Dictionary<String, JsonElement>? Properties { get; set; }

		/// <summary>
		/// True for an option that a custom item of the page shows (the boxes of a resolution editor): the page builds it,
		/// loads it and saves it, but its group does not list it.
		/// </summary>
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public Boolean Hidden { get; set; }
	}

	/// <summary>
	/// A titled card of items.
	/// </summary>
	internal sealed class CatalogGroup
	{
		public String? Title { get; set; }

		public List<CatalogItem> Items { get; set; } = [];
	}

	/// <summary>
	/// A tab of options: its title, and its groups in the order they are shown.
	/// </summary>
	internal sealed class CatalogPage
	{
		public String Id { get; set; } = String.Empty;

		public String Title { get; set; } = String.Empty;

		public List<CatalogGroup> Groups { get; set; } = [];
	}

	/// <summary>
	/// The options of the program, as data: what they are called, where they are stored, what they accept and how they are
	/// grouped. The pages build their options from it and add the behaviour that data cannot say.
	/// <para>The files are the embedded resources Options/Catalog/*.json.</para>
	/// </summary>
	internal static class OptionCatalog
	{
		private const String ResourcePrefix = "SFallConfigurator.Options.Catalog.";

		private static readonly Lazy<IReadOnlyDictionary<String, CatalogPage>> Loaded = new(Load);

		public static JsonSerializerOptions JsonOptions { get; } = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};

		/// <summary>
		/// Every page of the catalog, by its id.
		/// </summary>
		public static IReadOnlyDictionary<String, CatalogPage> Pages => Loaded.Value;

		public static CatalogPage Page(String id)
		{
			return Pages.TryGetValue(id, out CatalogPage? page) ? page : throw new InvalidOperationException("The option catalog has no page " + id + ".");
		}

		private static IReadOnlyDictionary<String, CatalogPage> Load()
		{
			Assembly assembly = typeof(OptionCatalog).Assembly;
			Dictionary<String, CatalogPage> pages = [];
			foreach (String name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)))
			{
				using Stream stream = assembly.GetManifestResourceStream(name)!;
				CatalogPage page = JsonSerializer.Deserialize<CatalogPage>(stream, JsonOptions)!;
				pages[page.Id] = page;
			}

			return pages;
		}
	}
}