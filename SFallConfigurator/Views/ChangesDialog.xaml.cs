namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// Lists the settings that saving would change: what each one was, and what it will be.
	/// </summary>
	public partial class ChangesDialog : Window
	{
		internal ChangesDialog(ChangePreview preview, Boolean askToSave)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			DataContext = preview;
			EmptyText.Visibility = preview.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
			if (!askToSave)
			{
				SaveButton.Visibility = Visibility.Collapsed;
				CloseButton.Content = Localizer.T("Close");
				CloseButton.IsDefault = true;
			}
		}

		private void OnSaveClick(Object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}