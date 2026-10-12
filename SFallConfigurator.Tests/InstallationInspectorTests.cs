namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The check of the installation: what is found in the game folder, and what is reported as wrong.
	/// </summary>
	public sealed class InstallationInspectorTests
	{
		private static TestFolder CreateGame(Boolean withExe = true, Boolean withSfall = true, String ddraw = "[Main]\r\nHiResMode=0\r\n")
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", ddraw);
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_dat=master.dat\r\n");
			if (withExe)
			{
				folder.Write("fallout2.exe", "exe");
			}

			if (withSfall)
			{
				folder.Write("ddraw.dll", "dll");
			}

			return folder;
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static ReportLine[] Check(TestFolder folder)
		{
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);

			viewModel.CheckInstallationCommand.Execute(null);

			return interaction.Reports.Single().Lines.ToArray();
		}

		[Fact]
		public void AMissingSfallIsAWarning()
		{
			using TestFolder folder = CreateGame(withSfall: false);

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Warning && line.Text.Contains("ddraw.dll", StringComparison.Ordinal));
		}

		[Fact]
		public void AMissingExecutableIsAnError()
		{
			using TestFolder folder = CreateGame(withExe: false);

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Error && line.Text.Contains("fallout2.exe", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void ACompleteInstallationHasNothingWrong()
		{
			using TestFolder folder = CreateGame();
			folder.Write("master.dat", "data");

			ReportLine[] lines = Check(folder);

			Assert.DoesNotContain(lines, line => line.Severity is ReportSeverity.Warning or ReportSeverity.Error);
			Assert.Contains(lines, line => line.Severity == ReportSeverity.Ok && line.Text.Contains("sfall", StringComparison.Ordinal));
		}

		[Fact]
		public void AMissingDataFileIsAWarning()
		{
			using TestFolder folder = CreateGame();

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Warning && line.Text.Contains("master_dat", StringComparison.Ordinal));
		}

		[Fact]
		public void TwoHiResPatchesAreAWarning()
		{
			using TestFolder folder = CreateGame(ddraw: "[Main]\r\nHiResMode=1\r\n");
			folder.Write("f2_res.dll", "dll");

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Warning && line.Text.Contains("f2_res.dll", StringComparison.Ordinal));
		}

		[Fact]
		public void TheHiResPatchOfSfallIsRecognised()
		{
			using TestFolder folder = CreateGame(ddraw: "[Main]\r\nHiResMode=1\r\n");

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Ok && line.Text.Contains("Hi-Res", StringComparison.Ordinal));
		}

		[Fact]
		public void AReadOnlyConfigurationFileIsAnError()
		{
			using TestFolder folder = CreateGame();
			FileInfo ddraw = new(folder.Combine("ddraw.ini")) { IsReadOnly = true };
			try
			{
				ReportLine[] lines = Check(folder);

				Assert.Contains(lines, line => line.Severity == ReportSeverity.Error && line.Text.Contains("ddraw.ini", StringComparison.Ordinal));
			}
			finally
			{
				ddraw.IsReadOnly = false;
			}
		}

		[Fact]
		public void TheModsAreCounted()
		{
			using TestFolder folder = CreateGame();
			Directory.CreateDirectory(Path.Combine(folder.Path, "mods", "first"));
			Directory.CreateDirectory(Path.Combine(folder.Path, "mods", "second"));

			ReportLine[] lines = Check(folder);

			Assert.Contains(lines, line => line.Severity == ReportSeverity.Info && line.Text.Contains('2'));
		}

		[Fact]
		public void TheVersionIsReadFromTheFile()
		{
			String assembly = typeof(InstallationInspectorTests).Assembly.Location;

			String? version = InstallationInspector.GetVersion(assembly);

			Assert.NotNull(version);
			Assert.DoesNotContain(',', version);
			Assert.Null(InstallationInspector.GetVersion(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".dll")));
		}
	}
}