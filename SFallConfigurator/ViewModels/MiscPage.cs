namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Globalization;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Misc" tab of the sfall settings.
	/// </summary>
	internal sealed class MiscPage : OptionPage
	{
		// The color animation speed is stored as a factor (1.0 = normal, 2.0 = twice as slow); the slider counts in 1/20 steps.
		private const Int32 CycleSpeedSteps = 20;

		public MiscPage(OptionContext context)
			: base("Misc", context)
		{
			OptionLocation sfall = context.Sfall;

			BoolOption ghostPerkTweak = Bool(sfall, "GhostPerkTweak", "Ghost Perk Tweak");
			BoolOption heaveHoPerk = Bool(
				context.SfallMods.In("Misc"),
				"HeaveHoPerkTweak",
				"Heave Ho Perk Tweak",
				"Enables the built-in alternative formula for the \"Heave Ho\" perk.\nIt removes the strength limit of the perk and applies the set modifier (+3)\nto the maximum throwing range for each level of the perk.");
			BoolOption smallFrameFix = Bool(sfall, "SmallFrameFix", "Small Frame Perk Fix");
			BoolOption removeCriticalTime = Bool(sfall, "RemoveCriticalTimelimits", "Remove Critical Time Limits");
			BoolOption objCanSeeObj = Bool(
				sfall,
				"ObjCanSeeObj_ShootThru_Fix",
				"NPC See Thru Shoot",
				"Lets NPCs see through objects that can be shot through (Shoot Thru flag).");
			BoolOption activeGeigerMessages = Bool(
				sfall,
				"ActiveGeigerMsgs",
				"Geiger Radiation Message",
				"Shows the messages of an active Geiger counter when radiation is received.");
			BoolOption skipLoadingGameSettings = Add(new BoolOption(sfall, "SkipLoadingGameSettings", "Skip Loading Game Settings")
			{
				TrueValue = "2",
				FalseValue = "1",
				Interpret = raw => raw == "2",
			});

			ChoiceOption motionScanner = Add(new ChoiceOption(
				sfall,
				"MotionScannerFlags",
				"Motion Scanner Mode",
				[
					"0: Fallout default",
					"1: Use on Automap when motion sensor is in pack",
					"2: Doesn't require charges",
					"3: Both 1 and 2 mode",
				])
			{
				ToolTip = "Changes the behavior of the motion sensor item.\n\n0 - Fallout 2 behavior (default).\n1 - Can be used even when the motion sensor is in the inventory.\n2 - The motion sensor does not need charges.\n3 - Both modes 1 and 2.",
				IndexFromRaw = raw => IniValue.ToInt32(raw) & 0x3,
			});

			NumberOption useWalkDistance = Number(sfall, "UseWalkDistance", "Use Walk Distance", 0, 3, "Allowed range: 0..3\nA value of 0 disables this behavior.");
			NumberOption corpseDeleteTime = Number(sfall, "CorpseDeleteTime", "Corpse Delete Time", 0, 13, "Allowed range: 0..13.\n0 stands for 12 hours.");

			// Animation & Effects
			BoolOption fadeMultiplier = Add(new BoolOption(sfall, "FadeMultiplier", "Multiplier Fade X2")
			{
				ToolTip = "Doubles the speed of the screen fade effects.",
				TrueValue = "50",
				FalseValue = "100",
				Interpret = raw => IniValue.ToInt32(raw) == 50,
			});
			BoolOption explosionsEmitLight = Bool(sfall, "ExplosionsEmitLight", "Explosions Light", "Lets explosions and projectiles emit light on the map.");
			BoolOption backgroundDialogFx = Bool(context.SfallMods.In("Misc"), "BackgroundDialogFX", "Background DialogFX");

			NumberOption soundBuffers = Add(new NumberOption(sfall, "NumSoundBuffers", "Number Sound Buffers:", 0, 32)
			{
				ToolTip = "Sets the number of sound effects that can play at the same time in the game.\nMaximum 32, minimum 4. 0 is the default value.",
				Normalize = NormalizeSoundBuffers,
			});
			NumberOption animationsAtOnce = Number(
				sfall,
				"AnimationsAtOnceLimit",
				"Animations Map Limit:",
				0,
				127,
				"Number of animated objects at the same time on one map.\nMinimum 32, maximum 127.");

			NumberOption colorAnimationSpeed = Add(new NumberOption(context.GameCfg.In("system"), "cycle_speed_factor", "Color Animation Speed", 0, CycleSpeedSteps)
			{
				Presentation = NumberPresentation.Slider,
				MinimumLabel = "Fast",
				MaximumLabel = "Slow",
				DefaultValue = "1.0",
				ParseRaw = raw => Math.Clamp((Int32)(CycleSpeedSteps * (IniValue.ToSingle(raw, 1.0f) - 1.0f)), 0, CycleSpeedSteps),
				FormatRaw = steps => (1.0f + (steps / (Single)CycleSpeedSteps)).ToString("F6", CultureInfo.InvariantCulture),
			});

			// Object Lighting
			BoolOption useLightOrientation = Bool(
				sfall,
				"UseLightOrientationFlags",
				"Use Orientation Flags",
				"Takes the orientation flags of the object prototype into account when lighting a map object.\nEnable it if the orientation flags in the Scenery prototypes are set correctly:\nthe light is then projected onto the sprite of the object depending on its direction.");
			BoolOption legacyLighting = Bool(sfall, "LegacySceneryLighting", "Legacy Lighting");
			useLightOrientation.EnableWhen(() => legacyLighting.Value == false, legacyLighting);

			Groups =
			[
				new OptionGroup(
					"Game",
					ghostPerkTweak,
					heaveHoPerk,
					smallFrameFix,
					removeCriticalTime,
					objCanSeeObj,
					activeGeigerMessages,
					skipLoadingGameSettings,
					motionScanner,
					useWalkDistance,
					corpseDeleteTime),
				new OptionGroup("Animation & Effects", fadeMultiplier, explosionsEmitLight, backgroundDialogFx, soundBuffers, animationsAtOnce, colorAnimationSpeed),
				new OptionGroup("Object Lighting", useLightOrientation, legacyLighting),
			];
		}

		/// <summary>
		/// A buffer count between 1 and 3 is invalid: moving up from 0 selects 4, moving down selects 0.
		/// </summary>
		private static Int32 NormalizeSoundBuffers(Int32 newValue, Int32 oldValue)
		{
			if (newValue is > 0 and < 4)
			{
				return newValue > oldValue ? 4 : 0;
			}

			return newValue;
		}
	}
}