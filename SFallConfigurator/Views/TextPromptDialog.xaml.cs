namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;

	/// <summary>
	/// Asks the user for a line of text.
	/// </summary>
	public partial class TextPromptDialog : Window
	{
		internal TextPromptDialog(String prompt, String? initialText, String? caption)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Prompt.Text = prompt;
			Input.Text = initialText ?? String.Empty;
			if (caption is not null)
			{
				Title = caption;
			}

			Loaded += (_, _) => Input.SelectAll();
		}

		/// <summary>
		/// The text typed by the user; valid after the dialog was closed with "OK".
		/// </summary>
		internal String Result => Input.Text;

		private void OnOkClick(Object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}