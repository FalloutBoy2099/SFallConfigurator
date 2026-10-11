namespace SFallConfigurator.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using Configuration;

	/// <summary>
	/// Lets the user choose a DirectInput key code from the list, or type one.
	/// </summary>
	public partial class KeyPickerDialog : Window
	{
		private readonly List<String> _entries = [];

		internal KeyPickerDialog(Int32 currentCode, Boolean allowModifiers)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			if (allowModifiers)
			{
				_entries.Add("-1  CTRL");
				_entries.Add("-2  ALT");
				_entries.Add("-3  SHIFT");
			}

			foreach (DirectInputKeys.KeyInfo key in DirectInputKeys.All)
			{
				_entries.Add(key.ListText);
			}

			KeyList.ItemsSource = _entries;

			String codeText = currentCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
			KeyList.Text = _entries.Find(entry => ParseCode(entry) == currentCode) ?? codeText;
		}

		/// <summary>
		/// The chosen key code; valid after the dialog was closed with "Done".
		/// </summary>
		internal Int32 SelectedCode { get; private set; }

		private static Int32? ParseCode(String text)
		{
			String trimmed = text.Trim();
			Int32 space = trimmed.IndexOf(' ');
			String number = space < 0 ? trimmed : trimmed.Substring(0, space);
			return Int32.TryParse(number, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Int32 code) ? code : null;
		}

		private void OnDoneClick(Object sender, RoutedEventArgs e)
		{
			Int32? code = ParseCode(KeyList.Text);
			if (code is null)
			{
				KeyList.Focus();
				return;
			}

			SelectedCode = code.Value;
			DialogResult = true;
		}
	}
}