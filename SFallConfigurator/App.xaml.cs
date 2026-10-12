namespace SFallConfigurator
{
	using System;
	using System.IO;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Threading;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Services;
	using ViewModels;
	using Views;

	/// <summary>
	/// Starts the configurator and keeps the window on the game folder chosen by the user.
	/// </summary>
	public partial class App : Application, IAppHost
	{
		private WpfUserInteraction? _interaction;
		private readonly ThemeService _theme = new();
		private MainWindow? _window;
		private Boolean _isReportingError;
		private FolderLock? _folderLock;

		// Kept in a field: the command manager holds its handlers weakly.
		private static readonly EventHandler RequeryCommands = (_, _) => RelayCommand.RaiseAll();

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			DispatcherUnhandledException += OnUnhandledException;
			CommandManager.RequerySuggested += RequeryCommands;
			ThemeManager.Initialize();
			TitleBarTheme.ApplyToAllWindows();
			Localizer.Instance.Initialize();

			// Installers start the program with "-install" to carry the settings over to a new version.
			Boolean? merged;
			try
			{
				merged = InstallMode.TryRun(PathHelper.GetApplicationDirectory(), e.Args);
			}
			catch (Exception exception) when (ErrorGuard.IsFileError(exception))
			{
				// An installer cannot answer a dialog: the settings simply stay as they are.
				merged = false;
			}

			if (merged is not null)
			{
				if (merged.Value)
				{
					MessageDialog.Show(null, Localizer.T("The existing settings of ddraw.ini and sfall-mods.ini were saved successfully."), "SFallConfigurator", MessageBoxButton.OK, MessageBoxImage.Information);
				}

				Shutdown();
				return;
			}

			_interaction = new WpfUserInteraction(() => _window);

			MainViewModel? viewModel = CreateViewModel(GameLocator.Resolve(e.Args));
			if (viewModel is null)
			{
				Shutdown(1);
				return;
			}

			_window = new MainWindow(viewModel);
			MainWindow = _window;
			_window.Show();
		}

		private MainViewModel? CreateViewModel(String gamePath)
		{
			MainViewModel viewModel;
			FolderLock? folderLock = null;
			try
			{
				String normalized = GameLocator.Normalize(gamePath);

				// Two windows on one folder would overwrite the changes of each other.
				folderLock = FolderLock.TryAcquire(normalized);
				if (folderLock is null && !AskToShareFolder(normalized))
				{
					return null;
				}

				GameConfiguration configuration = new(normalized);
				configuration.Load();
				viewModel = new MainViewModel(configuration, new AppServices(_interaction!) { Theme = _theme }, this);
				viewModel.Load();
			}
			catch (Exception exception) when (ErrorGuard.IsFileError(exception))
			{
				folderLock?.Dispose();
				MessageDialog.Show(_window, exception.Message, "SFallConfigurator", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}

			// The folder that was open is free for the other windows.
			_folderLock?.Dispose();
			_folderLock = folderLock;

			// A folder that cannot be opened is not remembered.
			GameLocator.RememberGamePath(viewModel.Configuration.GamePath);
			viewModel.StartWatching();
			return viewModel;
		}

		private Boolean AskToShareFolder(String gamePath)
		{
			return MessageDialog.Show(_window, Localizer.F("Another window of SFallConfigurator is already editing the folder {0}. Editing it in two windows can lose changes. Open it anyway?", gamePath), "SFallConfigurator", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
		}

		private void OnUnhandledException(Object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			// The window stays open: the changes the user made are not lost with a failure of one action.
			e.Handled = true;
			ErrorLog.Write(e.Exception);
			if (_isReportingError)
			{
				return;
			}

			_isReportingError = true;
			try
			{
				MessageDialog.Show(_window, e.Exception.Message, Localizer.T("Unexpected error"), MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				_isReportingError = false;
			}
		}

		void IAppHost.Close()
		{
			_window?.Close();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_folderLock?.Dispose();
			base.OnExit(e);
		}

		/// <summary>
		/// Reads the game folder again: the files changed outside of the program, or were restored from a backup.
		/// </summary>
		void IAppHost.Reload(String? notice)
		{
			if (_window is null)
			{
				return;
			}

			MainViewModel? viewModel = CreateViewModel(_window.ViewModel.GamePath);
			if (viewModel is not null)
			{
				viewModel.ShowNotice(notice, canReload: false);
				_window.SetViewModel(viewModel, keepTab: true);
			}
		}

		/// <summary>
		/// Switches the language of the interface: the window is rebuilt for the same game folder.
		/// </summary>
		void IAppHost.ChangeLanguage(String code)
		{
			if (_window is null || code == Localizer.Instance.Language || !_window.ViewModel.ConfirmClose())
			{
				return;
			}

			String previous = Localizer.Instance.Language;
			Localizer.Instance.SetLanguage(code);

			Boolean switched = false;
			try
			{
				MainViewModel? viewModel = CreateViewModel(_window.ViewModel.GamePath);
				if (viewModel is not null)
				{
					_window.SetViewModel(viewModel, keepTab: true);
					switched = true;
				}
			}
			finally
			{
				// The window still shows the old language: the choice must not stay, whatever stopped the switch.
				if (!switched)
				{
					Localizer.Instance.SetLanguage(previous);
				}
			}
		}

		/// <summary>
		/// Switches the window to another game folder, after the pending changes were dealt with.
		/// </summary>
		void IAppHost.ChangeGameFolder(String gamePath)
		{
			if (_window is null || !_window.ViewModel.ConfirmClose())
			{
				return;
			}

			MainViewModel? viewModel = CreateViewModel(gamePath);
			if (viewModel is not null)
			{
				_window.ViewModel = viewModel;
				viewModel.ReportMissingConfiguration();
			}
		}
	}
}