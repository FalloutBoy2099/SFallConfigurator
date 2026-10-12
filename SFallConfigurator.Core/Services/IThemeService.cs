namespace SFallConfigurator.Services
{
	using System;
	using Configuration;

	/// <summary>
	/// The theme choice of the user: follow Windows, or always light, or always dark.
	/// </summary>
	internal enum ThemePreference
	{
		Automatic,
		Light,
		Dark,
	}

	/// <summary>
	/// The theme of the application. The window applies it; the view models only choose it.
	/// </summary>
	internal interface IThemeService
	{
		ThemePreference Preference { get; }

		/// <summary>
		/// Changes the theme and remembers the choice.
		/// </summary>
		void SetPreference(ThemePreference preference);
	}

	/// <summary>
	/// Remembers the choice and applies nothing: for the tests, and the base of the service of the window.
	/// </summary>
	internal class MemoryThemeService : IThemeService
	{
		private const String ThemeKey = "theme";

		public ThemePreference Preference { get; protected set; } = Parse(UserSettings.Get(ThemeKey));

		public virtual void SetPreference(ThemePreference preference)
		{
			Preference = preference;
			UserSettings.Set(ThemeKey, preference.ToString().ToLowerInvariant());
		}

		public static ThemePreference Parse(String? text)
		{
			return Enum.TryParse(text, true, out ThemePreference preference) && Enum.IsDefined(preference) ? preference : ThemePreference.Automatic;
		}
	}
}