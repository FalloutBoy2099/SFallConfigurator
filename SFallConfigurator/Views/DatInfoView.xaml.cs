namespace SFallConfigurator.Views
{
	using System;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using ViewModels;

	/// <summary>
	/// The Dat Info tab: the archives of the game and their descriptions.
	/// </summary>
	public partial class DatInfoView : UserControl
	{
		public DatInfoView()
		{
			InitializeComponent();
		}

		private async void OnIsVisibleChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			// The archives may have changed since the tab was last shown.
			if (IsVisible)
			{
				await RefreshAsync();
			}
		}

		private async void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			// Another game folder was chosen while the tab is shown.
			if (IsVisible)
			{
				await RefreshAsync();
			}
		}

		private async Task RefreshAsync()
		{
			if (DataContext is DatInfoViewModel viewModel)
			{
				await viewModel.RefreshAsync();
			}
		}

		private void OnArchivesMouseDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			// Only a double click on an archive edits it, not on the header or on an empty area.
			if (DataContext is DatInfoViewModel viewModel
				&& Archives.SelectedItem is not null
				&& ItemsControl.ContainerFromElement(Archives, (DependencyObject)e.OriginalSource) is ListViewItem)
			{
				viewModel.EditDescriptionCommand.Execute(null);
			}
		}
	}
}