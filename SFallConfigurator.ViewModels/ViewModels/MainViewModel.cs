namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;
	using Services;

	/// <summary>
	/// The main window: the tabs, and the buttons that save the configuration and start the game.
	/// </summary>
	internal sealed class MainViewModel : ObservableObject, IDisposable
	{
		private const String PreviewBeforeSavingSetting = "PreviewBeforeSaving";
		private const String AutoReloadSetting = "AutoReload";

		private readonly GameConfiguration _configuration;
		private readonly AppServices _services;
		private readonly IUserInteraction _interaction;
		private readonly IAppHost _host;
		private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

		private ConfigFileWatcher? _watcher;
		private String? _notice;
		private Boolean _canReloadNow;

		public MainViewModel(GameConfiguration configuration, AppServices services, IAppHost host)
		{
			_configuration = configuration;
			_services = services;
			_interaction = services.Interaction;
			_host = host;

			RecentGameFolders = GameLocator.GetRecentFolders()
				.Where(folder => !String.Equals(folder, configuration.GamePath, StringComparison.OrdinalIgnoreCase))
				.Prepend(configuration.GamePath)
				.ToList();

			Title = "SFallConfigurator v" + GetShortVersion() + " - by Kain";

			RunGameCommand = new RelayCommand(RunGame, () => CanRunGame);
			ReviewChangesCommand = new RelayCommand(ReviewChanges, () => HasPendingChanges);
			CheckInstallationCommand = new RelayCommand(CheckInstallation, () => IsGameConfigured);
			RestoreBackupCommand = new RelayCommand(RestoreBackup, () => IsGameConfigured);
			ReloadNowCommand = new RelayCommand(() => _host.Reload(), () => CanReloadNow);
			DismissNoticeCommand = new RelayCommand(() => ShowNotice(null, canReload: false));
			DoneCommand = new RelayCommand(Done);
			ChooseGameFolderCommand = new RelayCommand(ChooseGameFolder);
			ChangeLanguageCommand = new RelayCommand(code => _host.ChangeLanguage((String)code!));
			ThemeChoices = ViewModels.ThemeChoices.Create(services.Theme);
			LanguageChoices = ViewModels.LanguageChoices.Create(host);

			OptionContext context = new(configuration, services);
			HiRes = new HiResViewModel(context, OpenInEditor);
			Sfall = new SfallViewModel(context, HiRes, OpenInEditor);
			Mods = new ModsViewModel(context, OpenInEditor);
			Settings = new GameSettingsViewModel(context, OpenInEditor);
			DatInfo = new DatInfoViewModel(configuration.GamePath, _interaction);
			Language = Settings.Language;

			Search = new SearchViewModel(GetSearchLocations, Navigate);
			Profiles = new ProfilesViewModel(new ProfileStore(Path.Combine(UserSettings.SettingsDirectory, "profiles")), () => AllOptions, _interaction);

			Settings.ExecutableChanged += (_, _) => OnExecutableChanged();
			context.ReloadOptions = ReloadOptions;
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

		public SearchViewModel Search { get; }

		public ProfilesViewModel Profiles { get; }

		/// <summary>
		/// Raised when an option must be shown: the window selects its tab, and the view model its page.
		/// </summary>
		public event EventHandler<NavigationTarget>? NavigationRequested;

		/// <summary>
		/// The entries of the theme menu.
		/// </summary>
		public IReadOnlyList<MenuChoice> ThemeChoices { get; }

		/// <summary>
		/// The entries of the language menu.
		/// </summary>
		public IReadOnlyList<MenuChoice> LanguageChoices { get; }

		public ICommand RunGameCommand { get; }

		/// <summary>
		/// Shows the settings that saving would change.
		/// </summary>
		public ICommand ReviewChangesCommand { get; }

		/// <summary>
		/// Shows what is installed in the game folder and what looks wrong.
		/// </summary>
		public ICommand CheckInstallationCommand { get; }

		/// <summary>
		/// Puts back the game files of an earlier save, from the folder old_ini.
		/// </summary>
		public ICommand RestoreBackupCommand { get; }

		/// <summary>
		/// Reads the game folder again, dropping the changes that were not saved.
		/// </summary>
		public ICommand ReloadNowCommand { get; }

		public ICommand DismissNoticeCommand { get; }

		/// <summary>
		/// A sentence for the user, shown on top of the window until it is dismissed; null when there is none.
		/// </summary>
		public String? Notice => _notice;

		public Boolean HasNotice => _notice is not null;

		/// <summary>
		/// The notice offers to read the folder again.
		/// </summary>
		public Boolean CanReloadNow => _canReloadNow;

		/// <summary>
		/// When set, the folder is read again as soon as another program changes its files, if there is nothing to lose. The choice is
		/// remembered.
		/// </summary>
		public Boolean AutoReload
		{
			get => UserSettings.Get(AutoReloadSetting) != "0";
			set
			{
				if (value != AutoReload)
				{
					UserSettings.Set(AutoReloadSetting, value ? null : "0");
					OnPropertyChanged();
				}
			}
		}

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
					_host.ChangeGameFolder(value);
				}

				// When the change was refused the list must show the current folder again.
				_uiContext?.Post(_ => OnPropertyChanged(nameof(SelectedGameFolder)), null);
			}
		}

		/// <summary>
		/// Every option of the program that is stored in a configuration file.
		/// </summary>
		public IEnumerable<ConfigOption> AllOptions => Settings.Options
			.Concat([Mods.DisableAutoloading])
			.Concat(Sfall.Pages.SelectMany(page => page.Options))
			.Concat(HiRes.Pages.SelectMany(page => page.Options));

		/// <summary>
		/// True when something was changed since the files were read or saved.
		/// </summary>
		public Boolean HasPendingChanges => _configuration.HasPendingChanges || Mods.IsModified;

		/// <summary>
		/// When set, saving first shows what will change and asks to go on. The choice is remembered.
		/// </summary>
		public Boolean PreviewBeforeSaving
		{
			get => UserSettings.Get(PreviewBeforeSavingSetting) == "1";
			set
			{
				if (value != PreviewBeforeSaving)
				{
					UserSettings.Set(PreviewBeforeSavingSetting, value ? "1" : null);
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		/// False when ddraw.ini was not found: only the DAT archives can be browsed then.
		/// </summary>
		public Boolean IsGameConfigured => _configuration.DdrawExists;

		/// <summary>
		/// The game can be started: the selected executable exists.
		/// </summary>
		public Boolean CanRunGame => IsGameConfigured && _services.FileSystem.FileExists(_configuration.GameExePath);

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
		/// Reads the options again after a file was merged or replaced; the load order being edited is kept.
		/// </summary>
		private void ReloadOptions()
		{
			Settings.Load();
			Mods.LoadOptions();

			// The Hi-Res patch is read before sfall: the sfall resolution editor compares with it.
			HiRes.Load();
			Sfall.Load();
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

		/// <summary>
		/// Shows an option: its tab, its page, and the option itself.
		/// </summary>
		public void Navigate(NavigationTarget target)
		{
			if (target.Page is not null)
			{
				if (target.Section == MainSection.Sfall)
				{
					Sfall.SelectedPage = target.Page;
				}
				else if (target.Section == MainSection.HiRes)
				{
					HiRes.SelectedPage = target.Page;
				}
			}

			target.Option.Highlight();
			NavigationRequested?.Invoke(this, target);
		}

		private IEnumerable<(MainSection Section, String SectionName, OptionPage? Page, IEnumerable<ConfigOption> Options)> GetSearchLocations()
		{
			if (!IsGameConfigured)
			{
				yield break;
			}

			foreach (OptionPage page in Sfall.Pages)
			{
				yield return (MainSection.Sfall, "sFall Config", page, page.Options);
			}

			foreach (OptionPage page in HiRes.Pages)
			{
				yield return (MainSection.HiRes, "Hi-Res Config", page, page.Options);
			}

			yield return (MainSection.Mods, "Mods", null, [Mods.DisableAutoloading]);
			yield return (MainSection.Settings, "Settings", null, Settings.Options);
		}

		/// <summary>
		/// What saving would write now, file by file.
		/// </summary>
		public ChangePreview CreatePreview()
		{
			return ChangePreviewBuilder.Create(_configuration.GetChanges(), AllOptions, Mods.SavedOrder, Mods.CurrentOrder);
		}

		/// <summary>
		/// Shows a sentence on top of the window. With <paramref name="canReload"/> it offers to read the folder again.
		/// </summary>
		public void ShowNotice(String? text, Boolean canReload)
		{
			_notice = String.IsNullOrEmpty(text) ? null : text;
			_canReloadNow = canReload && _notice is not null;
			OnPropertyChanged(nameof(Notice));
			OnPropertyChanged(nameof(HasNotice));
			OnPropertyChanged(nameof(CanReloadNow));
		}

		/// <summary>
		/// Starts to watch the files: when another program changes them, the folder is read again or the user is told.
		/// </summary>
		public void StartWatching(TimeSpan? quietTime = null)
		{
			if (_watcher is not null || !IsGameConfigured)
			{
				return;
			}

			_watcher = new ConfigFileWatcher(() => _configuration.GetTrackedFiles(), quietTime ?? TimeSpan.FromMilliseconds(700));
			_watcher.Changed += (_, files) =>
			{
				if (_uiContext is null)
				{
					HandleExternalChange(files);
				}
				else
				{
					_uiContext.Post(_ => HandleExternalChange(files), null);
				}
			};
			_watcher.Start();
		}

		/// <summary>
		/// Called when files of the folder were changed by another program.
		/// </summary>
		internal void HandleExternalChange(IReadOnlyList<String> files)
		{
			String names = String.Join(", ", files.Select(file => Path.GetRelativePath(_configuration.GamePath, file)));
			if (AutoReload && !HasPendingChanges)
			{
				_host.Reload(Localizer.F("{0} changed on disk: the settings were read again.", names));
			}
			else
			{
				ShowNotice(Localizer.F("{0} changed on disk. Read the files again to see the new content: the changes that were not saved will be lost.", names), canReload: true);
			}
		}

		public void Dispose()
		{
			_watcher?.Dispose();
			_watcher = null;
		}

		private void RestoreBackup()
		{
			IReadOnlyList<BackupSet> sets = ErrorGuard.Try(_configuration.Backups.GetSets, []);
			if (sets.Count == 0)
			{
				_interaction.ShowMessage(Localizer.F("There is no backup yet. The files are copied to the folder {0} each time the settings are saved.", BackupStore.FolderName));
				return;
			}

			List<String> items = sets.Select(set => set.Time.ToString("g", System.Globalization.CultureInfo.CurrentCulture) + "  -  " + String.Join(", ", set.Files)).ToList();
			Int32? index = _interaction.PickItem("Choose the backup to restore:", items, "Restore a backup");
			if (index is null || !_interaction.Confirm(Localizer.T("Restore the files of this backup? The current files are kept in a new backup, and the changes that were not saved are lost.")))
			{
				return;
			}

			BackupSet chosen = sets[index.Value];
			_watcher?.Suspend();
			Boolean restored = ErrorGuard.Run(_interaction, () =>
			{
				_configuration.Backups.Backup(chosen.Files.Select(file => Path.Combine(_configuration.GamePath, file)));
				_configuration.Backups.Restore(chosen);
			});
			_watcher?.Resume(takeSnapshot: restored);

			if (restored)
			{
				_host.Reload();
			}
		}

		private void CheckInstallation()
		{
			_interaction.ShowReport("Installation check", InstallationInspector.Inspect(_configuration, _services, HiRes.Mode.ActiveMode));
		}

		private void ReviewChanges()
		{
			_interaction.ReviewChanges(CreatePreview(), askToSave: false);
		}

		private void RunGame()
		{
			if (!ErrorGuard.Run(_interaction, () => Sfall.SystemTab.RegisterChecksum(_configuration.GameExePath)) || !SaveAll())
			{
				return;
			}

			try
			{
				_services.ProcessLauncher.StartGame(_configuration.GameExePath, _configuration.GamePath);
			}
			catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
			{
				_interaction.ShowError(exception.Message);
				return;
			}

			_host.Close();
		}

		private void ChooseGameFolder()
		{
			String? folder = _interaction.PickFolder(_configuration.GamePath, "Select the game folder");
			if (folder is not null && !String.Equals(Path.GetFullPath(folder), _configuration.GamePath, StringComparison.OrdinalIgnoreCase))
			{
				_host.ChangeGameFolder(folder);
			}
		}

		private void Done()
		{
			if (SaveAll())
			{
				_host.Close();
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
				_services.ProcessLauncher.OpenFile(path);
			}
			catch (Win32Exception exception)
			{
				_interaction.ShowError(exception.Message, "Open error");
				return;
			}

			_host.Close();
		}

		/// <summary>
		/// Saves everything. Returns false, after telling the user, when a file could not be written.
		/// </summary>
		private Boolean SaveAll()
		{
			// The files that the program writes itself are not a change made by another program.
			_watcher?.Suspend();
			Boolean saved = false;
			try
			{
				if (!String.IsNullOrWhiteSpace(Language.Text))
				{
					_configuration.AddLanguage(Language.Text);
				}

				if (PreviewBeforeSaving && HasPendingChanges && CreatePreview() is { IsEmpty: false } preview && !_interaction.ReviewChanges(preview, askToSave: true))
				{
					return false;
				}

				// The load order and the configuration files are replaced together: all of them or none.
				_configuration.Save(Mods.StageSave() is { } order ? [order] : []);
				saved = true;
				return true;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				_interaction.ShowError(exception.Message, "Save error");
				return false;
			}
			finally
			{
				_watcher?.Resume(takeSnapshot: saved);
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