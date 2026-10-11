namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using Localization;

	/// <summary>
	/// A message box in the theme of the application.
	/// </summary>
	public partial class MessageDialog : Window
	{
		private MessageBoxResult _result = MessageBoxResult.None;

		internal MessageDialog(String message, String caption, MessageBoxButton buttons, MessageBoxImage image)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Title = caption;
			Message.Text = message;
			Glyph.Text = GetGlyph(image);
			Glyph.Visibility = Glyph.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

			switch (buttons)
			{
				case MessageBoxButton.OK:
					AddButton(MessageBoxResult.OK, isDefault: true, isCancel: true);
					break;
				case MessageBoxButton.YesNo:
					AddButton(MessageBoxResult.Yes, isDefault: true, isCancel: false);
					AddButton(MessageBoxResult.No, isDefault: false, isCancel: true);
					break;
				case MessageBoxButton.YesNoCancel:
					AddButton(MessageBoxResult.Yes, isDefault: true, isCancel: false);
					AddButton(MessageBoxResult.No, isDefault: false, isCancel: false);
					AddButton(MessageBoxResult.Cancel, isDefault: false, isCancel: true);
					break;
				default:
					AddButton(MessageBoxResult.OK, isDefault: true, isCancel: true);
					AddButton(MessageBoxResult.Cancel, isDefault: false, isCancel: true);
					break;
			}
		}

		/// <summary>
		/// Shows the message and returns the button the user pressed. The window is closed with the cancel button
		/// (or Escape, or the close box) when there is one.
		/// </summary>
		internal static MessageBoxResult Show(Window? owner, String message, String caption, MessageBoxButton buttons, MessageBoxImage image)
		{
			MessageDialog dialog = new MessageDialog(message, caption, buttons, image);
			if (owner is { IsLoaded: true })
			{
				dialog.Owner = owner;
			}
			else
			{
				dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}

			dialog.ShowDialog();
			return dialog._result;
		}

		private static String GetGlyph(MessageBoxImage image)
		{
			// Glyphs of the symbol font: information, warning, error, question.
			return image switch
			{
				MessageBoxImage.Information => "",
				MessageBoxImage.Warning => "",
				MessageBoxImage.Error => "",
				MessageBoxImage.Question => "",
				_ => String.Empty,
			};
		}

		private static String GetCaption(MessageBoxResult result)
		{
			return Localizer.T(result switch
			{
				MessageBoxResult.Yes => "Yes",
				MessageBoxResult.No => "No",
				MessageBoxResult.Cancel => "Cancel",
				_ => "OK",
			});
		}

		private void AddButton(MessageBoxResult result, Boolean isDefault, Boolean isCancel)
		{
			Button button = new Button
			{
				Content = GetCaption(result),
				MinWidth = 80,
				Margin = new Thickness(Buttons.Children.Count == 0 ? 0 : 8, 0, 0, 0),
				IsDefault = isDefault,
				IsCancel = isCancel,
			};

			if (isDefault)
			{
				button.Style = (Style)FindResource("AccentButtonStyle");
			}

			button.Click += (_, _) =>
			{
				_result = result;
				Close();
			};

			Buttons.Children.Add(button);
		}
	}
}