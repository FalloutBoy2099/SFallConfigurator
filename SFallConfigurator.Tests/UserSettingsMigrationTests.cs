namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using Configuration;
	using Xunit;

	public class UserSettingsMigrationTests
	{
		[Fact]
		public void TheSettingsOfTheOldNameAreCopiedOnce()
		{
			using TestFolder folder = new();
			String legacy = folder.Write("old/settings.txt", "language=fr\r\ntheme=dark\r\n");
			String current = folder.Combine("new/settings.txt");

			UserSettings.MigrateLegacySettings(current, legacy);

			Assert.Equal(["language=fr", "theme=dark"], File.ReadAllLines(current));
			Assert.True(File.Exists(legacy));
		}

		[Fact]
		public void TheSettingsOfTheNewNameAreNeverReplacedByTheOldOnes()
		{
			using TestFolder folder = new();
			String legacy = folder.Write("old/settings.txt", "language=fr\r\n");
			String current = folder.Write("new/settings.txt", "language=de\r\n");

			UserSettings.MigrateLegacySettings(current, legacy);

			Assert.Equal(["language=de"], File.ReadAllLines(current));
		}

		[Fact]
		public void WithoutOldSettingsNothingIsCreated()
		{
			using TestFolder folder = new();
			String current = folder.Combine("new/settings.txt");

			UserSettings.MigrateLegacySettings(current, folder.Combine("old/settings.txt"));

			Assert.False(File.Exists(current));
			Assert.False(Directory.Exists(folder.Combine("new")));
		}
	}
}