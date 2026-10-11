namespace SFallConfigurator.ViewModels
{
	using System;
	using System.IO;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Options;
	using Services;

	/// <summary>
	/// The "System" tab of the sfall settings.
	/// </summary>
	internal sealed class SystemPage : OptionPage
	{
		// Character translation tables for the Russian input (Windows-1251 and OEM-866).
		private const String WindowsTable = ",221,,,,,253,,,,,225,,254,,,,,,,,,,,,198,230,193,,222,,,212,200,209,194,211,192,207,208,216,206,203,196,220,210,217,199,201,202,219,197,195,204,214,215,205,223,245,,250,,,,244,232,241,226,243,224,239,240,248,238,235,228,252,242,249,231,233,234,251,229,227,236,246,247,237,255,213,,218";
		private const String DosTable = ",157,,,,,237,,,,,161,,238,,,,,,,,,,,,134,166,129,,158,,,148,136,145,130,147,128,143,144,152,142,139,132,156,146,153,135,137,138,155,133,131,140,150,151,141,159,229,,234,,,,228,168,225,162,227,160,175,224,232,174,171,164,236,226,233,167,169,170,235,165,163,172,230,231,173,239,240,,154";

		private readonly FileDisplayItem _sfallFile;
		private readonly ToggleItem _useSfallDll;

		public SystemPage(OptionContext context, ICommand browseSfallFileCommand)
			: base("System", context)
		{
			GameConfiguration configuration = context.Configuration;
			OptionLocation sfall = context.Sfall;

			BoolOption processorIdle = Add(new BoolOption(sfall, "ProcessorIdle", "Processor Idle")
			{
				ToolTip = "Enable it to prevent a CPU usage of 100%.",
				FalseValue = "-1",
				Interpret = raw => IniValue.ToInt32(raw) != -1,
			});
			BoolOption singleCore = Bool(sfall, "SingleCore", "Use Single Core", "Does not use several processor cores, even when they are available.");
			BoolOption overrideArtCache = Bool(sfall, "OverrideArtCacheSize", "Override Art Cache Size", "Overrides the art_cache_size parameter of fallout2.cfg with the value 256.");
			ChoiceOption skipOpeningMovies = Add(new ChoiceOption(
				sfall,
				"SkipOpeningMovies",
				"Skip Movies and Splash",
				["0 - None", "1 - Movies", "2 - Splash", "3 - Movies and Splash"])
			{
				ToolTip = "Skips the intro movies and the splash screens when the game starts.",
				IndexFromRaw = raw => IniValue.ToInt32(raw) & 0x3,
			});

			// The debug mode key holds the log type, or -1 when the debug mode is off.
			ChoiceOption debugLog = Add(new ChoiceOption(
				sfall,
				"DebugMode",
				"Debug Log Output",
				[
					"0 - Output to sfall log file",
					"1 - Output to game window",
					"2 - Output to debug.log file",
					"3 - Combo 1 and 2 mode",
					"4 - Output to console window",
				])
			{
				ToolTip = "Mode of the debug log output.",
				IndexFromRaw = raw => IniValue.ToInt32(raw) is >= 0 and < 5 ? IniValue.ToInt32(raw) : 0,
			});
			BoolOption debugMode = Add(new BoolOption(sfall, "DebugMode", "Game Debug Mode")
			{
				ToolTip = "Enables the debug mode.",
				FalseValue = "-1",
				TrueValueSource = () => IniValue.Format(Math.Max(debugLog.SelectedIndex, 0)),
				Interpret = raw => IniValue.ToInt32(raw) >= 0,
			});
			debugLog.EnableWhen(() => debugMode.Value == true, debugMode);
			debugMode.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(BoolOption.Value) && debugMode.Value == true && !IsPageLoading)
				{
					configuration.EnsureDebugSection();
				}
			};

			BoolOption sfallDebugging = Bool(sfall.In("Debugging"), "Enable", "sFall Debugging");
			BoolOption allowUnsafeScripting = Bool(sfall, "AllowUnsafeScripting", "Allow Unsafe Scripting");

			// Files
			_sfallFile = new FileDisplayItem("sFall configuration file (optional):", browseSfallFileCommand)
			{
				ToolTip = "Choose a file.",
			};
			_useSfallDll = new ToggleItem("Use SFALL.DLL", ApplySfallDll)
			{
				ToolTip = "Uses the sfall.dll library instead of the system ddraw.dll.\nNote: ddraw.dll must be renamed to sfall.dll.\n\nAn alternative way of loading the sfall library for operating systems that are not compatible with Windows.\nIt needs no extra steps to override the loading of ddraw.dll on such systems.",
			};

			ExtraCrc = Add(new TextOption(sfall, "ExtraCRC", "Extra CRC:")
			{
				ActionLabel = "Get CRC",
				ActionToolTip = "Gets the CRC value of a file.",
				ActionCommand = new RelayCommand(AddChecksumOfChosenFile),
			});

			// Input and language
			BoolOption useScrollWheel = Bool(sfall, "UseScrollWheel", "Use Scroll Wheel", "Enables the mouse wheel to scroll the item lists in the inventory.");
			BoolOption reverseMouseButtons = Bool(sfall, "ReverseMouseButtons", "Reverse Mouse Buttons", "Reverses the mouse buttons (from left to right).");
			BoolOption backgroundMouse = Bool(sfall, "BackgroundMouse", "Background Mouse");
			NumberOption mouseSensitivity = Number(sfall, "MouseSensitivity", "Mouse Sensitivity %", -1000, 1000, increment: 5);

			ChoiceOption xltKey = Add(new ChoiceOption(
				sfall,
				"XltKey",
				"Switch Language Key",
				["None (Disable)", "Window system", "Num Lock", "Caps Lock", "Scroll Lock"])
			{
				ToolTip = "Key that switches the keyboard layout (English/Russian) when typing.",
				Values = ["0", "-1", "1", "2", "3"],
			});
			ChoiceOption xltTable = Add(new ChoiceOption(
				context.Translations.In("sfall"),
				"XltTable",
				"Encoding:",
				["Win   [Windows-1251]", "Dos   [OEM-866]"])
			{
				ToolTip = "Encoding of the Cyrillic characters, depending on the font of the game.",
				Values = [WindowsTable, DosTable],
				AvailabilityCondition = () => configuration.Translations.LineCount > 0 && xltKey.IsAvailable,
				IndexFromRaw = raw =>
				{
					String[] table = raw.Split(',');
					return table.Length > 1 && table[1] == "157" ? 1 : 0;
				},
			});
			KeyOption windowMoveKey = Add(new KeyOption(sfall, "WindowScrollKey", "Window Move Key :", 0, context.Interaction)
			{
				HasToggle = false,
				AllowModifiers = true,
			});

			Groups =
			[
				new OptionGroup(
					"Performance",
					processorIdle,
					singleCore,
					overrideArtCache,
					skipOpeningMovies),
				new OptionGroup("Debugging", debugMode, debugLog, sfallDebugging, allowUnsafeScripting),
				new OptionGroup("Files", _sfallFile, _useSfallDll, ExtraCrc),
				new OptionGroup("Input and Language", useScrollWheel, reverseMouseButtons, backgroundMouse, mouseSensitivity, xltKey, xltTable, windowMoveKey),
			];
		}

		/// <summary>
		/// The checksums that sfall accepts besides the one of the original executable.
		/// </summary>
		public TextOption ExtraCrc { get; }

		public override void Load()
		{
			base.Load();
			RefreshFiles();
		}

		/// <summary>
		/// Shows the sfall configuration file and the state of the executable. Call it when either changed.
		/// </summary>
		public void RefreshFiles()
		{
			_sfallFile.Text = Context.Configuration.SfallFileName ?? String.Empty;

			String exePath = Context.Configuration.GameExePath;
			DllPatchState state = File.Exists(exePath) ? ExePatcher.GetSfallDllState(exePath) : DllPatchState.Unknown;
			_useSfallDll.Show(state == DllPatchState.Unknown ? null : state == DllPatchState.Patched);
		}

		/// <summary>
		/// Adds the checksum of the game executable to the accepted ones, unless it is there already.
		/// Returns true when the list was extended.
		/// </summary>
		public Boolean RegisterChecksum(String exePath)
		{
			if (!ExtraCrc.IsAvailable || !File.Exists(exePath))
			{
				return false;
			}

			// The file is read once.
			UInt32 checksum = ExeChecksum.Compute(exePath);
			if (ExeChecksum.IsListed(checksum, ExtraCrc.Text))
			{
				return false;
			}

			ExtraCrc.Text = ExeChecksum.AppendTo(ExtraCrc.Text, checksum);
			return true;
		}

		private Boolean ApplySfallDll(Boolean enable)
		{
			if (ExePatcher.SetSfallDll(Context.Configuration.GameExePath, enable))
			{
				return true;
			}

			Context.Interaction.ShowError("The selected game executable could not be patched. Select Fallout2.exe in the settings and try again.");
			return false;
		}

		private void AddChecksumOfChosenFile()
		{
			String? path = Context.Interaction.PickFile("exe files|*.exe", Context.Configuration.GamePath, "FALLOUT2.EXE");
			if (path is null)
			{
				return;
			}

			Boolean added = false;
			if (ErrorGuard.Run(Context.Interaction, () => added = RegisterChecksum(path)) && !added)
			{
				Context.Interaction.ShowMessage("The checksum of this file is already in the list.", "Extra CRC");
			}
		}
	}
}