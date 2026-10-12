namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;
	using Services;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// How the options are written and arranged: the labels, the colors of the palette, the key codes.
	/// </summary>
	public sealed class LayoutOptionsTests
	{
		private static MainViewModel Open(TestFolder folder)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[CombatAI]\r\nTryToFindTargets=1\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=800\r\nSCR_HEIGHT=600\r\n[IFACE]\r\nALTERNATE_AMMO_METRE=2\r\nALTERNATE_AMMO_LIGHT=196\r\nALTERNATE_AMMO_DARK=75\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		[Theory]
		[InlineData("Light color:", "Light color")]
		[InlineData("Game folder :", "Game folder")]
		[InlineData("Ignore scroll limits (IGNORE_SCROLL_LIMITS)", "Ignore scroll limits\n(IGNORE_SCROLL_LIMITS)")]
		[InlineData("Splash time (SPLASH_SCRN_TIME):", "Splash time\n(SPLASH_SCRN_TIME)")]
		[InlineData("Key code (DIK_*):", "Key code (DIK_*)")]
		[InlineData("Use SFALL.DLL", "Use SFALL.DLL")]
		public void ALabelHasNoColonAndTheNameOfTheSettingIsOnItsOwnLine(String text, String expected)
		{
			Assert.Equal(expected, Localizer.FormatLabel(text));
		}

		[Fact]
		public void NoLabelOfTheOptionsEndsWithAColon()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			Assert.All(viewModel.AllOptions, option => Assert.False(option.Label.EndsWith(':') || option.Label.EndsWith(' '), option.RawLabel));
		}

		[Fact]
		public void ThePlainLabelIsOnOneLine()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			Assert.All(viewModel.AllOptions, option => Assert.DoesNotContain('\n', option.PlainLabel));
		}

		[Fact]
		public void ThePaletteIsReadAsSixBitColors()
		{
			Byte[] data = new Byte[768];
			data[3] = 63;
			data[4] = 32;
			data[5] = 0;

			IReadOnlyList<Int32> colors = GamePalette.Parse(data)!;

			Assert.Equal(256, colors.Count);
			Assert.Equal(0x000000, colors[0]);
			Assert.Equal(0xFF8100, colors[1]);
			Assert.Null(GamePalette.Parse(new Byte[10]));
		}

		[Fact]
		public void ThePaletteIsFoundInTheDataFolderOfTheGame()
		{
			using TestFolder folder = CreateGame();
			Byte[] data = new Byte[768];
			data[0] = 63;
			folder.Write("data\\placeholder.txt", "x");
			System.IO.File.WriteAllBytes(folder.Combine("data\\color.pal"), data);
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			IReadOnlyList<Int32>? palette = GamePalette.Load(configuration);

			Assert.Equal(0xFF0000, palette![0]);
		}

		[RealDataFact("SF_CONFIGURATOR_TEST_GAME")]
		public void TheRealGameHasAPalette()
		{
			GameConfiguration configuration = new(Environment.GetEnvironmentVariable("SF_CONFIGURATOR_TEST_GAME")!);
			configuration.Load();
			if (!System.IO.File.Exists(System.IO.Path.Combine(configuration.GamePath, "master.dat")))
			{
				// A game folder without its archive has no palette to find.
				return;
			}


			IReadOnlyList<Int32>? palette = GamePalette.Load(configuration);

			Assert.NotNull(palette);
			Assert.Equal(256, palette.Count);
			Assert.True(palette.Distinct().Count() > 100);
		}

		[Fact]
		public void AGameWithoutPaletteHasNoColorsToChoose()
		{
			using TestFolder folder = CreateGame();
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			Assert.Null(GamePalette.Load(configuration));
		}

		[Fact]
		public void ChoosingAColorOfThePaletteSetsTheNumber()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption light = (NumberOption)viewModel.AllOptions.First(option => option.Id == "lightColor");
			light.PaletteSource = () => Enumerable.Range(0, 256).Select(index => index * 0x010101).ToList();
			light.IsPaletteOpen = true;

			light.PaletteEntries[40].SelectCommand.Execute(null);

			Assert.Equal(40, light.Value);
			Assert.Equal(0x282828, light.ColorRgb);
			Assert.False(light.IsPaletteOpen);
			Assert.Equal("40", viewModel.Configuration.HiRes!.GetValue("IFACE", "ALTERNATE_AMMO_LIGHT"));
		}

		[Fact]
		public void TheTwoColorsAreShownInOneColumnNextToTheMeter()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			OptionPage page = viewModel.HiRes.Pages.First(candidate => candidate.Groups.Any(group => group.Items.OfType<OptionColumn>().Any()));

			OptionGroup group = page.Groups.First(candidate => candidate.Items.OfType<OptionColumn>().Any());
			OptionColumn column = group.Items.OfType<OptionColumn>().Single();

			Assert.Equal(["lightColor", "darkColor"], column.Items.Select(option => option.Id));
			Assert.Equal(2, group.Items.Count);
			Assert.IsType<ChoiceOption>(group.Items[0]);
		}

		[Theory]
		[InlineData(Key.Escape, 1)]
		[InlineData(Key.Up, 200)]
		[InlineData(Key.Down, 208)]
		[InlineData(Key.Space, 57)]
		[InlineData(Key.F1, 59)]
		public void AKeyThatIsPressedGivesItsDirectInputCode(Key key, Int32 expected)
		{
			Assert.Equal(expected, DirectInputScanCodes.FromKey(key, allowModifiers: false));
		}

		[Fact]
		public void TheModifiersAreCodedOnlyWhenTheOptionAcceptsThem()
		{
			Assert.Equal(DirectInputKeys.ModifierControl, DirectInputScanCodes.FromKey(Key.LeftCtrl, allowModifiers: true));
			Assert.Equal(DirectInputKeys.ModifierAlt, DirectInputScanCodes.FromKey(Key.RightAlt, allowModifiers: true));
			Assert.Equal(DirectInputKeys.ModifierShift, DirectInputScanCodes.FromKey(Key.LeftShift, allowModifiers: true));
			Assert.Equal(29, DirectInputScanCodes.FromKey(Key.LeftCtrl, allowModifiers: false));
		}
	}
}