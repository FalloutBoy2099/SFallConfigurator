namespace SFallConfigurator.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Configuration;
	using Localization;
	using Services;

	/// <summary>
	/// Lets the user choose a DirectInput key code from the list, or type one.
	/// </summary>
	public partial class KeyPickerDialog : Window
	{
		private readonly List<String> _entries = [];

		private readonly Boolean _allowModifiers;

		private Boolean _isListening;

		internal KeyPickerDialog(Int32 currentCode, Boolean allowModifiers)
		{
			_allowModifiers = allowModifiers;
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

		/// <summary>
		/// The next key that is pressed gives its code.
		/// </summary>
		private void OnPressClick(Object sender, RoutedEventArgs e)
		{
			SetListening(!_isListening);
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (!_isListening)
			{
				return;
			}

			e.Handled = true;
			Key key = e.Key switch
			{
				Key.System => e.SystemKey,
				Key.ImeProcessed => e.ImeProcessedKey,
				_ => e.Key,
			};
			if (key == Key.Escape)
			{
				SetListening(false);
				return;
			}

			Int32? code = DirectInputScanCodes.FromKey(key, _allowModifiers);
			if (code is null)
			{
				Message.Text = Localizer.T("This key has no DirectInput code. Press another key.");
				return;
			}

			KeyList.Text = _entries.Find(entry => ParseCode(entry) == code) ?? code.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
			SetListening(false);
		}

		private void SetListening(Boolean listening)
		{
			_isListening = listening;
			// The button keeps its size whatever the text: a long translation is cut, not the window made larger.
			PressButton.Content = new TextBlock { Text = Localizer.T(listening ? "Press the key now..." : "Press a key"), TextTrimming = TextTrimming.CharacterEllipsis };
			Message.Text = listening ? Localizer.T("Press the key now. Escape cancels.") : String.Empty;
			if (!listening)
			{
				KeyList.Focus();
			}
		}

		private void OnDoneClick(Object sender, RoutedEventArgs e)
		{
			Int32? code = ParseCode(KeyList.Text);
			if (code is null || !DirectInputKeys.IsKnown(code.Value, _allowModifiers))
			{
				KeyList.Focus();
				return;
			}

			SelectedCode = code.Value;
			DialogResult = true;
		}
	}
}