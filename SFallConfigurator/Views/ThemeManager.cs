namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using Configuration;
	using Microsoft.Win32;

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
	/// Applies the theme preference to the application and remembers it.
	/// </summary>
	internal static class ThemeManager
	{
		private const String ThemeKey = "theme";

		public static ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

		/// <summary>
		/// Reads the remembered preference and applies it.
		/// </summary>
		public static void Initialize()
		{
			Preference = Parse(UserSettings.Get(ThemeKey));
			ApplyToApplication();
		}

		/// <summary>
		/// Changes the theme of the running application and remembers the choice.
		/// </summary>
		public static void SetPreference(ThemePreference preference)
		{
			Preference = preference;
			UserSettings.Set(ThemeKey, preference.ToString().ToLowerInvariant());
			ApplyToApplication();

			foreach (Window window in Application.Current.Windows)
			{
				TitleBarTheme.Apply(window);
			}
		}

		/// <summary>
		/// Whether the application is drawn with the dark theme, which depends on the preference and on Windows.
		/// </summary>
		public static Boolean IsDark()
		{
			return Preference switch
			{
				ThemePreference.Light => false,
				ThemePreference.Dark => true,
				_ => IsWindowsDark(),
			};
		}

		internal static ThemePreference Parse(String? text)
		{
			return Enum.TryParse(text, true, out ThemePreference preference) && Enum.IsDefined(preference) ? preference : ThemePreference.Automatic;
		}

		private static Boolean IsWindowsDark()
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			return key?.GetValue("AppsUseLightTheme") is Int32 light && light == 0;
		}

		private static void ApplyToApplication()
		{
			Application.Current.ThemeMode = Preference switch
			{
				ThemePreference.Light => ThemeMode.Light,
				ThemePreference.Dark => ThemeMode.Dark,
				_ => ThemeMode.System,
			};
		}
	}
}