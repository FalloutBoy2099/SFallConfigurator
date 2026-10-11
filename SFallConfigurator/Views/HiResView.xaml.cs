namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Threading;

	/// <summary>
	/// The Hi-Res patch configuration tab.
	/// </summary>
	public partial class HiResView : UserControl
	{
		public HiResView()
		{
			InitializeComponent();
		}

		/// <summary>
		/// A newly loaded game folder always starts on the first page.
		/// </summary>
		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			// The pages of the new folder are bound after this event: select the first one once they exist.
			Dispatcher.BeginInvoke(() => Pages.SelectedIndex = 0, DispatcherPriority.Loaded);
		}
	}
}