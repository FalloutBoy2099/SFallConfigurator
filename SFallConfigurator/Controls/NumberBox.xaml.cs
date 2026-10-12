namespace SFallConfigurator.Controls
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;

	/// <summary>
	/// A text box for whole numbers with spin buttons. The value is kept within the minimum and maximum.
	/// </summary>
	public partial class NumberBox : UserControl
	{
		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
			nameof(Value),
			typeof(Int32),
			typeof(NumberBox),
			new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

		public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
			nameof(Minimum),
			typeof(Int32),
			typeof(NumberBox),
			new PropertyMetadata(Int32.MinValue, OnRangeChanged));

		public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
			nameof(Maximum),
			typeof(Int32),
			typeof(NumberBox),
			new PropertyMetadata(Int32.MaxValue, OnRangeChanged));

		public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
			nameof(Increment),
			typeof(Int32),
			typeof(NumberBox),
			new PropertyMetadata(1));

		public NumberBox()
		{
			InitializeComponent();
			ShowValue();
		}

		public Int32 Value
		{
			get => (Int32)GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}

		public Int32 Minimum
		{
			get => (Int32)GetValue(MinimumProperty);
			set => SetValue(MinimumProperty, value);
		}

		public Int32 Maximum
		{
			get => (Int32)GetValue(MaximumProperty);
			set => SetValue(MaximumProperty, value);
		}

		public Int32 Increment
		{
			get => (Int32)GetValue(IncrementProperty);
			set => SetValue(IncrementProperty, value);
		}

		private static Object CoerceValue(DependencyObject sender, Object baseValue)
		{
			NumberBox box = (NumberBox)sender;
			Int32 minimum = box.Minimum;
			Int32 maximum = Math.Max(box.Maximum, minimum);
			return Math.Clamp((Int32)baseValue, minimum, maximum);
		}

		private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			((NumberBox)sender).ShowValue();
		}

		private static void OnRangeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			sender.CoerceValue(ValueProperty);
			((NumberBox)sender).ShowValue();
		}

		private void ShowValue()
		{
			Editor.Text = Value.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Takes the number typed by the user. An invalid text is replaced by the current value.
		/// </summary>
		private void CommitText()
		{
			if (Int32.TryParse(Editor.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 typed))
			{
				Value = typed;
			}

			// The bound value may have been adjusted (or refused): always show the real value.
			ShowValue();
		}

		private void Step(Int32 direction)
		{
			CommitText();

			Int64 next = (Int64)Value + ((Int64)direction * Increment);
			Value = (Int32)Math.Clamp(next, Minimum, Math.Max(Maximum, Minimum));
			Editor.SelectAll();
		}

		private void OnIncreaseClick(Object sender, RoutedEventArgs e)
		{
			Step(1);
		}

		private void OnDecreaseClick(Object sender, RoutedEventArgs e)
		{
			Step(-1);
		}

		private void OnEditorLostKeyboardFocus(Object sender, KeyboardFocusChangedEventArgs e)
		{
			CommitText();
		}

		private void OnEditorPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Enter:
					CommitText();
					Editor.SelectAll();
					e.Handled = true;
					break;
				case Key.Up:
					Step(1);
					e.Handled = true;
					break;
				case Key.Down:
					Step(-1);
					e.Handled = true;
					break;
				case Key.Escape:
					ShowValue();
					e.Handled = true;
					break;
			}
		}

		private void OnEditorMouseWheel(Object sender, MouseWheelEventArgs e)
		{
			if (Editor.IsKeyboardFocusWithin)
			{
				Step(e.Delta > 0 ? 1 : -1);
				e.Handled = true;
			}
		}

		/// <summary>
		/// Only digits, and a minus sign when negative values are allowed, can be typed.
		/// </summary>
		private void OnEditorPreviewTextInput(Object sender, TextCompositionEventArgs e)
		{
			e.Handled = !IsNumberText(e.Text);
		}

		private void OnEditorPasting(Object sender, DataObjectPastingEventArgs e)
		{
			if (!e.DataObject.GetDataPresent(typeof(String)) || !IsNumberText((String)e.DataObject.GetData(typeof(String))!))
			{
				e.CancelCommand();
			}
		}

		private Boolean IsNumberText(String text)
		{
			for (Int32 index = 0; index < text.Length; index++)
			{
				Char character = text[index];
				Boolean isMinus = character == '-' && Minimum < 0;
				if (!Char.IsAsciiDigit(character) && !isMinus)
				{
					return false;
				}
			}

			return true;
		}
	}
}