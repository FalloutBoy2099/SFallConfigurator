namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Globalization;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Graphics" tab of the Hi-Res patch settings.
	/// </summary>
	internal sealed class HiResGraphicsPage : OptionPage
	{
		public HiResGraphicsPage(OptionContext context)
			: base("Graphics", context)
		{
			OptionLocation main = context.HiRes.In("MAIN");

			ChoiceOption graphicsMode = Add(new ChoiceOption(
				main,
				"GRAPHICS_MODE",
				"Graphic mode:",
				["0 - Basic (obsolete DirectDraw)", "1 - DirectDraw (DirectX 7)", "2 - Direct3D (DirectX 9)"])
			{
				DefaultValue = "2",
			});

			WindowModeOption windowMode = Add(new WindowModeOption(main, "Window Mode")
			{
				ToolTip = "Full Screen - the game runs full screen.\nWindowed - the game runs in a window.",
			});

			NumberOption width = Add(new NumberOption(main, "SCR_WIDTH", "Width", 640, 10000) { Increment = 10, DefaultValue = "640", ToolTip = "Width" });
			NumberOption height = Add(new NumberOption(main, "SCR_HEIGHT", "Height", 480, 10000) { Increment = 10, DefaultValue = "480", ToolTip = "Height" });
			BoolOption scaleX2 = Add(new BoolOption(main, "SCALE_2X", "Scale X2 Mode") { DefaultValue = "0" });

			Resolution = new HiResResolutionEditor(width, height, scaleX2);

			Groups =
			[
				new OptionGroup(null, graphicsMode, windowMode),
				new OptionGroup("Custom Screen Resolution", Resolution),
			];
		}

		public HiResResolutionEditor Resolution { get; }

		public override void Load()
		{
			base.Load();
			Resolution.Refresh();
		}
	}

	/// <summary>
	/// The "Interface" tab of the Hi-Res patch settings.
	/// </summary>
	internal sealed class HiResInterfacePage : OptionPage
	{
		public HiResInterfacePage(OptionContext context)
			: base("Interface", context)
		{
			OptionLocation iface = context.HiRes.In("IFACE");

			BoolOption barMode = Add(new BoolOption(iface, "IFACE_BAR_MODE", "IFACE_BAR_MODE")
			{
				DefaultValue = "0",
				ToolTip = "Extends the lower edge of the map view down to the bottom of the game window;\nthe interface bar is then drawn over the map, without side panels at the bottom.\nThe bar can be hidden by holding the key assigned to the HideInterfaceKey option (file sfall-mods.ini).\n",
			});
			NumberOption barWidth = Add(new NumberOption(iface, "IFACE_BAR_WIDTH", "IFACE_BAR_WIDTH:", 640, 10000) { Increment = 10, DefaultValue = "640" });

			BarArtOption sideArt = Add(new BarArtOption(iface, "IFACE_BAR_SIDE_ART", "IFACE_BAR_SIDE_ART") { ToolTip = "Picture of the side panels." });
			BoolOption sidesOrientation = Add(new BoolOption(iface, "IFACE_BAR_SIDES_ORI", "IFACE_BAR_SIDES_ORI") { DefaultValue = "0" });
			sideArt.EnableWhen(() => barMode.Value != true, barMode);
			sidesOrientation.EnableWhen(() => barMode.Value != true, barMode);

			ChoiceOption ammoMeter = Add(new ChoiceOption(
				iface,
				"ALTERNATE_AMMO_METRE",
				"ALTERNATE_AMMO_METRE",
				["Disabled", "Single colour", "Changes colour", "Divides colour"])
			{
				ToolTip = "Type of the alternative ammo meter.\n\nDisabled - the standard one.\nDivides colour - splits the meter into several colored sections.",
				Presentation = ChoicePresentation.RadioButtons,
				DefaultValue = "0",
			});
			NumberOption lightColor = Add(new NumberOption(iface, "ALTERNATE_AMMO_LIGHT", "Light color:", 0, 255) { DefaultValue = "196" });
			NumberOption darkColor = Add(new NumberOption(iface, "ALTERNATE_AMMO_DARK", "Dark color:", 0, 255) { DefaultValue = "75" });

			Groups =
			[
				new OptionGroup("Interface bar", barMode, barWidth),
				new OptionGroup("Side panels", sideArt, sidesOrientation),
				new OptionGroup("Alternate ammo meter", ammoMeter, lightColor, darkColor),
			];
		}
	}

	/// <summary>
	/// The "Screens" tab of the Hi-Res patch settings.
	/// </summary>
	internal sealed class HiResScreensPage : OptionPage
	{
		private static readonly String[] ScreenSizes =
		[
			"0 - Original size",
			"1 - Stretch to fit screen (aspect ratio)",
			"2 - Stretch to fill screen",
		];

		public HiResScreensPage(OptionContext context)
			: base("Screens", context)
		{
			OptionLocation hiRes = context.HiRes;
			OptionLocation staticScreens = hiRes.In("STATIC_SCREENS");
			OptionLocation mainMenu = hiRes.In("MAINMENU");
			OptionLocation other = hiRes.In("OTHER_SETTINGS");

			ChoiceOption splash = ScreenSize(staticScreens, "SPLASH_SCRN_SIZE", "Splash Screen Size:", "1");
			ChoiceOption death = ScreenSize(staticScreens, "DEATH_SCRN_SIZE", "Death Screen Size:", "1");
			ChoiceOption endSlides = ScreenSize(staticScreens, "END_SLIDE_SIZE", "End slides size:", "1");
			ChoiceOption help = ScreenSize(staticScreens, "HELP_SCRN_SIZE", "Help Screen Size:", "0");
			ChoiceOption movies = ScreenSize(hiRes.In("MOVIES"), "MOVIE_SIZE", "Movies Size:", "1");
			ChoiceOption mainMenuSize = ScreenSize(mainMenu, "MAIN_MENU_SIZE", "Mainmenu Screen Size:", "1");

			NumberOption offsetX = Add(new NumberOption(mainMenu, "MENU_BG_OFFSET_X", "Menu Background Offset X:", -1000, 10000) { DefaultValue = "-14" });
			NumberOption offsetY = Add(new NumberOption(mainMenu, "MENU_BG_OFFSET_Y", "Menu Background Offset Y:", -1000, 10000) { DefaultValue = "-4" });
			BoolOption useHiResImages = Add(new BoolOption(mainMenu, "USE_HIRES_IMAGES", "USE_HIRES_IMAGES") { DefaultValue = "1" });
			BoolOption scaleButtons = Add(new BoolOption(mainMenu, "SCALE_BUTTONS_AND_TEXT_MENU", "SCALE_BUTTONS") { DefaultValue = "0" });

			BoolOption dialogBackground = Add(new BoolOption(other, "DIALOG_SCRN_BACKGROUND", "DIALOG_SCRN_BACKGROUND")
			{
				DefaultValue = "0",
				ToolTip = "The map and the player's interface bar are hidden during a dialogue with an NPC.",
			});
			NumberOption splashTime = Add(new NumberOption(other, "SPLASH_SCRN_TIME", "SPLASH_SCRN_TIME:", 0, 10)
			{
				DefaultValue = "1",
				ToolTip = "Increases the display time of the start picture, in seconds.",
			});

			Groups =
			[
				new OptionGroup("Screen sizes", splash, movies, death, endSlides, help, mainMenuSize),
				new OptionGroup("Main menu", useHiResImages, scaleButtons, offsetX, offsetY),
				new OptionGroup("Other", dialogBackground, splashTime),
			];
		}

		private ChoiceOption ScreenSize(OptionLocation location, String key, String label, String defaultValue)
		{
			return Add(new ChoiceOption(location, key, label, ScreenSizes) { DefaultValue = defaultValue });
		}
	}

	/// <summary>
	/// The "Map" tab of the Hi-Res patch settings.
	/// </summary>
	internal sealed class HiResMapPage : OptionPage
	{
		private const Int32 NodesPerStep = 2000;

		public HiResMapPage(OptionContext context)
			: base("Map", context)
		{
			OptionLocation maps = context.HiRes.In("MAPS");

			NumberOption pathNodes = Add(new NumberOption(maps, "NumPathNodes", "Number Path Nodes:", 1, 20)
			{
				Presentation = NumberPresentation.Slider,
				DefaultValue = "1",
				DisplayText = steps => (steps * NodesPerStep).ToString(CultureInfo.InvariantCulture),
			});

			BoolOption ignorePlayerScrollLimits = Add(new BoolOption(maps, "IGNORE_PLAYER_SCROLL_LIMITS", "IGNORE_PLAYER_SCROLL_LIMITS")
			{
				DefaultValue = "0",
				ToolTip = "Ignores the map scrolling limits around the player.",
			});
			BoolOption edgeClipping = Add(new BoolOption(maps, "EDGE_CLIPPING_ON", "EDGE_CLIPPING_ON") { DefaultValue = "1" });
			BoolOption vanillaMapBlockers = Add(new BoolOption(maps, "IGNORE_MAP_EDGES", "VANILLA_MAP_BLOCKERS")
			{
				DefaultValue = "2",
				TrueValue = "2",
				Interpret = raw => IniValue.ToInt32(raw) == 2,
			});
			BoolOption fogOfWar = Add(new BoolOption(maps, "FOG_OF_WAR", "FOG_OF_WAR")
			{
				DefaultValue = "0",
				ToolTip = "Enables the fog of war mod.",
			});

			Groups =
			[
				new OptionGroup("Path finding", pathNodes),
				new OptionGroup("Map scrolling", ignorePlayerScrollLimits, edgeClipping, vanillaMapBlockers, fogOfWar),
			];
		}
	}

	/// <summary>
	/// The "Misc" tab of the Hi-Res patch settings.
	/// </summary>
	internal sealed class HiResMiscPage : OptionPage
	{
		public HiResMiscPage(OptionContext context)
			: base("Misc", context)
		{
			OptionLocation other = context.HiRes.In("OTHER_SETTINGS");
			OptionLocation main = context.HiRes.In("MAIN");
			String gamePath = context.Configuration.GamePath;

			BoolOption recalculateFade = Add(new BoolOption(other, "FADE_TIME_RECALCULATE_ON_FADE", "FADE_TIME_RECALCULATE_ON_FADE") { DefaultValue = "0" });
			BoolOption cpuUsageFix = Add(new BoolOption(other, "CPU_USAGE_FIX", "CPU_USAGE_FIX")
			{
				DefaultValue = "0",
				ToolTip = "Reduces the processor load by letting the system idle.",
			});
			BoolOption extraWinMessageChecks = Add(new BoolOption(context.HiRes.In("INPUT"), "EXTRA_WIN_MSG_CHECKS", "EXTRA_WIN_MSG_CHECKS") { DefaultValue = "0" });
			BoolOption dialogArtFix = Add(new BoolOption(other, "DIALOG_SCRN_ART_FIX", "DIALOG_SCRN_ART_FIX")
			{
				DefaultValue = "1",
				ToolTip = "Uses the dialogue window interface picture made by Continuum.",
			});

			PathOption resourceData = Add(new PathOption(main, "f2_res_dat", "f2_res_dat:", PathKind.File, gamePath, context.Interaction)
			{
				DefaultValue = "f2_res.dat",
				FileFilter = "Dat files|*.dat",
				Normalize = text => text.ToLowerInvariant(),
				ToolTip = "Path to the resource file of the Hi-Res patch.",
			});
			PathOption resourcePatches = Add(new PathOption(main, "f2_res_patches", "f2_res_patches:", PathKind.Folder, gamePath, context.Interaction)
			{
				DefaultValue = "data",
				Normalize = text => text.ToLowerInvariant(),
				ToolTip = "Path to the patch folder.",
			});

			Groups =
			[
				new OptionGroup("Options", recalculateFade, cpuUsageFix, extraWinMessageChecks, dialogArtFix),
				new OptionGroup("Resource Data", resourceData, resourcePatches),
			];
		}
	}
}