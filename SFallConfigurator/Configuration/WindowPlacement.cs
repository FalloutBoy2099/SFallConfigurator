namespace SFallConfigurator.Configuration
{
	using System;
	using System.Globalization;

	/// <summary>
	/// Position, size and state of the main window, as they are kept between two runs.
	/// </summary>
	internal readonly record struct WindowPlacement(Double Left, Double Top, Double Width, Double Height, Boolean IsMaximized)
	{
		private const String LeftKey = "window_left";
		private const String TopKey = "window_top";
		private const String WidthKey = "window_width";
		private const String HeightKey = "window_height";
		private const String MaximizedKey = "window_maximized";

		/// <summary>
		/// The remembered placement, or null when there is none.
		/// </summary>
		public static WindowPlacement? Load()
		{
			if (TryReadNumber(LeftKey, out Double left)
				&& TryReadNumber(TopKey, out Double top)
				&& TryReadNumber(WidthKey, out Double width)
				&& TryReadNumber(HeightKey, out Double height)
				&& width > 0
				&& height > 0)
			{
				return new WindowPlacement(left, top, width, height, UserSettings.Get(MaximizedKey) == "1");
			}

			return null;
		}

		public void Save()
		{
			UserSettings.Set(LeftKey, Format(Left));
			UserSettings.Set(TopKey, Format(Top));
			UserSettings.Set(WidthKey, Format(Width));
			UserSettings.Set(HeightKey, Format(Height));
			UserSettings.Set(MaximizedKey, IsMaximized ? "1" : "0");
		}

		/// <summary>
		/// Moves and shrinks the placement so that it is visible in the given area (the virtual screen).
		/// </summary>
		public WindowPlacement FitInto(Double areaLeft, Double areaTop, Double areaWidth, Double areaHeight)
		{
			Double width = Math.Min(Width, areaWidth);
			Double height = Math.Min(Height, areaHeight);
			Double left = Math.Clamp(Left, areaLeft, areaLeft + areaWidth - width);
			Double top = Math.Clamp(Top, areaTop, areaTop + areaHeight - height);
			return new WindowPlacement(left, top, width, height, IsMaximized);
		}

		private static Boolean TryReadNumber(String key, out Double value)
		{
			return Double.TryParse(UserSettings.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Double.IsFinite(value);
		}

		private static String Format(Double value)
		{
			return value.ToString("0.##", CultureInfo.InvariantCulture);
		}
	}
}