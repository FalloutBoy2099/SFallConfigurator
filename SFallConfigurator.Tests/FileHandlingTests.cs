namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Configuration;
	using Dat;
	using Infrastructure;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The files of the game are written back as they were read (encoding, byte order mark, line breaks), the files
	/// that a program replaces are never replaced without asking, and the options show what the files hold.
	/// </summary>
	public sealed class FileHandlingTests
	{
		private const String Ddraw = "ddraw.ini";

		private static GameConfiguration Load(TestFolder folder)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			return configuration;
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			MainViewModel viewModel = new(Load(folder), new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame(String ddraw = "[Main]\r\nExtraCRC=0x1\r\n")
		{
			TestFolder folder = new();
			folder.Write(Ddraw, ddraw);
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			return folder;
		}

		// --- Encodings and line breaks ---

		[Fact]
		public void AnIniFileWithoutAByteOrderMarkGetsNoneWhenItIsSaved()
		{
			using TestFolder folder = CreateGame("[Main]\r\n;Игры\r\nExtraCRC=0x1\r\n");
			File.WriteAllBytes(folder.Combine(Ddraw), new UTF8Encoding(false).GetBytes("[Main]\r\n;Игры\r\nExtraCRC=0x1\r\n"));
			GameConfiguration configuration = Load(folder);

			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x2"));
			configuration.Save();

			Byte[] saved = File.ReadAllBytes(folder.Combine(Ddraw));
			Assert.Equal((Byte)'[', saved[0]);
			Assert.Equal(new UTF8Encoding(false).GetBytes("[Main]\r\n;Игры\r\nExtraCRC=0x2\r\n"), saved);
		}

		[Fact]
		public void APlainAsciiIniFileGetsNoByteOrderMarkEither()
		{
			using TestFolder folder = CreateGame();
			GameConfiguration configuration = Load(folder);

			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x2"));
			configuration.Save();

			Assert.Equal((Byte)'[', File.ReadAllBytes(folder.Combine(Ddraw))[0]);
		}

		[Fact]
		public void AnIniFileWithAByteOrderMarkKeepsIt()
		{
			using TestFolder folder = CreateGame();
			File.WriteAllBytes(folder.Combine(Ddraw), [0xEF, 0xBB, 0xBF, .. "[Main]\r\nExtraCRC=0x1\r\n"u8.ToArray()]);
			GameConfiguration configuration = Load(folder);

			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x2"));
			configuration.Save();

			Byte[] saved = File.ReadAllBytes(folder.Combine(Ddraw));
			Assert.Equal([0xEF, 0xBB, 0xBF], saved[..3]);
			Assert.Equal([0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes("[Main]\r\nExtraCRC=0x2\r\n")], saved);
		}

		[Theory]
		[InlineData("[Main]\nExtraCRC=0x1\n")]
		[InlineData("[Main]\nExtraCRC=0x1")]
		[InlineData("[Main]\r\nExtraCRC=0x1")]
		[InlineData("[Main]\r\nExtraCRC=0x1\r\n")]
		public void TheLineBreaksAndTheEndOfTheFileAreKept(String original)
		{
			using TestFolder folder = CreateGame(original);
			GameConfiguration configuration = Load(folder);

			Assert.True(configuration.SfallSource.Write(null, "ExtraCRC", "0x2"));
			configuration.Save();

			Assert.Equal(original.Replace("0x1", "0x2"), File.ReadAllText(folder.Combine(Ddraw)));
		}

		[Fact]
		public void ABytePageIsKeptByteForByte()
		{
			using TestFolder folder = new();
			Encoding latin1 = Encoding.Latin1;
			Byte[] original = [.. "[a]\r\nname=caf"u8.ToArray(), 0xE9, .. "\r\n"u8.ToArray()];
			String path = folder.Combine("a.ini");
			File.WriteAllBytes(path, original);

			IniDocument document = IniDocument.Load(path, latin1, latin1);
			document.MarkModified();
			document.Save();

			Assert.Equal(original, File.ReadAllBytes(path));
		}

		// --- sfall.ini ---

		[Fact]
		public void AnExistingSfallIniIsNeverOverwrittenByANewOne()
		{
			using TestFolder folder = CreateGame();
			folder.Write("sfall.ini", "[Main]\r\nExtraCRC=0x5\r\n");
			// Ddraw.ini is the sfall file: sfall.ini is not the one in use.
			GameConfiguration configuration = Load(folder);
			configuration.SetCustomSfallFile(folder.Combine(Ddraw));

			Assert.True(configuration.SfallIniExists);
			Assert.Throws<IOException>(configuration.CreateSfallIni);
			Assert.Equal(["[Main]", "ExtraCRC=0x5"], File.ReadAllLines(folder.Combine("sfall.ini")));
		}

		[Fact]
		public void TheCommandThatMakesSfallIniIsOfferedOnlyWhenThereIsNone()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);
			Assert.True(viewModel.Sfall.CanMakeSfallIni);

			folder.Write("sfall.ini", "[Main]\r\n");
			viewModel.Configuration.SetCustomSfallFile(folder.Combine(Ddraw));
			viewModel.Sfall.Load();

			Assert.False(viewModel.Sfall.CanMakeSfallIni);
		}

		[Fact]
		public void ANewSfallIniIsTheSfallFileAtTheNextStart()
		{
			using TestFolder folder = CreateGame();
			GameConfiguration configuration = Load(folder);
			configuration.SetCustomSfallFile(folder.Combine(Ddraw));

			configuration.CreateSfallIni();
			configuration.Save();

			GameConfiguration reloaded = Load(folder);
			Assert.NotNull(reloaded.Sfall);
			Assert.Equal(folder.Combine("sfall.ini"), reloaded.Sfall!.Path, ignoreCase: true);
		}

		// --- Changing the file of the options ---

		private static FileDisplayItem SfallFileItem(MainViewModel viewModel)
		{
			return viewModel.Sfall.SystemTab.Groups
				.SelectMany(group => group.Items)
				.OfType<FileDisplayItem>()
				.First(item => item.Label == "sFall configuration file (optional)");
		}

		[Fact]
		public void ChoosingAnotherSfallFileAsksBeforeItThrowsAwayChanges()
		{
			using TestFolder folder = CreateGame();
			folder.Write("sfall.ini", "[Main]\r\nExtraCRC=0x5\r\n");
			String other = folder.Write("other.ini", "[Main]\r\nExtraCRC=0x9\r\n");
			TestInteraction interaction = new() { FileAnswer = other, ConfirmAnswer = false };
			MainViewModel viewModel = Open(folder, interaction);
			Assert.True(viewModel.Configuration.SfallSource.Write(null, "ExtraCRC", "0x6"));

			SfallFileItem(viewModel).ActionCommand.Execute(null);

			Assert.Equal(1, interaction.Questions);
			Assert.Equal(folder.Combine("sfall.ini"), viewModel.Configuration.Sfall!.Path, ignoreCase: true);
			Assert.Equal("0x6", viewModel.Configuration.SfallSource.Read(null, "ExtraCRC"));

			interaction.ConfirmAnswer = true;
			SfallFileItem(viewModel).ActionCommand.Execute(null);

			Assert.Equal(2, interaction.Questions);
			Assert.Equal(other, viewModel.Configuration.Sfall!.Path, ignoreCase: true);
			Assert.Equal("0x9", viewModel.Configuration.SfallSource.Read(null, "ExtraCRC"));
		}

		[Fact]
		public void ChoosingAnotherSfallFileWithoutChangesAsksNothing()
		{
			using TestFolder folder = CreateGame();
			String other = folder.Write("other.ini", "[Main]\r\nExtraCRC=0x9\r\n");
			TestInteraction interaction = new() { FileAnswer = other };
			MainViewModel viewModel = Open(folder, interaction);

			SfallFileItem(viewModel).ActionCommand.Execute(null);

			Assert.Equal(0, interaction.Questions);
			Assert.Equal("0x9", viewModel.Configuration.SfallSource.Read(null, "ExtraCRC"));
		}

		[Fact]
		public void TheOptionsOutsideThePagesShowTheValuesOfTheNewSfallFile()
		{
			using TestFolder folder = CreateGame("[Main]\r\nAutoloadingMods=1\r\n");
			String other = folder.Write("other.ini", "[Main]\r\nAutoloadingMods=0\r\n");
			TestInteraction interaction = new() { FileAnswer = other };
			MainViewModel viewModel = Open(folder, interaction);
			Assert.False(viewModel.Mods.DisableAutoloading.Value);

			SfallFileItem(viewModel).ActionCommand.Execute(null);

			Assert.True(viewModel.Mods.DisableAutoloading.Value);
		}

		[Fact]
		public void TheLoadOrderThatIsBeingEditedSurvivesAChangeOfSfallFile()
		{
			using TestFolder folder = CreateGame();
			folder.Write("mods/mods_order.ini", "first.dat=1\r\n");
			folder.Write("mods/first.dat", "x");
			String other = folder.Write("other.ini", "[Main]\r\nExtraCRC=0x9\r\n");
			TestInteraction interaction = new() { FileAnswer = other };
			MainViewModel viewModel = Open(folder, interaction);
			viewModel.Mods.Mods[0].IsEnabled = false;

			SfallFileItem(viewModel).ActionCommand.Execute(null);

			Assert.False(viewModel.Mods.Mods[0].IsEnabled);
			Assert.True(viewModel.Mods.IsModified);
		}

		[Fact]
		public void AGameConfigurationFileThatCannotBeReadIsNotRemembered()
		{
			using TestFolder folder = CreateGame();
			String locked = folder.Write("locked.cfg", "[system]\r\nlanguage=german\r\n");
			GameConfiguration configuration = Load(folder);

			using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Assert.Throws<IOException>(() => configuration.SetGameCfgFile(locked));
			}

			Assert.True(String.IsNullOrEmpty(configuration.Settings.GetValue("Main", "game_cfg")));
			Assert.Equal(folder.Combine("fallout2.cfg"), configuration.GameCfg.Path, ignoreCase: true);
		}

		// --- Errors ---

		[Theory]
		[InlineData(typeof(ArgumentException))]
		[InlineData(typeof(ArgumentNullException))]
		[InlineData(typeof(OverflowException))]
		[InlineData(typeof(OutOfMemoryException))]
		[InlineData(typeof(NullReferenceException))]
		[InlineData(typeof(InvalidOperationException))]
		public void TheDefectsOfTheProgramAreNotTakenForErrorsOfFiles(Type type)
		{
			Assert.False(ErrorGuard.IsFileError((Exception)Activator.CreateInstance(type)!));
		}

		[Fact]
		public void AnUnexpectedErrorIsKeptWithItsDetailsInTheLog()
		{
			using TestFolder folder = new();
			String previous = UserSettings.DirectoryOverride!;
			UserSettings.DirectoryOverride = folder.Path;
			try
			{
				try
				{
					throw new InvalidOperationException("the reason of the failure");
				}
				catch (InvalidOperationException exception)
				{
					ErrorLog.Write(exception);
				}
			}
			finally
			{
				UserSettings.DirectoryOverride = previous;
			}

			String log = File.ReadAllText(folder.Combine("error.log"));
			Assert.Contains("the reason of the failure", log, StringComparison.Ordinal);
			Assert.Contains(nameof(AnUnexpectedErrorIsKeptWithItsDetailsInTheLog), log, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("-install+")]
		[InlineData("-install-")]
		[InlineData("-INSTALL+")]
		public void TheInstallSwitchAndItsFlagCanBeOneArgument(String argument)
		{
			using TestFolder folder = new();

			Assert.NotNull(InstallMode.TryRun(folder.Path, [argument]));
		}

		[Theory]
		[InlineData("-installer")]
		[InlineData("-installed")]
		public void AnotherSwitchThatStartsTheSameWayIsNotTheInstallSwitch(String argument)
		{
			using TestFolder folder = new();

			Assert.Null(InstallMode.TryRun(folder.Path, [argument]));
		}

		// --- Descriptions of the archives ---

		private static String CreateDat(TestFolder folder, String name, String? description, Int32 descriptionSize = 0)
		{
			String path = folder.Combine(name);
			if (description is null)
			{
				DatArchiveTests.WriteDat(path, ("art\\a.frm", [1, 2, 3], false));
			}
			else
			{
				Byte[] content = descriptionSize > 0
					? new Byte[descriptionSize]
					: Encoding.UTF8.GetBytes(name + "\r\n" + description);
				DatArchiveTests.WriteDat(path, ("desc.id", content, false));
			}

			return path;
		}

		[Fact]
		public void AnEmptyDescriptionIsNoDescription()
		{
			using TestFolder folder = new();
			String path = CreateDat(folder, "a.dat", "Hello");
			Assert.Equal("Hello", DatDescription.Read(path));

			DatDescription.Write(path, String.Empty);

			Assert.Null(DatDescription.Read(path));
		}

		[Fact]
		public void ADescriptionFileThatIsFarLargerThanALineIsRefused()
		{
			using TestFolder folder = new();
			String path = CreateDat(folder, "big.dat", "ignored", descriptionSize: 1_500_000);

			Assert.Throws<DatFormatException>(() => DatDescription.Read(path));
		}

		[Fact]
		public async Task TheBoxOfADescriptionStartsEmptyWhenThereIsNoneAndAnEmptyAnswerRemovesIt()
		{
			using TestFolder folder = new();
			String without = CreateDat(folder, "without.dat", null);
			String with = CreateDat(folder, "with.dat", "Old text");
			TestInteraction interaction = new() { PromptAnswer = "New text" };
			DatInfoViewModel viewModel = new(folder.Path, interaction);
			await viewModel.RefreshAsync();

			DatFileInfo withoutInfo = viewModel.Files.Single(file => file.FullPath == without);
			Assert.False(withoutInfo.HasDescription);
			viewModel.Selected = withoutInfo;
			viewModel.EditDescriptionCommand.Execute(null);

			Assert.Equal([String.Empty], interaction.PromptInitialTexts);
			Assert.Equal("New text", DatDescription.Read(without));
			Assert.True(withoutInfo.HasDescription);

			DatFileInfo withInfo = viewModel.Files.Single(file => file.FullPath == with);
			viewModel.Selected = withInfo;
			interaction.PromptAnswer = String.Empty;
			viewModel.EditDescriptionCommand.Execute(null);

			Assert.Equal("Old text", interaction.PromptInitialTexts[1]);
			Assert.Null(DatDescription.Read(with));
			Assert.False(withInfo.HasDescription);
		}

		[Fact]
		public async Task ACancelledPromptChangesNothing()
		{
			using TestFolder folder = new();
			String path = CreateDat(folder, "a.dat", "Old text");
			TestInteraction interaction = new() { PromptAnswer = null };
			DatInfoViewModel viewModel = new(folder.Path, interaction);
			await viewModel.RefreshAsync();
			viewModel.Selected = viewModel.Files.Single();

			viewModel.EditDescriptionCommand.Execute(null);

			Assert.Equal("Old text", DatDescription.Read(path));
		}

		[Fact]
		public async Task AFolderThatCannotBeListedGivesAnEmptyList()
		{
			TestInteraction interaction = new();
			DatInfoViewModel viewModel = new(Path.Combine(Path.GetTempPath(), "SFallConfiguratorTests", "does-not-exist-" + Guid.NewGuid().ToString("N")), interaction);

			await viewModel.RefreshAsync();

			Assert.Empty(viewModel.Files);
		}
	}
}