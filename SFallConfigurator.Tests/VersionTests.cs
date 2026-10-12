namespace SFallConfigurator.Tests
{
	using System;
	using System.Diagnostics;
	using Configuration;
	using Infrastructure;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The version of the program, as the window and the executable show it.
	/// </summary>
	public sealed class VersionTests
	{
		[Fact]
		public void TheTitleOfTheWindowShowsTheVersionOfTheProgram()
		{
			using TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\n");
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());

			Assert.StartsWith("SFallConfigurator v20", viewModel.Title, StringComparison.Ordinal);
			Assert.EndsWith(" - by Kain", viewModel.Title, StringComparison.Ordinal);
		}

		[Fact]
		public void EveryAssemblyHasTheSameVersion()
		{
			String viewModels = FileVersionInfo.GetVersionInfo(typeof(MainViewModel).Assembly.Location).FileVersion!;
			String core = FileVersionInfo.GetVersionInfo(typeof(GameConfiguration).Assembly.Location).FileVersion!;
			String program = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "SFallConfigurator.dll")).FileVersion!;

			Assert.Equal(core, viewModels);
			Assert.Equal(core, program);
		}
	}
}