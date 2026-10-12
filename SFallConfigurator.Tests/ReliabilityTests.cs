namespace SFallConfigurator.Tests
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Configuration;
	using Dat;
	using Infrastructure;
	using Options;
	using Services;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The behaviours that keep the game files and the user's changes safe.
	/// </summary>
	public sealed class ReliabilityTests : IDisposable
	{
		private readonly TestFolder _settings = new();

		public ReliabilityTests()
		{
			UserSettings.DirectoryOverride = _settings.Path;
		}

		public void Dispose()
		{
			UserSettings.DirectoryOverride = TestEnvironment.SettingsFolder;
			_settings.Dispose();
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			return folder;
		}

		// --- Saving ---

		[Fact]
		public void TheLoadOrderAndTheConfigurationAreSavedTogetherOrNotAtAll()
		{
			using TestFolder folder = CreateGame();
			String order = folder.Write("mods/mods_order.ini", "first.dat=1\r\n");
			folder.Write("mods/first.dat", "x");
			String cfg = folder.Combine("fallout2.cfg");
			TestInteraction interaction = new()
			{ SaveAnswer = true };
			MainViewModel viewModel = Open(folder, interaction);

			viewModel.Mods.Mods[0].IsEnabled = false;
			Assert.True(viewModel.Configuration.GameCfgSource.Write("system", "language", "french"));
			File.SetAttributes(cfg, FileAttributes.ReadOnly);
			try
			{
				Assert.False(viewModel.ConfirmClose());
			}
			finally
			{
				File.SetAttributes(cfg, FileAttributes.Normal);
			}

			Assert.Equal(["first.dat=1"], File.ReadAllLines(order));
			Assert.Single(interaction.Errors);
			Assert.True(viewModel.Mods.IsModified);
		}

		[Fact]
		public void SavingWritesTheLoadOrderAndTheConfiguration()
		{
			using TestFolder folder = CreateGame();
			String order = folder.Write("mods/mods_order.ini", "first.dat=1\r\n");
			folder.Write("mods/first.dat", "x");
			MainViewModel viewModel = Open(folder, new TestInteraction { SaveAnswer = true });

			viewModel.Mods.Mods[0].IsEnabled = false;
			Assert.True(viewModel.Configuration.GameCfgSource.Write("system", "language", "french"));

			Assert.True(viewModel.ConfirmClose());
			Assert.Equal(["first.dat=0"], File.ReadAllLines(order));
			Assert.Contains("language=french", File.ReadAllLines(folder.Combine("fallout2.cfg")));
			Assert.False(viewModel.Mods.IsModified);
			Assert.False(viewModel.Configuration.HasPendingChanges);
		}

		[Theory]
		[InlineData(true, true)]
		[InlineData(false, true)]
		[InlineData(null, false)]
		public void ClosingAsksWhatToDoWithTheChanges(Boolean? answer, Boolean closes)
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new()
			{ SaveAnswer = answer };
			MainViewModel viewModel = Open(folder, interaction);
			Assert.True(viewModel.Configuration.GameCfgSource.Write("system", "language", "french"));

			Boolean result = viewModel.ConfirmClose();

			Assert.Equal(closes, result);
			Assert.Equal(1, interaction.Questions);
			Boolean saved = File.ReadAllLines(folder.Combine("fallout2.cfg")).Contains("language=french");
			Assert.Equal(answer == true, saved);
		}

		[Fact]
		public void ClosingWithoutChangesAsksNothing()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);

			Assert.True(viewModel.ConfirmClose());
			Assert.Equal(0, interaction.Questions);
		}

		[Fact]
		public void ChoosingTheMainIniAsTheSfallFileLeavesASingleDocument()
		{
			using TestFolder folder = CreateGame();
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			configuration.SetCustomSfallFile(folder.Combine("ddraw.ini"));
			Assert.True(configuration.SfallSource.Write("Main", "ExtraCRC", "0x2"));
			Assert.True(configuration.SfallSource.Write("Graphics", "Mode", "4"));

			Assert.Null(configuration.Sfall);
			configuration.Save();
			String[] lines = File.ReadAllLines(folder.Combine("ddraw.ini"));
			Assert.Contains("ExtraCRC=0x2", lines);
			Assert.Contains("Mode=4", lines);
			Assert.DoesNotContain(Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories), file => file.EndsWith(".tmp", StringComparison.Ordinal) || file.EndsWith(".old", StringComparison.Ordinal));
		}

		[Fact]
		public void TheAmmoFileIsWrittenOnAnActiveLineAndTheCommentedExampleStaysAsItIs()
		{
			using TestFolder folder = CreateGame();
			folder.Write("sfall-mods.ini", "[Misc]\r\n;AmmoFile=example.ini\r\n");
			MainViewModel viewModel = Open(folder, new TestInteraction());
			TextOption ammoFile = viewModel.Sfall.Pages
				.SelectMany(page => page.Groups)
				.SelectMany(group => group.Items)
				.OfType<TextOption>()
				.First(option => option.Key == "AmmoFile");

			ammoFile.Text = "data\\ammo.ini";

			IniDocument mods = viewModel.Configuration.SfallMods!;
			Assert.Equal("data\\ammo.ini", mods.GetValue("Misc", "AmmoFile"));
			Assert.Contains(";AmmoFile=example.ini", mods.Lines);
		}

		// --- Errors ---

		[Fact]
		public void AFileErrorIsShownInsteadOfEndingTheProgram()
		{
			TestInteraction interaction = new();

			Boolean ok = ErrorGuard.Run(interaction, () => throw new IOException("the disk is full"), "Save error");

			Assert.False(ok);
			Assert.Equal(["the disk is full"], interaction.Errors);
		}

		[Fact]
		public void AFailureOfTheProgramItselfIsNotHidden()
		{
			TestInteraction interaction = new();

			Assert.Throws<InvalidOperationException>(() => ErrorGuard.Run(interaction, () => throw new InvalidOperationException("a defect")));
			Assert.Empty(interaction.Errors);
		}

		[Theory]
		[InlineData(typeof(IOException))]
		[InlineData(typeof(UnauthorizedAccessException))]
		[InlineData(typeof(Win32Exception))]
		[InlineData(typeof(InvalidDataException))]
		[InlineData(typeof(DatFormatException))]
		public void TheErrorsOfFilesAndOfTheSystemAreRecognized(Type type)
		{
			Exception exception = type == typeof(DatFormatException)
				? new DatFormatException("damaged")
				: (Exception)Activator.CreateInstance(type)!;

			Assert.True(ErrorGuard.IsFileError(exception));
		}

		[Fact]
		public void TheOrderFileThatCannotBeWrittenLeavesTheListAsItIsOnTheDisk()
		{
			using TestFolder folder = CreateGame();
			folder.Write("mods/first.dat", "x");
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);
			String order = folder.Combine("mods/mods_order.ini");
			Directory.CreateDirectory(order);

			viewModel.Mods.CreateFileCommand.Execute(null);

			Assert.Single(interaction.Errors);
			Assert.False(viewModel.Mods.OrderFileExists);
		}

		// --- Game folder and command line ---

		[Theory]
		[InlineData("C:\\Games\\F2\\", "C:\\Games\\F2")]
		[InlineData("C:\\Games\\F2", "C:\\Games\\F2")]
		[InlineData("C:\\Games\\..\\Games\\F2/", "C:\\Games\\F2")]
		[InlineData("D:\\", "D:\\")]
		public void AGameFolderIsWrittenInOneWayOnly(String path, String expected)
		{
			Assert.Equal(expected, GameLocator.Normalize(path));
		}

		[Fact]
		public void TheSameFolderWithAndWithoutASeparatorIsRememberedOnce()
		{
			using TestFolder folder = CreateGame();

			GameLocator.RememberGamePath(folder.Path + "\\");
			GameLocator.RememberGamePath(folder.Path);

			Assert.Equal([folder.Path], GameLocator.GetRecentFolders());
		}

		[Fact]
		public void TheInstallSwitchIsAnArgumentOfItsOwn()
		{
			using TestFolder folder = new();

			Assert.Null(InstallMode.TryRun(folder.Path, ["--game", "D:\\my-install dir"]));
			Assert.Null(InstallMode.TryRun(folder.Path, ["--game=D:\\my-install dir"]));
			Assert.Null(InstallMode.TryRun(folder.Path, ["--game", "D:\\games -install x"]));
			Assert.NotNull(InstallMode.TryRun(folder.Path, ["-install", "+"]));
			Assert.NotNull(InstallMode.TryRun(folder.Path, ["-INSTALL", "-"]));
		}

		// --- Encodings ---

		[Fact]
		public void TextWithAByteOrderMarkIsUtf8()
		{
			Byte[] content = [0xEF, 0xBB, 0xBF, .. "[A]"u8.ToArray()];

			Assert.Equal(65001, TextEncodings.DetectEncoding(content).CodePage);
			Assert.NotEmpty(TextEncodings.DetectEncoding(content).GetPreamble());
		}

		[Fact]
		public void Utf8TextWithoutAByteOrderMarkIsNotReadAsAnsi()
		{
			Byte[] content = Encoding.UTF8.GetBytes("[A]\r\nname=Игры é\r\n");

			Encoding encoding = TextEncodings.DetectEncoding(content);

			Assert.Equal("[A]\r\nname=Игры é\r\n", encoding.GetString(content));
		}

		[Fact]
		public void PlainAsciiAndAnsiTextAreReadWithTheAnsiCodePage()
		{
			Assert.Same(TextEncodings.Ansi, TextEncodings.DetectEncoding("[A]\r\nk=v\r\n"u8));
			Assert.Same(TextEncodings.Ansi, TextEncodings.DetectEncoding([0x5B, 0x41, 0x5D, 0xE9, 0xE8, 0xFF]));
		}

		// --- Checksums ---

		[Theory]
		[InlineData("0x01234567", true)]
		[InlineData("0X1234567", true)]
		[InlineData("1234567", true)]
		[InlineData("0x1, 0x01234567", true)]
		[InlineData("0x1234568", false)]
		[InlineData("not a number", false)]
		[InlineData("", false)]
		public void ChecksumsAreComparedAsNumbers(String list, Boolean listed)
		{
			Assert.Equal(listed, ExeChecksum.IsListed(0x01234567u, list));
		}

		[Fact]
		public void AChecksumIsAppendedOnlyOnce()
		{
			String list = ExeChecksum.AppendTo(String.Empty, 0x0000ABCDu);

			Assert.Equal("0xABCD", list);
			Assert.True(ExeChecksum.IsListed(0xABCDu, list));
			Assert.Equal("0xABCD, 0x1", ExeChecksum.AppendTo(list, 1u));
		}

		// --- Executable ---

		[Fact]
		public void TheOriginalExecutableIsKeptBeforeItIsPatchedForTheFirstTime()
		{
			using TestFolder folder = new();
			String exe = TestExecutable.Create(folder);
			Byte[] original = File.ReadAllBytes(exe);

			Assert.True(ExePatcher.SetSfallDll(exe, enable: true));
			Assert.True(ExePatcher.SetHiResPatch(exe, enable: true));

			Assert.Equal(original, File.ReadAllBytes(exe + ".bak"));
			Assert.NotEqual(original, File.ReadAllBytes(exe));
		}

		[Fact]
		public void TheCopyOfTheOriginalIsNotReplacedByALaterPatchedState()
		{
			using TestFolder folder = new();
			String exe = TestExecutable.Create(folder);
			Byte[] original = File.ReadAllBytes(exe);
			Assert.True(ExePatcher.SetSfallDll(exe, enable: true));

			Assert.True(ExePatcher.SetSfallDll(exe, enable: false));
			Assert.True(ExePatcher.SetSfallDll(exe, enable: true));

			Assert.Equal(original, File.ReadAllBytes(exe + ".bak"));
		}

		[Fact]
		public void AnExecutableThatCannotBeWrittenIsLeftAsItWas()
		{
			using TestFolder folder = new();
			String exe = TestExecutable.Create(folder);
			Byte[] original = File.ReadAllBytes(exe);
			File.SetAttributes(exe, FileAttributes.ReadOnly);
			try
			{
				Assert.False(ExePatcher.SetHiResPatch(exe, enable: true));
			}
			finally
			{
				File.SetAttributes(exe, FileAttributes.Normal);
			}

			Assert.Equal(original, File.ReadAllBytes(exe));
		}
	}
}