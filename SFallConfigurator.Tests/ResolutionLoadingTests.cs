namespace SFallConfigurator.Tests
{
	using System;
	using Configuration;
	using Infrastructure;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// Opening a game must not change anything: reading the options is not an edit.
	/// </summary>
	public sealed class ResolutionLoadingTests : IDisposable
	{
		private readonly TestFolder _settings = new();

		public ResolutionLoadingTests()
		{
			UserSettings.DirectoryOverride = _settings.Path;
		}

		public void Dispose()
		{
			UserSettings.DirectoryOverride = TestEnvironment.SettingsFolder;
			_settings.Dispose();
		}

		[Theory]
		[InlineData("1920", "1080", "960", "540", "1")]
		[InlineData("1920", "1080", "1920", "1080", "0")]
		[InlineData("1600", "900", "640", "480", "0")]
		[InlineData("1920", "1080", "800", "600", "1")]
		public void OpeningAGameDoesNotRewriteTheHiResResolution(String sfallWidth, String sfallHeight, String hiResWidth, String hiResHeight, String scale)
		{
			using TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Graphics]\r\nMode=4\r\nGraphicsWidth=" + sfallWidth + "\r\nGraphicsHeight=" + sfallHeight + "\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=" + hiResWidth + "\r\nSCR_HEIGHT=" + hiResHeight + "\r\nSCALE_2X=" + scale + "\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();

			Assert.False(configuration.HasPendingChanges);
			Assert.Equal(hiResWidth, configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal(hiResHeight, configuration.HiResSource.Read("MAIN", "SCR_HEIGHT"));
			Assert.Equal(scale, configuration.HiResSource.Read("MAIN", "SCALE_2X"));
		}
	}
}