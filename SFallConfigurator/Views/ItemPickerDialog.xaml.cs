namespace SFallConfigurator.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Input;

	/// <summary>
	/// Lets the user choose one item of a list.
	/// </summary>
	public partial class ItemPickerDialog : Window
	{
		internal ItemPickerDialog(String prompt, IReadOnlyList<String> items, String? caption)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Prompt.Text = prompt;
			Items.ItemsSource = items;
			Items.SelectedIndex = items.Count > 0 ? 0 : -1;
			if (caption is not null)
			{
				Title = caption;
			}
		}

		/// <summary>
		/// The item chosen by the user; valid after the dialog was closed with "OK".
		/// </summary>
		internal Int32 SelectedIndex => Items.SelectedIndex;

		private void OnOkClick(Object sender, RoutedEventArgs e)
		{
			if (Items.SelectedIndex >= 0)
			{
				DialogResult = true;
			}
		}

		private void OnItemDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			OnOkClick(sender, e);
		}
	}
}