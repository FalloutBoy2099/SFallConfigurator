namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;
	using Services;

	/// <summary>
	/// What the pages need to build their options: the configuration, the user interaction
	/// and the files the options are stored in.
	/// </summary>
	internal sealed class OptionContext
	{
		public OptionContext(GameConfiguration configuration, AppServices services)
		{
			Configuration = configuration;
			Services = services;

			_palette = new Lazy<IReadOnlyList<Int32>?>(() => GamePalette.Load(configuration));
			Sfall = new OptionLocation(configuration.SfallSource);
			SfallMods = new OptionLocation(configuration.SfallModsSource);
			HiRes = new OptionLocation(configuration.HiResSource);
			GameCfg = new OptionLocation(configuration.GameCfgSource);
			Translations = new OptionLocation(configuration.TranslationsSource);
		}

		private readonly Lazy<IReadOnlyList<Int32>?> _palette;

		public GameConfiguration Configuration { get; }

		/// <summary>
		/// The services of the program: the user, the disk, the other programs, the executable and the display.
		/// </summary>
		public AppServices Services { get; }

		public IUserInteraction Interaction => Services.Interaction;

		/// <summary>
		/// The sfall options (custom sfall ini and ddraw.ini).
		/// </summary>
		public OptionLocation Sfall { get; }

		/// <summary>
		/// sfall-mods.ini
		/// </summary>
		public OptionLocation SfallMods { get; }

		/// <summary>
		/// f2_res.ini
		/// </summary>
		public OptionLocation HiRes { get; }

		/// <summary>
		/// The game's own configuration file (fallout2.cfg).
		/// </summary>
		public OptionLocation GameCfg { get; }

		public OptionLocation Translations { get; }

		/// <summary>
		/// Reads every option again, after a file was merged or replaced. Set by the main view model.
		/// </summary>
		public Action ReloadOptions { get; set; } = () => { };

		/// <summary>
		/// The colors of the game, as 0xRRGGBB; null when the game has no palette. Read once, when first needed.
		/// </summary>
		public IReadOnlyList<Int32>? Palette => _palette.Value;

		/// <summary>
		/// sfall-mods.ini, where a key that is missing is added when a value is written.
		/// </summary>
		public OptionLocation SfallModsAdd => new(new DocumentSource(() => Configuration.SfallMods, IniWriteMode.AddIfMissing));
	}

	/// <summary>
	/// A tab of options, made of titled groups.
	/// </summary>
	internal abstract class OptionPage : ObservableObject
	{
		private readonly List<ConfigOption> _options = [];

		private readonly String _header;

		protected OptionPage(String header, OptionContext context)
		{
			_header = header;
			Context = context;
		}

		/// <summary>
		/// A page that is described by the catalog: its options are built from it, and the page adds their behaviour.
		/// </summary>
		protected OptionPage(CatalogPage page, OptionContext context)
			: this(page.Title, context)
		{
			Catalog = new CatalogOptions(page, context);
			_options.AddRange(Catalog.All);
		}

		/// <summary>
		/// The options of the catalog page. Null for a page that is not described by the catalog.
		/// </summary>
		protected CatalogOptions Catalog { get; } = null!;

		/// <summary>
		/// The title as it is written in the sources and in the catalog: the key of the translations.
		/// </summary>
		internal String RawHeader => _header;

		public String Header => Localizer.T(_header);

		public override String ToString()
		{
			return Header;
		}

		public IReadOnlyList<OptionGroup> Groups { get; protected set; } = [];

		/// <summary>
		/// Every option of the page that is read and saved with it, hidden ones included.
		/// </summary>
		public IReadOnlyList<ConfigOption> Options => _options;

		protected OptionContext Context { get; }

		/// <summary>
		/// True while the options are read; reactions to user changes must not run then.
		/// </summary>
		protected Boolean IsPageLoading { get; private set; }

		/// <summary>
		/// Reads every option of the page from the configuration.
		/// </summary>
		public virtual void Load()
		{
			IsPageLoading = true;
			try
			{
				foreach (ConfigOption option in _options)
				{
					option.Load();
				}
			}
			finally
			{
				IsPageLoading = false;
			}
		}

		/// <summary>
		/// Registers an option so that <see cref="Load"/> reads it.
		/// </summary>
		protected T Add<T>(T option)
			where T : ConfigOption
		{
			_options.Add(option);
			return option;
		}

		protected BoolOption Bool(OptionLocation location, String key, String label, String? toolTip = null)
		{
			return Add(new BoolOption(location, key, label) { ToolTip = toolTip });
		}

		protected NumberOption Number(OptionLocation location, String key, String label, Int32 minimum, Int32 maximum, String? toolTip = null, Int32 increment = 1)
		{
			return Add(new NumberOption(location, key, label, minimum, maximum) { ToolTip = toolTip, Increment = increment });
		}

		protected ChoiceOption Choice(OptionLocation location, String key, String label, String[] items, String? toolTip = null)
		{
			return Add(new ChoiceOption(location, key, label, items) { ToolTip = toolTip });
		}

		protected KeyOption Key(OptionLocation location, String key, String label, Int32 defaultCode, String? toolTip = null)
		{
			return Add(new KeyOption(location, key, label, defaultCode, Context.Interaction) { ToolTip = toolTip });
		}
	}
}