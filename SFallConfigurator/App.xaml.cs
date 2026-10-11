namespace SFallConfigurator
{
	using System;
	using System.IO;
	using System.Windows;
	using System.Windows.Threading;
	using Configuration;
	using Infrastructure;
	using Localization;
	using ViewModels;
	using Views;

	/// <summary>
	/// Starts the configurator and keeps the window on the game folder chosen by the user.
	/// </summary>
	public partial class App : Application
	{
		private WpfUserInteraction? _interaction;
		private MainWindow? _window;
		private Boolean _isReportingError;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			DispatcherUnhandledException += OnUnhandledException;
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
			try
			{
				GameConfiguration configuration = new(GameLocator.Normalize(gamePath));
				configuration.Load();
				viewModel = new MainViewModel(configuration, _interaction!, () => _window?.Close(), ChangeGameFolder, ChangeLanguage);
				viewModel.Load();
			}
			catch (Exception exception) when (ErrorGuard.IsFileError(exception))
			{
				MessageDialog.Show(_window, exception.Message, "SFallConfigurator", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}

			// A folder that cannot be opened is not remembered.
			GameLocator.RememberGamePath(viewModel.Configuration.GamePath);
			return viewModel;
		}

		private void OnUnhandledException(Object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			// The window stays open: the changes the user made are not lost with a failure of one action.
			e.Handled = true;
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

		/// <summary>
		/// Switches the language of the interface: the window is rebuilt for the same game folder.
		/// </summary>
		private void ChangeLanguage(String code)
		{
			if (_window is null || code == Localizer.Instance.Language || !_window.ViewModel.ConfirmClose())
			{
				return;
			}

			String previous = Localizer.Instance.Language;
			Localizer.Instance.SetLanguage(code);

			MainViewModel? viewModel = CreateViewModel(_window.ViewModel.GamePath);
			if (viewModel is null)
			{
				// The window still shows the old language: the choice must not stay.
				Localizer.Instance.SetLanguage(previous);
				return;
			}

			_window.ViewModel = viewModel;
		}

		/// <summary>
		/// Switches the window to another game folder, after the pending changes were dealt with.
		/// </summary>
		private void ChangeGameFolder(String gamePath)
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