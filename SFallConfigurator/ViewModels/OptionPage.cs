namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;

	/// <summary>
	/// What the pages need to build their options: the configuration, the user interaction
	/// and the files the options are stored in.
	/// </summary>
	internal sealed class OptionContext
	{
		public OptionContext(GameConfiguration configuration, IUserInteraction interaction)
		{
			Configuration = configuration;
			Interaction = interaction;

			Sfall = new OptionLocation(configuration.SfallSource);
			SfallMods = new OptionLocation(configuration.SfallModsSource);
			HiRes = new OptionLocation(configuration.HiResSource);
			GameCfg = new OptionLocation(configuration.GameCfgSource);
			Translations = new OptionLocation(configuration.TranslationsSource);
		}

		public GameConfiguration Configuration { get; }

		public IUserInteraction Interaction { get; }

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

		public String Header => Localizer.T(_header);

		public override String ToString()
		{
			return Header;
		}

		public IReadOnlyList<OptionGroup> Groups { get; protected set; } = [];

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