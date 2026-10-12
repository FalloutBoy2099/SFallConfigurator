namespace SFallConfigurator.Configuration
{
	using System;
	using System.Globalization;

	/// <summary>
	/// Conversions between ini text values and numbers. Always culture independent.
	/// </summary>
	internal static class IniValue
	{
		/// <summary>
		/// Parses a decimal or "0x" prefixed hexadecimal integer.
		/// </summary>
		public static Int32 ToInt32(String? value, Int32 fallback = 0)
		{
			return TryParseInt32(value, out Int32 number) ? number : fallback;
		}

		public static Boolean TryParseInt32(String? value, out Int32 number)
		{
			number = 0;
			if (String.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			String text = value.Trim();
			if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
			{
				return true;
			}

			return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				&& Int32.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out number);
		}

		public static Single ToSingle(String? value, Single fallback)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return fallback;
			}

			return Single.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out Single number) ? number : fallback;
		}

		public static String Format(Int32 number)
		{
			return number.ToString(CultureInfo.InvariantCulture);
		}
	}
}