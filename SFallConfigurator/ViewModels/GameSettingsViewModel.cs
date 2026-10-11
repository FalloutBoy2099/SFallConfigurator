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
		private readonly List<ConfigOption> _options = [];

		private String? _selectedExecutable;
		private Boolean _isLoading;

		public GameSettingsViewModel(OptionContext context, Action<String> openFile)
		{
			_context = context;
			String gamePath = context.Configuration.GamePath;
			OptionLocation system = context.GameCfg.In("system");
			OptionLocation sound = context.GameCfg.In("sound");

			PathOption masterDat = CreatePathOption(system, "master_dat", "master_dat:", PathKind.FileOrFolder, gamePath, "Path to the file or folder of Master.dat");
			PathOption masterPatches = CreatePathOption(system, "master_patches", "master_patches:", PathKind.FileOrFolder, gamePath, "Path to the Master patch folder.");
			PathOption critterDat = CreatePathOption(system, "critter_dat", "critter_dat:", PathKind.FileOrFolder, gamePath, "Path to the file or folder of Critter.dat");
			PathOption critterPatches = CreatePathOption(system, "critter_patches", "critter_patches:", PathKind.FileOrFolder, gamePath, "Path to the Critter patch folder.");
			PathOption musicPath1 = CreatePathOption(sound, "music_path1", "music_path1:", PathKind.Folder, gamePath, "Path to the music folder.");
			PathOption musicPath2 = CreatePathOption(sound, "music_path2", "music_path2:", PathKind.Folder, gamePath, "Path to the music folder.");

			GameConfigGroups =
			[
				new OptionGroup("Game Config", masterDat, masterPatches, critterDat, critterPatches, musicPath1, musicPath2),
			];

			Language = new TextOption(system, "language", "Game Language:") { ToolTip = "Language of the game.", Suggestions = context.Configuration.Languages };
			_options.Add(Language);

			ConfigFile = new FileDisplayItem("Game Configuration File:", new RelayCommand(ChooseGameConfig)) { ToolTip = "Choose the game configuration file." };
			OpenGameConfigCommand = new RelayCommand(() => openFile(context.Configuration.GameCfgFileName), () => File.Exists(context.Configuration.GameCfg.Path));
		}

		/// <summary>
		/// Raised when the user picks another game executable.
		/// </summary>
		public event EventHandler? ExecutableChanged;

		public ObservableCollection<String> Executables { get; } = [];

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

			foreach (ConfigOption option in _options)
			{
				option.Load();
			}

			foreach (OptionGroup group in GameConfigGroups)
			{
				foreach (ConfigOption option in group.Items.OfType<ConfigOption>())
				{
					option.Load();
				}
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

		private PathOption CreatePathOption(OptionLocation location, String key, String label, PathKind kind, String gamePath, String toolTip)
		{
			PathOption option = new(location, key, label, kind, gamePath, _context.Interaction)
			{
				ToolTip = toolTip,
				FileFilter = "Dat files|*.dat",
				TrailingSeparator = key.StartsWith("music_path", StringComparison.Ordinal),
			};

			// The options are loaded with the tab, once the configuration file is known.
			_options.Add(option);
			return option;
		}

		private void ChooseGameConfig()
		{
			String? path = _context.Interaction.PickFile("cfg files|*.cfg", _context.Configuration.GamePath);
			if (path is null)
			{
				return;
			}

			if (ErrorGuard.Run(_context.Interaction, () => _context.Configuration.SetGameCfgFile(path)))
			{
				Load();
			}
		}
	}
}