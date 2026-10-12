namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using Configuration;
	using Services;
	using Microsoft.Win32;

	/// <summary>
	/// Applies the theme preference to the application and remembers it.
	/// </summary>
	internal static class ThemeManager
	{
		public static ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

		/// <summary>
		/// Reads the remembered preference and applies it.
		/// </summary>
		public static void Initialize()
		{
			Apply(MemoryThemeService.Parse(UserSettings.Get("theme")));
		}

		/// <summary>
		/// Changes the theme of the running application: its colours, and the title bar of every open window.
		/// </summary>
		public static void Apply(ThemePreference preference)
		{
			Preference = preference;
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