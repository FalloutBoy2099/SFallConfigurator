namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Text;
	using Configuration;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// What happens to the game files when they are written, and when they cannot be.
	/// </summary>
	public sealed class SavingTests : IDisposable
	{
		private readonly TestFolder _settings = new TestFolder();

		public SavingTests()
		{
			UserSettings.DirectoryOverride = _settings.Path;
		}

		public void Dispose()
		{
			UserSettings.DirectoryOverride = null;
			_settings.Dispose();
		}

		private static void Lock(String path)
		{
			File.SetAttributes(path, FileAttributes.ReadOnly);
		}

		private static void Unlock(String path)
		{
			File.SetAttributes(path, FileAttributes.Normal);
		}

		[Fact]
		public void AFileIsReplacedWithoutLeavingATemporaryFile()
		{
			using TestFolder folder = new TestFolder();
			String path = folder.Write("a.ini", "[A]\r\nKey=1\r\n");

			StagedFile.WriteAllLines(path, ["[A]", "Key=2"], new UTF8Encoding(false));

			Assert.Equal(["[A]", "Key=2"], File.ReadAllLines(path));
			Assert.Equal([path], Directory.GetFiles(folder.Path));
		}

		[Fact]
		public void AFailedWriteLeavesTheOldFileAndNoTemporaryFile()
		{
			using TestFolder folder = new TestFolder();
			String path = folder.Write("a.ini", "[A]\r\nKey=1\r\n");
			Lock(path);
			try
			{
				Assert.ThrowsAny<Exception>(() => StagedFile.WriteAllLines(path, ["[A]", "Key=2"], new UTF8Encoding(false)));
			}
			finally
			{
				Unlock(path);
			}

			Assert.Equal(["[A]", "Key=1"], File.ReadAllLines(path));
			Assert.Equal([path], Directory.GetFiles(folder.Path));
		}

		[Fact]
		public void WhenOneGameFileCannotBeWrittenNoneOfThemIsTouched()
		{
			using TestFolder folder = new TestFolder();
			String ddraw = folder.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			String cfg = folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();
			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x1, 0x2"));
			Assert.True(configuration.GameCfgSource.Write("system", "language", "french"));
			Lock(cfg);
			try
			{
				Assert.ThrowsAny<Exception>(() => configuration.Save());
			}
			finally
			{
				Unlock(cfg);
			}

			Assert.Contains("ExtraCRC=0x1", File.ReadAllLines(ddraw));
			Assert.Contains("language=english", File.ReadAllLines(cfg));
			Assert.True(configuration.HasPendingChanges);
			Assert.Equal(2, Directory.GetFiles(folder.Path).Length);
		}

		[Fact]
		public void AFailedSaveIsReportedAndKeepsTheWindowOpen()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			String cfg = folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();
			TestInteraction interaction = new TestInteraction { SaveAnswer = true };
			MainViewModel viewModel = new MainViewModel(configuration, interaction, () => { }, _ => { }, _ => { });
			viewModel.Load();
			Assert.True(configuration.GameCfgSource.Write("system", "language", "french"));
			Lock(cfg);
			try
			{
				Assert.False(viewModel.ConfirmClose());
			}
			finally
			{
				Unlock(cfg);
			}

			Assert.Single(interaction.Errors);
		}

		[Fact]
		public void TheGameConfigurationKeepsTheBytesOfTheSystemCodePage()
		{
			if (!TextEncodings.Ansi.IsSingleByte)
			{
				return;
			}

			using TestFolder folder = new TestFolder();
			Byte[] accented = [0xC8, 0xE9, 0xF0];
			Byte[] original = [.. "[system]\r\nlanguage=english\r\nmaster_patches="u8.ToArray(), .. accented, .. "\r\n"u8.ToArray()];
			File.WriteAllBytes(folder.Combine("fallout2.cfg"), original);
			folder.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();

			Assert.True(configuration.GameCfgSource.Write("system", "language", "french"));
			configuration.Save();

			Byte[] saved = File.ReadAllBytes(folder.Combine("fallout2.cfg"));
			Assert.True(saved.AsSpan().IndexOf(accented) >= 0, "The accented characters of a path must survive a save.");
		}
	}
}