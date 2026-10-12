namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Runtime.CompilerServices;
	using Configuration;

	/// <summary>
	/// Keeps every test away from the real settings of the user: until a test chooses its own folder, the settings
	/// go to a folder of the test run, which is deleted when the run ends.
	/// </summary>
	internal static class TestEnvironment
	{
		public static String SettingsFolder { get; } = Path.Combine(Path.GetTempPath(), "SFallConfiguratorTests", "settings-" + Guid.NewGuid().ToString("N"));

		[ModuleInitializer]
		internal static void Initialize()
		{
			UserSettings.DirectoryOverride = SettingsFolder;
			AppDomain.CurrentDomain.ProcessExit += (_, _) =>
			{
				try
				{
					Directory.Delete(SettingsFolder, recursive: true);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					// A leftover temporary folder is harmless.
				}
			};
		}
	}
}