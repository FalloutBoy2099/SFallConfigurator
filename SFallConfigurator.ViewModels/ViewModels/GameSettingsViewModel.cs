namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Options;

	/// <summary>
	/// The "Settings" tab: the game executable and the paths of the game configuration (fallout2.cfg).
	/// </summary>
	internal sealed class GameSettingsViewModel : ObservableObject
	{
		private const Int64 MinimumExecutableSize = 1024;

		/// <summary>
		/// The game languages offered in the language list: the names of the language folders of the game.
		/// </summary>
		private static readonly String[] KnownLanguages =
		[
			"english",
			"russian",
			"french",
			"czech",
			"german",
			"hungarian",
			"italian",
			"polish",
			"portuguese",
			"spanish",
			"cht",
			"korean",
			"swedish",
		];

		private readonly OptionContext _context;

		private String? _selectedExecutable;
		private Boolean _isLoading;

		public GameSettingsViewModel(OptionContext context, Action<String> openFile)
		{
			_context = context;
			CatalogOptions catalog = new(OptionCatalog.Page("settings"), context);
			Options = catalog.All;

			GameConfigGroups = catalog.BuildGroups();

			Language = catalog.Get<TextOption>("language");
			Language.Suggestions = context.Configuration.Languages;

			ConfigFile = new FileDisplayItem("Game Configuration File:", new RelayCommand(ChooseGameConfig)) { ToolTip = "Choose the game configuration file." };
			OpenGameConfigCommand = new RelayCommand(() => openFile(context.Configuration.GameCfgFileName), () => context.Services.FileSystem.FileExists(context.Configuration.GameCfg.Path));
		}

		/// <summary>
		/// Raised when the user picks another game executable.
		/// </summary>
		public event EventHandler? ExecutableChanged;

		public ObservableCollection<String> Executables { get; } = [];

		/// <summary>
		/// Every option of the tab, the game language included.
		/// </summary>
		public IReadOnlyList<ConfigOption> Options { get; }

		public IReadOnlyList<OptionGroup> GameConfigGroups { get; }

		/// <summary>
		/// The language of the game (a folder name below the "text" folder of the patches).
		/// </summary>
		public TextOption Language { get; }

		public FileDisplayItem ConfigFile { get; }

		public ICommand OpenGameConfigCommand { get; }

		public String? SelectedExecutable
		{
			get => _selectedExecutable;
			set
			{
				if (!SetProperty(ref _selectedExecutable, value) || _isLoading || value is null)
				{
					return;
				}

				_context.Configuration.GameExe = value;
				ExecutableChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public void Load()
		{
			_isLoading = true;
			try
			{
				GameConfiguration configuration = _context.Configuration;

				Executables.Clear();
				Executables.Add(configuration.GameExe);
				foreach (String exe in FindExecutables(configuration))
				{
					Executables.Add(exe);
				}

				SelectedExecutable = configuration.GameExe;
				ConfigFile.Text = configuration.GameCfgFileName;
			}
			finally
			{
				_isLoading = false;
			}

			foreach (ConfigOption option in Options)
			{
				option.Load();
			}

			// The language list is fixed: the known languages, plus any other one already in use so that it stays selectable.
			Language.Suggestions = KnownLanguages
				.Concat(_context.Configuration.Languages)
				.Append(Language.Text)
				.Where(language => !String.IsNullOrWhiteSpace(language))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Order(StringComparer.CurrentCultureIgnoreCase)
				.ToList();
		}

		private static IEnumerable<String> FindExecutables(GameConfiguration configuration)
		{
			String? ownName = Path.GetFileName(Environment.ProcessPath);

			foreach (String path in Directory.GetFiles(configuration.GamePath, "*.exe", SearchOption.TopDirectoryOnly))
			{
				String name = Path.GetFileName(path);
				if (!name.Equals(configuration.GameExe, StringComparison.OrdinalIgnoreCase)
					&& !name.Equals(ownName, StringComparison.OrdinalIgnoreCase)
					&& new FileInfo(path).Length > MinimumExecutableSize)
				{
					yield return name;
				}
			}
		}

		private void ChooseGameConfig()
		{
			String? path = _context.Interaction.PickFile("cfg files|*.cfg", _context.Configuration.GamePath);
			if (path is null)
			{
				return;
			}

			if (_context.Configuration.HasPendingChangesInGameCfgFiles && !_context.Interaction.Confirm("The changes that were not saved will be lost. Continue?"))
			{
				return;
			}

			if (ErrorGuard.Run(_context.Interaction, () => _context.Configuration.SetGameCfgFile(path)))
			{
				_context.ReloadOptions();
			}
		}
	}
}