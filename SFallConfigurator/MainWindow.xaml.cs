namespace SFallConfigurator
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Media;
	using System.Windows.Threading;
	using Configuration;
	using Localization;
	using ViewModels;
	using Views;

	/// <summary>
	/// The main window of the configurator. It draws its own title bar, so that the bar follows the theme.
	/// </summary>
	public partial class MainWindow : Window
	{
		private const String MaximizeGlyph = "";
		private const String RestoreGlyph = "";

		private MainViewModel _viewModel;

		internal MainWindow(MainViewModel viewModel)
		{
			_viewModel = viewModel;
			DataContext = viewModel;
			InitializeComponent();
			TitleBarTheme.Attach(this);
			RestorePlacement();
		}

		/// <summary>
		/// What the window shows. It is replaced when the user chooses another game folder or language.
		/// </summary>
		internal MainViewModel ViewModel
		{
			get => _viewModel;
			set
			{
				_viewModel = value;
				DataContext = value;
				SelectStartTab();
			}
		}

		private void SelectStartTab()
		{
			// The tabs of a configured game are shown by bindings that are updated after the view model is set.
			Dispatcher.BeginInvoke(
				() => MainTabs.SelectedItem = _viewModel.IsGameConfigured ? SfallTab : DatInfoTab,
				DispatcherPriority.Loaded);
		}

		/// <summary>
		/// Puts the window where it was when the program ended last time.
		/// </summary>
		private void RestorePlacement()
		{
			WindowPlacement? saved = WindowPlacement.Load();
			if (saved is null)
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen;
				return;
			}

			WindowPlacement placement = saved.Value.FitInto(
				SystemParameters.VirtualScreenLeft,
				SystemParameters.VirtualScreenTop,
				SystemParameters.VirtualScreenWidth,
				SystemParameters.VirtualScreenHeight);

			WindowStartupLocation = WindowStartupLocation.Manual;
			Width = Math.Max(placement.Width, MinWidth);
			Height = Math.Max(placement.Height, MinHeight);
			Left = placement.Left;
			Top = placement.Top;
			if (placement.IsMaximized)
			{
				WindowState = WindowState.Maximized;
			}
		}

		private void SavePlacement()
		{
			Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
			new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized).Save();
		}

		private void OnContentRendered(Object? sender, EventArgs e)
		{
			ContentRendered -= OnContentRendered;

			SelectStartTab();
			_viewModel.ReportMissingConfiguration();
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			// A number that is still being typed reaches its option when its box loses the focus: take the focus away first.
			Focus();
			e.Cancel = !_viewModel.ConfirmClose();
			if (!e.Cancel)
			{
				SavePlacement();
			}
		}

		private void OnStateChanged(Object? sender, EventArgs e)
		{
			// A maximized window with a custom frame extends beyond the screen by the size of its resize border.
			Boolean maximized = WindowState == WindowState.Maximized;
			Frame.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameVerticalBorderWidth) : new Thickness(0);
			MaximizeButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;
		}

		private void OnMinimizeClick(Object sender, RoutedEventArgs e)
		{
			SystemCommands.MinimizeWindow(this);
		}

		private void OnMaximizeClick(Object sender, RoutedEventArgs e)
		{
			if (WindowState == WindowState.Maximized)
			{
				SystemCommands.RestoreWindow(this);
			}
			else
			{
				SystemCommands.MaximizeWindow(this);
			}
		}

		private void OnCloseClick(Object sender, RoutedEventArgs e)
		{
			SystemCommands.CloseWindow(this);
		}

		private void OnThemeButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(ThemeButton);
			foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
			{
				MenuItem item = CreateMenuItem(Localizer.T(preference.ToString()), preference == ThemeManager.Preference);
				item.Click += (_, _) => ThemeManager.SetPreference(preference);
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private void OnLanguageButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(LanguageButton);
			foreach (LanguageInfo language in Localizer.Languages)
			{
				MenuItem item = CreateMenuItem(language.NativeName, language.Code == Localizer.Instance.Language);
				item.Command = _viewModel.ChangeLanguageCommand;
				item.CommandParameter = language.Code;
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private ContextMenu CreateMenu(Button target)
		{
			// The menu inherits the font of its target, which is the symbol font of the button: give it the font of the window.
			return new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom, FontFamily = FontFamily, FontSize = FontSize };
		}

		private MenuItem CreateMenuItem(String header, Boolean isCurrent)
		{
			// A check mark (a glyph of the symbol font) shows the choice in use.
			return new MenuItem
			{
				Header = header,
				Icon = isCurrent ? new TextBlock { Text = "\uE73E", FontFamily = (FontFamily)FindResource("SymbolThemeFontFamily") } : null,
			};
		}
	}
}