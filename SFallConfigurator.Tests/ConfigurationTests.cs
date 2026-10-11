namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Configuration;
	using Mods;
	using Xunit;

	public class ConfigurationTests
	{
		[Fact]
		public void LoadsTheFilesOfAGameFolderAndSavesOnlyWhatChanged()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=800\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			folder.Write("sfall/sfall-mods.ini", "[Misc]\r\nAutoSave=0\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();

			Assert.True(configuration.DdrawExists);
			Assert.Null(configuration.Sfall);
			Assert.NotNull(configuration.HiRes);
			Assert.Equal("0x1", configuration.SfallSource.Read(null, "ExtraCRC"));
			Assert.Equal("800", configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal("english", configuration.GameCfgSource.Read("system", "language"));
			Assert.False(configuration.HasPendingChanges);

			DateTime before = File.GetLastWriteTimeUtc(folder.Combine("ddraw.ini"));
			configuration.Save();
			Assert.Equal(before, File.GetLastWriteTimeUtc(folder.Combine("ddraw.ini")));

			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x1, 0x2"));
			Assert.True(configuration.HasPendingChanges);
			configuration.Save();

			Assert.False(configuration.HasPendingChanges);
			Assert.Contains("ExtraCRC=0x1, 0x2", File.ReadAllText(folder.Combine("ddraw.ini")), StringComparison.Ordinal);
			Assert.Contains("SCR_WIDTH=800", File.ReadAllText(folder.Combine("f2_res.ini")), StringComparison.Ordinal);
		}

		[Fact]
		public void SfallModsIniIsFoundInTheConfiguredFolder()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Misc]\r\nIniConfigFolder=mods/ini\r\n");
			folder.Write("mods/ini/sfall-mods.ini", "[Misc]\r\nAutoSave=1\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();

			Assert.NotNull(configuration.SfallMods);
			Assert.Equal("1", configuration.SfallModsSource.Read("Misc", "AutoSave"));
			Assert.Equal(Path.Combine("mods", "ini", "sfall-mods.ini"), configuration.SfallModsFileName);
		}

		[Fact]
		public void CustomSfallIniTakesPrecedenceOverDdraw()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nMode=1\r\nOnlyHere=a\r\n");
			folder.Write("sfall.ini", "[Main]\r\nMode=5\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();

			Assert.Equal("5", configuration.SfallSource.Read(null, "Mode"));
			Assert.Equal("a", configuration.SfallSource.Read(null, "OnlyHere"));

			configuration.SfallSource.Write(null, "Mode", "6");
			Assert.Equal("6", configuration.Sfall!.GetValue(null, "Mode"));
			Assert.Equal("1", configuration.Ddraw.GetValue(null, "Mode"));
		}

		[Fact]
		public void MakingSfallIniCopiesDdraw()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nMode=1\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();
			configuration.CreateSfallIni();
			configuration.Save();

			Assert.True(File.Exists(folder.Combine("sfall.ini")));
			Assert.Equal("sfall.ini", configuration.SfallFileName);
		}

		[Fact]
		public void SettingsAreSavedInTheSfallFolderAndKeepTheLanguages()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();
			configuration.GameExe = "FALLOUT2HR.EXE";
			configuration.AddLanguage("english");
			configuration.AddLanguage("french");
			configuration.AddLanguage("French");
			configuration.Save();

			GameConfiguration reloaded = new GameConfiguration(folder.Path);
			reloaded.Load();

			Assert.True(File.Exists(folder.Combine("sfall/setting.sfc")));
			Assert.Equal("FALLOUT2HR.EXE", reloaded.GameExe);
			Assert.Equal(["english", "french"], reloaded.Languages);
		}

		[Fact]
		public void DefaultGameExeIsFallout2()
		{
			using TestFolder folder = new TestFolder();
			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();

			Assert.Equal("FALLOUT2.EXE", configuration.GameExe);
			Assert.False(configuration.DdrawExists);
		}

		[Fact]
		public void DebugSectionIsAddedOnlyOnce()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");

			GameConfiguration configuration = new GameConfiguration(folder.Path);
			configuration.Load();
			configuration.EnsureDebugSection();
			configuration.EnsureDebugSection();

			Assert.Equal("[debug]", configuration.GameCfg.Lines[0]);
			Assert.Single(configuration.GameCfg.Lines, line => line == "[debug]");
		}

		[Fact]
		public void MergeCopiesValuesOfExistingKeysOnly()
		{
			using TestFolder folder = new TestFolder();
			String oldIni = folder.Write("old.ini", "[Main]\r\nMode=4\r\nRemoved=1\r\nModifiedIni=3\r\n[Misc]\r\nEmpty=\r\nKeep=9\r\n");
			IniDocument target = new IniDocument("new.ini", ["[Main]", "Mode=0", "NewKey=7", "[Misc]", "Empty=x", "Keep=1"], Encoding.UTF8);

			ConfigMerger.Merge(target, oldIni, carryModifiedIni: true);

			Assert.Equal("4", target.GetValue("Main", "Mode"));
			Assert.Equal("7", target.GetValue("Main", "NewKey"));
			Assert.Null(target.GetValue("Main", "Removed"));
			Assert.Equal("x", target.GetValue("Misc", "Empty"));
			Assert.Equal("9", target.GetValue("Misc", "Keep"));
			Assert.Equal("3", target.GetValue("Main", "ModifiedIni"));
		}

		[Fact]
		public void InstallModeMergesTheBackupsAndDeletesThemOnRequest()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nMode=0\r\n");
			folder.Write("ddraw.ini.bak", "[Main]\r\nMode=5\r\n");
			folder.Write("sfall-mods.ini", "[Misc]\r\nAutoSave=0\r\n");
			folder.Write("sfall-mods.ini.bak", "[Misc]\r\nAutoSave=1\r\n");

			Boolean? merged = InstallMode.TryRun(folder.Path, ["-install +"]);

			Assert.True(merged);
			Assert.Contains("Mode=5", File.ReadAllText(folder.Combine("ddraw.ini")), StringComparison.Ordinal);
			Assert.Contains("AutoSave=1", File.ReadAllText(folder.Combine("sfall-mods.ini")), StringComparison.Ordinal);
			Assert.False(File.Exists(folder.Combine("ddraw.ini.bak")));
			Assert.False(File.Exists(folder.Combine("sfall-mods.ini.bak")));
		}

		[Fact]
		public void InstallModeKeepsTheBackupsWithoutThePlusFlag()
		{
			using TestFolder folder = new TestFolder();
			folder.Write("ddraw.ini", "[Main]\r\nMode=0\r\n");
			folder.Write("ddraw.ini.bak", "[Main]\r\nMode=5\r\n");

			Assert.True(InstallMode.TryRun(folder.Path, ["-install", "-"]));
			Assert.True(File.Exists(folder.Combine("ddraw.ini.bak")));
		}

		[Fact]
		public void InstallModeIsNotTriggeredByOtherArguments()
		{
			using TestFolder folder = new TestFolder();

			Assert.Null(InstallMode.TryRun(folder.Path, []));
			Assert.Null(InstallMode.TryRun(folder.Path, ["--game", "C:\\x"]));
		}

		[Fact]
		public void GameFolderCanBeNamedOnTheCommandLine()
		{
			using TestFolder folder = new TestFolder();

			Assert.Equal(folder.Path, GameLocator.Resolve(["--game", folder.Path]));
			Assert.Equal(folder.Path, GameLocator.Resolve(["--game=" + folder.Path]));
			Assert.Equal(folder.Path, GameLocator.Resolve(["--game=\"" + folder.Path + "\""]));
		}

		[Fact]
		public void KeyNamesAreKnownForTheDirectInputCodes()
		{
			Assert.Equal("N/A", DirectInputKeys.GetDisplayName(0));
			Assert.Equal("Left Ctrl", DirectInputKeys.GetDisplayName(29));
			Assert.Equal("Right Alt", DirectInputKeys.GetDisplayName(184));
			Assert.Equal("250", DirectInputKeys.GetDisplayName(250));
			Assert.Equal("CTRL", DirectInputKeys.GetDisplayName(-1, allowModifiers: true));
			Assert.Equal("-1", DirectInputKeys.GetDisplayName(-1));
		}
	}

	public class ModsManagerTests
	{
		private static TestFolder CreateFolder()
		{
			TestFolder folder = new TestFolder();
			Directory.CreateDirectory(folder.Combine("mods"));
			File.WriteAllBytes(folder.Combine("mods/alpha.dat"), [1]);
			File.WriteAllBytes(folder.Combine("mods/beta.dat"), [1]);
			File.WriteAllBytes(folder.Combine("mods/gamma.dat"), [1]);
			Directory.CreateDirectory(folder.Combine("mods/delta"));
			return folder;
		}

		[Fact]
		public void LoadsTheOrderAndListsTheOtherMods()
		{
			using TestFolder folder = CreateFolder();
			folder.Write("mods/mods_order.ini", "; my order\r\nbeta.dat=1\r\nalpha.dat=0 ; disabled\r\nghost.dat=1\r\n");

			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();

			Assert.Equal(["beta.dat", "alpha.dat", "ghost.dat"], manager.Mods.Select(mod => mod.Name));
			Assert.True(manager.Mods[0].IsEnabled);
			Assert.False(manager.Mods[1].IsEnabled);
			Assert.True(manager.Mods[2].IsMissing);
			Assert.Null(manager.Mods[2].CheckState);
			Assert.Equal(["delta", "gamma.dat"], manager.AvailableMods);
		}

		[Fact]
		public void ReorderingKeepsCommentsInPlace()
		{
			using TestFolder folder = CreateFolder();
			folder.Write("mods/mods_order.ini", "; head\r\nbeta.dat=1\r\n; middle\r\nalpha.dat=0 ; disabled\r\n");

			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();
			manager.Move(0, 1);
			manager.Save();

			Assert.Equal(["; head", "alpha.dat=0 ; disabled", "; middle", "beta.dat=1"], File.ReadAllLines(folder.Combine("mods/mods_order.ini")));
		}

		[Fact]
		public void MissingModKeepsItsStateWhenSaved()
		{
			using TestFolder folder = CreateFolder();
			folder.Write("mods/mods_order.ini", "ghost.dat=0\r\nbeta.dat=1\r\n");

			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();
			manager.Mods[1].IsEnabled = false;
			manager.Save();

			Assert.Equal(["ghost.dat=0", "beta.dat=0"], File.ReadAllLines(folder.Combine("mods/mods_order.ini")));
		}

		[Fact]
		public void AddingAndRemovingModsUpdatesBothListsAndTheFile()
		{
			using TestFolder folder = CreateFolder();
			folder.Write("mods/mods_order.ini", "beta.dat=1\r\nalpha.dat=1\r\n");

			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();

			manager.Add(1, "gamma.dat");
			Assert.Equal(["beta.dat", "gamma.dat", "alpha.dat"], manager.Mods.Select(mod => mod.Name));
			Assert.Equal(["delta"], manager.AvailableMods);

			manager.Remove(0);
			Assert.Equal(["beta.dat", "delta"], manager.AvailableMods);

			manager.Save();
			Assert.Equal(["gamma.dat=1", "alpha.dat=1"], File.ReadAllLines(folder.Combine("mods/mods_order.ini")));
		}

		[Fact]
		public void NothingIsWrittenWithoutChanges()
		{
			using TestFolder folder = CreateFolder();
			String path = folder.Write("mods/mods_order.ini", "beta.dat=1\r\n");
			DateTime before = File.GetLastWriteTimeUtc(path);

			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();
			manager.Save();

			Assert.False(manager.IsModified);
			Assert.Equal(before, File.GetLastWriteTimeUtc(path));
		}

		[Fact]
		public void CreatesTheOrderFileFromTheTextListOrFromTheFoundMods()
		{
			using TestFolder fromText = CreateFolder();
			fromText.Write("mods/mods_order.txt", "gamma.dat\r\n# comment\r\nalpha.dat\r\n");
			ModsManager first = new ModsManager(fromText.Path);
			first.Load();
			Assert.False(first.OrderFileExists);
			first.CreateOrderFile();

			Assert.True(first.OrderFileExists);
			Assert.Equal(["gamma.dat=1", "# comment", "alpha.dat=1"], File.ReadAllLines(fromText.Combine("mods/mods_order.ini")));

			using TestFolder fromScan = CreateFolder();
			ModsManager second = new ModsManager(fromScan.Path);
			second.Load();
			second.CreateOrderFile();

			Assert.Equal(["alpha.dat=1", "beta.dat=1", "delta=1", "gamma.dat=1"], File.ReadAllLines(fromScan.Combine("mods/mods_order.ini")));
			Assert.Empty(second.AvailableMods);
		}

		[Fact]
		public void FirstModCreatesTheOrderFile()
		{
			using TestFolder folder = CreateFolder();
			ModsManager manager = new ModsManager(folder.Path);
			manager.Load();

			manager.Add(0, "alpha.dat");
			manager.Save();

			Assert.Equal(["alpha.dat=1"], File.ReadAllLines(folder.Combine("mods/mods_order.ini")));
		}
	}
}