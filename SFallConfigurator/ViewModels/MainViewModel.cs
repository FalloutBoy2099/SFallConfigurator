namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;

	/// <summary>
	/// The main window: the tabs, and the buttons that save the configuration and start the game.
	/// </summary>
	internal sealed class MainViewModel : ObservableObject
	{
		private readonly GameConfiguration _configuration;
		private readonly IUserInteraction _interaction;
		private readonly Action _closeWindow;
		private readonly Action<String> _changeGameFolder;
		private readonly Action<String> _changeLanguage;
		private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

		public MainViewModel(GameConfiguration configuration, IUserInteraction interaction, Action closeWindow, Action<String> changeGameFolder, Action<String> changeLanguage)
		{
			_configuration = configuration;
			_interaction = interaction;
			_closeWindow = closeWindow;
			_changeGameFolder = changeGameFolder;
			_changeLanguage = changeLanguage;

			RecentGameFolders = GameLocator.GetRecentFolders()
				.Where(folder => !String.Equals(folder, configuration.GamePath, StringComparison.OrdinalIgnoreCase))
				.Prepend(configuration.GamePath)
				.ToList();

			Title = "SFallConfigurator v" + GetShortVersion() + " - by Kain";

			RunGameCommand = new RelayCommand(RunGame, () => CanRunGame);
			DoneCommand = new RelayCommand(Done);
			ChooseGameFolderCommand = new RelayCommand(ChooseGameFolder);
			ChangeLanguageCommand = new RelayCommand(code => _changeLanguage((String)code!));

			OptionContext context = new OptionContext(configuration, interaction);
			HiRes = new HiResViewModel(context, OpenInEditor);
			Sfall = new SfallViewModel(context, HiRes, OpenInEditor);
			Mods = new ModsViewModel(context, OpenInEditor);
			Settings = new GameSettingsViewModel(context, OpenInEditor);
			DatInfo = new DatInfoViewModel(configuration.GamePath, interaction);
			Language = Settings.Language;

			Settings.ExecutableChanged += (_, _) => OnExecutableChanged();
		}

		public GameConfiguration Configuration => _configuration;

		/// <summary>
		/// The game folders used before (the most recent ones first), with the current one.
		/// </summary>
		public IReadOnlyList<String> RecentGameFolders { get; }

		public String Title { get; }

		public SfallViewModel Sfall { get; }

		public HiResViewModel HiRes { get; }

		public ModsViewModel Mods { get; }

		public GameSettingsViewModel Settings { get; }

		public DatInfoViewModel DatInfo { get; }

		public TextOption Language { get; }

		public ICommand RunGameCommand { get; }

		public ICommand DoneCommand { get; }

		public ICommand ChooseGameFolderCommand { get; }

		/// <summary>
		/// Switches the language of the interface. The parameter is the code of the language.
		/// </summary>
		public ICommand ChangeLanguageCommand { get; }

		/// <summary>
		/// The game folder: it holds the game, ddraw.ini and the other configuration files.
		/// </summary>
		public String GamePath => _configuration.GamePath;

		/// <summary>
		/// The game folder shown in the list of recent folders. Choosing another one opens it.
		/// </summary>
		public String SelectedGameFolder
		{
			get => _configuration.GamePath;
			set
			{
				if (!String.Equals(value, _configuration.GamePath, StringComparison.OrdinalIgnoreCase))
				{
					_changeGameFolder(value);
				}

				// When the change was refused the list must show the current folder again.
				_uiContext?.Post(_ => OnPropertyChanged(nameof(SelectedGameFolder)), null);
			}
		}

		/// <summary>
		/// False when ddraw.ini was not found: only the DAT archives can be browsed then.
		/// </summary>
		public Boolean IsGameConfigured => _configuration.DdrawExists;

		/// <summary>
		/// The game can be started: the selected executable exists.
		/// </summary>
		public Boolean CanRunGame => IsGameConfigured && File.Exists(_configuration.GameExePath);

		/// <summary>
		/// The tooltip of the "Run Game" button.
		/// </summary>
		public String RunGameToolTip => Localizer.F("Save the settings and start the game ({0}).", _configuration.GameExe);

		/// <summary>
		/// Reads the configuration files and fills every tab.
		/// </summary>
		public void Load()
		{
			if (IsGameConfigured)
			{
				Settings.Load();
				Mods.Load();

				// The Hi-Res patch is read before sfall: the sfall resolution editor compares with it.
				HiRes.Load();
				Sfall.Load();
			}

			OnPropertyChanged(nameof(IsGameConfigured));
			OnPropertyChanged(nameof(CanRunGame));
		}

		/// <summary>
		/// Tells the user when the game folder has no ddraw.ini, which leaves only the archives to browse.
		/// </summary>
		public void ReportMissingConfiguration()
		{
			if (!IsGameConfigured)
			{
				_interaction.ShowMessage("ddraw.ini was not found.", Title);
			}
		}

		/// <summary>
		/// Called when the window is closing. Returns false when the user cancelled.
		/// </summary>
		public Boolean ConfirmClose()
		{
			if (!_configuration.HasPendingChanges && !Mods.IsModified)
			{
				return true;
			}

			Boolean? save = _interaction.AskSaveChanges("Save the changes before closing?", Title);
			if (save is null)
			{
				return false;
			}

			// A failed save keeps the window open: closing now would lose the changes.
			return !save.Value || SaveAll();
		}

		private void RunGame()
		{
			if (!ErrorGuard.Run(_interaction, () => Sfall.SystemTab.RegisterChecksum(_configuration.GameExePath)) || !SaveAll())
			{
				return;
			}

			try
			{
				Process.Start(new ProcessStartInfo(_configuration.GameExePath)
				{
					WorkingDirectory = _configuration.GamePath,
					UseShellExecute = true,
				});
			}
			catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
			{
				_interaction.ShowError(exception.Message);
				return;
			}

			_closeWindow();
		}

		private void ChooseGameFolder()
		{
			String? folder = _interaction.PickFolder(_configuration.GamePath, "Select the game folder");
			if (folder is not null && !String.Equals(Path.GetFullPath(folder), _configuration.GamePath, StringComparison.OrdinalIgnoreCase))
			{
				_changeGameFolder(folder);
			}
		}

		private void Done()
		{
			if (SaveAll())
			{
				_closeWindow();
			}
		}

		/// <summary>
		/// Saves everything, opens a file of the game folder in its editor, and closes the program.
		/// </summary>
		private void OpenInEditor(String relativePath)
		{
			if (!SaveAll())
			{
				return;
			}

			String path = PathHelper.Resolve(_configuration.GamePath, relativePath);
			try
			{
				Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
			}
			catch (Win32Exception)
			{
				// No program is associated with the file type.
				try
				{
					// The path goes in the argument list, which quotes it: a folder name with spaces stays one argument.
					Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path } });
				}
				catch (Win32Exception exception)
				{
					_interaction.ShowError(exception.Message, "Open error");
					return;
				}
			}

			_closeWindow();
		}

		/// <summary>
		/// Saves everything. Returns false, after telling the user, when a file could not be written.
		/// </summary>
		private Boolean SaveAll()
		{
			try
			{
				if (!String.IsNullOrWhiteSpace(Language.Text))
				{
					_configuration.AddLanguage(Language.Text);
				}


				// The load order and the configuration files are replaced together: all of them or none.
				_configuration.Save(Mods.StageSave() is { } order ? [order] : []);
				return true;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				_interaction.ShowError(exception.Message, "Save error");
				return false;
			}
		}

		private void OnExecutableChanged()
		{
			HiRes.Mode.Refresh();
			Sfall.RefreshExeState();
			OnPropertyChanged(nameof(CanRunGame));
			OnPropertyChanged(nameof(RunGameToolTip));
		}

		private static String GetVersion()
		{
			String version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";
			Int32 metadata = version.IndexOf('+');
			return metadata < 0 ? version : version.Substring(0, metadata);
		}

		private static String GetShortVersion()
		{
			String version = GetVersion();
			Int32 last = version.LastIndexOf('.');
			return last < 0 ? version : version.Substring(0, last);
		}
	}
}