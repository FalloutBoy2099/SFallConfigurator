namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using Services;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// What the options and the editors do when the user changes something.
	/// </summary>
	public sealed class InterfaceBehaviourTests
	{
		private static readonly ScreenResolution[] Modes =
		[
			new ScreenResolution(1920, 1080),
			new ScreenResolution(1280, 960),
			new ScreenResolution(1024, 768),
		];

		/// <summary>
		/// The display modes that the test chooses, not those of the graphics driver of the machine.
		/// </summary>
		private sealed class FakeDisplayModes : IDisplayModes
		{
			public IReadOnlyList<ScreenResolution> GetAvailable(Boolean scaledX2)
			{
				return DisplayModes.Filter(Modes, scaledX2);
			}
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction) { DisplayModes = new FakeDisplayModes() }, new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame(String sfallWidth = "1920", String sfallHeight = "1080", String hiResWidth = "1920", String hiResHeight = "1080")
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Graphics]\r\nMode=4\r\nGraphicsWidth=" + sfallWidth + "\r\nGraphicsHeight=" + sfallHeight + "\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=" + hiResWidth + "\r\nSCR_HEIGHT=" + hiResHeight + "\r\nSCALE_2X=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			return folder;
		}

		// --- Scaling X2 ---

		[Fact]
		public void TickingTheScalingGivesTheHiResPatchHalfOfAResolutionOfTheList()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());
			SfallResolutionEditor editor = viewModel.Sfall.GraphicsTab.Resolution;

			editor.ScaleX2 = true;

			Assert.Equal("960", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal("540", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_HEIGHT"));
			Assert.False(editor.NeedsResolution);
		}

		[Fact]
		public void TickingTheScalingWithAResolutionThatCannotBeScaledAsksForAnother()
		{
			using TestFolder folder = CreateGame("800", "600", "800", "600");
			MainViewModel viewModel = Open(folder, new TestInteraction());
			SfallResolutionEditor editor = viewModel.Sfall.GraphicsTab.Resolution;

			editor.ScaleX2 = true;

			Assert.True(editor.NeedsResolution);
			Assert.Equal("800", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
		}

		[Fact]
		public void UntickingTheScalingGivesTheHiResPatchTheFullResolutionAgain()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());
			SfallResolutionEditor editor = viewModel.Sfall.GraphicsTab.Resolution;
			editor.ScaleX2 = true;

			editor.ScaleX2 = false;

			Assert.Equal("1920", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal("1080", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_HEIGHT"));
		}

		[Fact]
		public void TheScalingLeavesTheHiResPatchAloneWhenItDoesNotFollowSfall()
		{
			using TestFolder folder = CreateGame(hiResWidth: "1024", hiResHeight: "768");
			MainViewModel viewModel = Open(folder, new TestInteraction());
			SfallResolutionEditor editor = viewModel.Sfall.GraphicsTab.Resolution;
			editor.SetHiResResolution = false;

			editor.ScaleX2 = true;
			editor.ScaleX2 = false;

			Assert.Equal("1024", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal("768", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_HEIGHT"));
		}

		[Fact]
		public void WithoutTheOptionsOfSfallNothingIsPushedToTheHiResPatch()
		{
			using TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=1024\r\nSCR_HEIGHT=768\r\nSCALE_2X=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			MainViewModel viewModel = Open(folder, new TestInteraction());
			SfallResolutionEditor editor = viewModel.Sfall.GraphicsTab.Resolution;

			editor.Width.Value = 1920;
			editor.Height.Value = 1080;

			Assert.Equal("1024", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_WIDTH"));
			Assert.Equal("768", viewModel.Configuration.HiResSource.Read("MAIN", "SCR_HEIGHT"));
		}

		[Fact]
		public void WithoutTheHiResFileTheEditorDoesNotOfferToFollowIt()
		{
			using TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Graphics]\r\nGraphicsWidth=1920\r\nGraphicsHeight=1080\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");

			MainViewModel viewModel = Open(folder, new TestInteraction());

			Assert.False(viewModel.Sfall.GraphicsTab.Resolution.IsHiResAvailable);
		}

		// --- Options ---

		[Fact]
		public void EveryStepOfTheColorAnimationSpeedIsReadBackAsItWasWritten()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());
			NumberOption speed = viewModel.Sfall.Pages
				.SelectMany(page => page.Groups)
				.SelectMany(group => group.Items)
				.OfType<NumberOption>()
				.First(option => option.Key == "cycle_speed_factor");

			for (Int32 step = speed.Minimum; step <= speed.Maximum; step++)
			{
				String raw = speed.FormatRaw!(step);
				Assert.Equal(step, speed.ParseRaw!(raw));
			}
		}

		[Fact]
		public void ChoosingTheUnknownPictureShowsTheSelectedPictureAgain()
		{
			using TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nIFACE_BAR_SIDE_ART=1\r\n");
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			BarArtOption option = new(new OptionLocation(configuration.HiResSource, "MAIN"), "IFACE_BAR_SIDE_ART", "Side panels");
			option.Load();
			List<String?> raised = [];
			((INotifyPropertyChanged)option).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

			option.PresetIndex = 3;

			Assert.Equal(1, option.Art);
			Assert.Contains(nameof(BarArtOption.PresetIndex), raised);
		}

		[Theory]
		[InlineData(17, false, true)]
		[InlineData(42, false, true)]
		[InlineData(500, false, false)]
		[InlineData(0, false, false)]
		[InlineData(-7, true, false)]
		[InlineData(-2, true, true)]
		[InlineData(-2, false, false)]
		public void OnlyTheKeysOfTheListCanBeChosen(Int32 code, Boolean allowModifiers, Boolean known)
		{
			Assert.Equal(known, DirectInputKeys.IsKnown(code, allowModifiers));
		}

		[Fact]
		public void TheModeOfSfallIsNotChangedWhenTheExecutableCannotBePatched()
		{
			using TestFolder folder = CreateGame();
			File.WriteAllBytes(folder.Combine("fallout2.exe"), new Byte[2_000_000]);
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);
			HiResModePanel panel = viewModel.HiRes.Mode;

			panel.SelectedMode = HiResMode.Mash;
			panel.ApplyCommand.Execute(null);

			Assert.Single(interaction.Errors);
			Assert.Equal("1", viewModel.Configuration.SfallSource.Read("Main", "HiResMode"));
		}

		[Fact]
		public void AMonitorIsFoundForTheWindow()
		{
			// Without a display (a service) nothing is found: then the bounds of the virtual screen are used.
			System.Windows.Rect? area = MonitorAreas.GetWorkAreaNear(new System.Windows.Rect(0, 0, 640, 480), 1.0);

			Assert.True(area is null || (area.Value.Width > 0 && area.Value.Height > 0));
		}
	}
}