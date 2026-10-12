namespace SFallConfigurator
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Threading;
	using Configuration;
	using Localization;
	using Options;
	using Services;
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
		private WindowState _stateBeforeMinimize = WindowState.Normal;

		internal MainWindow(MainViewModel viewModel)
		{
			_viewModel = viewModel;
			DataContext = viewModel;
			InitializeComponent();
			TitleBarTheme.Attach(this);
			RestorePlacement();
			viewModel.NavigationRequested += OnNavigationRequested;
		}

		/// <summary>
		/// What the window shows. It is replaced when the user chooses another game folder or language.
		/// </summary>
		internal MainViewModel ViewModel
		{
			get => _viewModel;
			set => SetViewModel(value, keepTab: false);
		}

		/// <summary>
		/// Shows another view model. With <paramref name="keepTab"/> the tab that was selected stays selected.
		/// </summary>
		internal void SetViewModel(MainViewModel viewModel, Boolean keepTab)
		{
			Object? selected = keepTab ? MainTabs.SelectedItem : null;
			_viewModel.NavigationRequested -= OnNavigationRequested;
			_viewModel.Dispose();
			_viewModel = viewModel;
			_viewModel.NavigationRequested += OnNavigationRequested;
			DataContext = viewModel;

			if (selected is TabItem tab)
			{
				// The tabs are shown by bindings that are updated after the view model is set.
				Dispatcher.BeginInvoke(() => MainTabs.SelectedItem = tab.Visibility == Visibility.Visible ? tab : GetStartTab(), DispatcherPriority.Loaded);
			}
			else
			{
				SelectStartTab();
			}
		}

		/// <summary>
		/// Shows the tab of an option that was searched, and takes the user to the option.
		/// </summary>
		private void OnNavigationRequested(Object? sender, NavigationTarget target)
		{
			MainTabs.SelectedItem = target.Section switch
			{
				MainSection.Sfall => SfallTab,
				MainSection.HiRes => HiResTab,
				MainSection.Mods => ModsTab,
				_ => SettingsTab,
			};

			// The page of the tab is created when it is shown.
			Dispatcher.BeginInvoke(() => Reveal(target.Option), DispatcherPriority.ContextIdle);
		}

		private void Reveal(ConfigOption option)
		{
			if (FindElement(MainTabs, option) is { } element)
			{
				element.BringIntoView();
				element.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
			}
		}

		/// <summary>
		/// The outermost element that shows the option: the first one, going down, whose data is the option.
		/// </summary>
		private static FrameworkElement? FindElement(DependencyObject parent, ConfigOption option)
		{
			for (Int32 index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, index);
				if (child is FrameworkElement { IsVisible: true } element && ReferenceEquals(element.DataContext, option))
				{
					return element;
				}

				if (FindElement(child, option) is { } found)
				{
					return found;
				}
			}

			return null;
		}

		/// <summary>
		/// The down arrow takes the focus from the search box to the list of the results.
		/// </summary>
		private void OnSearchKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Down && SearchResults.Items.Count > 0)
			{
				SearchResults.Focus();
				e.Handled = true;
			}
		}

		/// <summary>
		/// Typing starts at once when the search bar is shown.
		/// </summary>
		private void OnSearchBarVisibleChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is true)
			{
				Dispatcher.BeginInvoke(
					() =>
					{
						SearchBox.Focus();
						SearchBox.SelectAll();
					},
					DispatcherPriority.Input);
			}
		}

		private void OnSearchResultClick(Object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(SearchResults, source) is ListBoxItem)
			{
				_viewModel.Search.Open();
			}
		}

		private TabItem GetStartTab()
		{
			return _viewModel.IsGameConfigured ? SfallTab : DatInfoTab;
		}

		private void SelectStartTab()
		{
			// The tabs of a configured game are shown by bindings that are updated after the view model is set.
			Dispatcher.BeginInvoke(
				() => MainTabs.SelectedItem = GetStartTab(),
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

			// The window comes back on the monitor that is nearest to where it was: a monitor that is gone, or a corner of
			// the bounding box of the monitors where there is no screen, cannot hide it.
			Rect area = MonitorAreas.GetWorkAreaNear(new Rect(saved.Value.Left, saved.Value.Top, saved.Value.Width, saved.Value.Height), VisualTreeHelper.GetDpi(this).DpiScaleX)
				?? new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
			WindowPlacement placement = saved.Value.FitInto(area.Left, area.Top, area.Width, area.Height);

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
			Boolean maximized = (WindowState == WindowState.Minimized ? _stateBeforeMinimize : WindowState) == WindowState.Maximized;
			new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized).Save();
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
			if (WindowState != WindowState.Minimized)
			{
				_stateBeforeMinimize = WindowState;
			}

			Boolean maximized = WindowState == WindowState.Maximized;
			Frame.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameVerticalBorderWidth) : new Thickness(0);
			MaximizeButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;
		}

		/// <summary>
		/// Ctrl+Z and Ctrl+Y undo and redo the last change of the option that has the focus. A text box keeps its own undo.
		/// </summary>
		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.Z or Key.Y
				&& Keyboard.FocusedElement is FrameworkElement { DataContext: ConfigOption option } and not TextBox)
			{
				if (e.Key == Key.Y || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					option.Redo();
				}
				else
				{
					option.Undo();
				}

				e.Handled = true;
			}
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

		/// <summary>
		/// Opens the menu of a button of the title bar: the entries come from the view model.
		/// </summary>
		private void OnMenuButtonClick(Object sender, RoutedEventArgs e)
		{
			if (sender is Button { ContextMenu: { } menu } button)
			{
				menu.PlacementTarget = button;
				menu.Placement = PlacementMode.Bottom;
				menu.IsOpen = true;
			}
		}
	}
}