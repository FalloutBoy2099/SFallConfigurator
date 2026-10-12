namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Options;
	using Services;

	/// <summary>
	/// The "System" tab of the sfall settings. The catalog says what the options are; the page says how they are read and written.
	/// </summary>
	internal sealed class SystemPage : OptionPage
	{
		private readonly FileDisplayItem _sfallFile;
		private readonly ToggleItem _useSfallDll;

		public SystemPage(OptionContext context, ICommand browseSfallFileCommand)
			: base(OptionCatalog.Page("system"), context)
		{
			GameConfiguration configuration = context.Configuration;

			BoolOption processorIdle = Catalog.Get<BoolOption>("processorIdle");
			processorIdle.Interpret = raw => IniValue.ToInt32(raw) != -1;

			ChoiceOption skipOpeningMovies = Catalog.Get<ChoiceOption>("skipOpeningMovies");
			skipOpeningMovies.IndexFromRaw = raw => IniValue.ToInt32(raw) & 0x3;

			// The debug mode key holds the log type, or -1 when the debug mode is off.
			ChoiceOption debugLog = Catalog.Get<ChoiceOption>("debugLog");
			debugLog.IndexFromRaw = raw => IniValue.ToInt32(raw) is >= 0 and < 5 ? IniValue.ToInt32(raw) : 0;
			BoolOption debugMode = Catalog.Get<BoolOption>("debugMode");
			debugMode.TrueValueSource = () => IniValue.Format(Math.Max(debugLog.SelectedIndex, 0));
			debugMode.Interpret = raw => IniValue.ToInt32(raw) >= 0;
			debugLog.EnableWhen(() => debugMode.Value == true, debugMode);
			debugMode.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(BoolOption.Value) && debugMode.Value == true && !IsPageLoading)
				{
					configuration.EnsureDebugSection();
				}
			};

			// Files
			_sfallFile = new FileDisplayItem("sFall configuration file (optional):", browseSfallFileCommand)
			{
				ToolTip = "Choose a file.",
			};
			_useSfallDll = new ToggleItem("Use SFALL.DLL", ApplySfallDll)
			{
				ToolTip = "Uses the sfall.dll library instead of the system ddraw.dll.\nNote: ddraw.dll must be renamed to sfall.dll.\n\nAn alternative way of loading the sfall library for operating systems that are not compatible with Windows.\nIt needs no extra steps to override the loading of ddraw.dll on such systems.",
			};

			ExtraCrc = Catalog.Get<TextOption>("extraCrc");
			ExtraCrc.ActionCommand = new RelayCommand(AddChecksumOfChosenFile);

			// Input and language
			ChoiceOption xltKey = Catalog.Get<ChoiceOption>("xltKey");
			ChoiceOption xltTable = Catalog.Get<ChoiceOption>("xltTable");
			xltTable.AvailabilityCondition = () => configuration.Translations.LineCount > 0 && xltKey.IsAvailable;
			xltTable.IndexFromRaw = raw =>
			{
				String[] table = raw.Split(',');
				return table.Length > 1 && table[1] == "157" ? 1 : 0;
			};

			Groups = Catalog.BuildGroups(new Dictionary<String, Object> { ["sfallFile"] = _sfallFile, ["useSfallDll"] = _useSfallDll });
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
			DllPatchState state = Context.Services.FileSystem.FileExists(exePath) ? Context.Services.ExecutablePatcher.GetSfallDllState(exePath) : DllPatchState.Unknown;
			_useSfallDll.Show(state == DllPatchState.Unknown ? null : state == DllPatchState.Patched);
		}

		/// <summary>
		/// Adds the checksum of the game executable to the accepted ones, unless it is there already.
		/// Returns true when the list was extended.
		/// </summary>
		public Boolean RegisterChecksum(String exePath)
		{
			if (!ExtraCrc.IsAvailable || !Context.Services.FileSystem.FileExists(exePath))
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
			if (Context.Services.ExecutablePatcher.SetSfallDll(Context.Configuration.GameExePath, enable))
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