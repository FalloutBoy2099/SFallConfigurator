namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// Inverts a Boolean value.
	/// </summary>
	[ValueConversion(typeof(Boolean), typeof(Boolean))]
	internal sealed class InverseBooleanConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Boolean flag ? !flag : DependencyProperty.UnsetValue;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return Convert(value, targetType, parameter, culture);
		}
	}

	/// <summary>
	/// Turns an integer into its text form and back (used by controls that need a String).
	/// </summary>
	[ValueConversion(typeof(Int32), typeof(String))]
	internal sealed class Int32ToStringConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Int32 number ? number.ToString(CultureInfo.InvariantCulture) : String.Empty;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return Int32.TryParse(value as String, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 number) ? number : DependencyProperty.UnsetValue;
		}
	}

	/// <summary>
	/// Collapses an element when the bound value is null or an empty string.
	/// </summary>
	[ValueConversion(typeof(Object), typeof(Visibility))]
	internal sealed class NullToCollapsedConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object parameter, CultureInfo culture)
		{
			Boolean hasValue = value is not null && !(value is String text && text.Length == 0);
			return hasValue ? Visibility.Visible : Visibility.Collapsed;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return DependencyProperty.UnsetValue;
		}
	}
}