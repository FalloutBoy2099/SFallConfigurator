namespace SFallConfigurator.ViewModels
{
	using Configuration;
	using Options;

	/// <summary>
	/// The "Common" tab of the sfall settings.
	/// </summary>
	internal sealed class CommonPage : OptionPage
	{
		public CommonPage(OptionContext context)
			: base("Common", context)
		{
			GameConfiguration configuration = context.Configuration;
			OptionLocation sfall = context.Sfall;
			OptionLocation mods = context.SfallMods;
			OptionLocation misc = mods.In("Misc");

			// Highlight Mod
			KeyOption highlightKey = Add(new KeyOption(mods.In("Highlighting"), "Key", "Highlights Item Key :", 42, context.Interaction)
			{
				ToolTip = "Highlights the items on the map while the key is held (Shift by default).",
				ReadFallback = () => configuration.SfallSource.Read(null, "ToggleItemHighlightsKey"),
				Written = raw => configuration.SfallSource.Write(null, "ToggleItemHighlightsKey", raw),
			});

			BoolOption highlightContainers = Add(new BoolOption(mods.In("Highlighting"), "Containers", "Highlight Containers")
			{
				ReadFallback = () => configuration.SfallSource.Read(null, "HighlightContainers") ?? configuration.SfallSource.Read(null, "TurnHighlightContainers"),
				Written = raw =>
				{
					configuration.SfallSource.Write(null, "HighlightContainers", raw);
					configuration.SfallSource.Write(null, "TurnHighlightContainers", raw);
				},
			});

			BoolOption highlightCorpses = Add(new BoolOption(mods.In("Highlighting"), "Corpses", "Highlight Corpses")
			{
				TrueValue = "2",
			});

			highlightContainers.EnableWhen(() => highlightKey.IsChecked == true, highlightKey);
			highlightCorpses.EnableWhen(() => highlightKey.IsChecked == true, highlightKey);

			// Quick Save Behavior
			BoolOption extraSaveSlots = Bool(sfall, "ExtraSaveSlots", "Enable extra save slots");
			NumberOption startSavePage = Number(sfall, "AutoQuickSavePage", "Start Save Page:", -1, 999);
			NumberOption quickSavePages = Number(
				sfall,
				"AutoQuickSave",
				"Count Pages/Slots:",
				0,
				999,
				"A value (1..999) sets the number of pages used for quick saves.\n0 to disable.\n\nIf Start Save Page is set to -1, this sets the number of slots per page for quick saves.\n");

			startSavePage.EnableWhen(() => extraSaveSlots.Value != false, extraSaveSlots);
			quickSavePages.EnableWhen(() => extraSaveSlots.Value != false, extraSaveSlots);

			// Auto Save
			BoolOption autoSave = Bool(misc, "AutoSave", "Enable Auto Save Mod");
			NumberOption autoSavePage = Number(misc, "AutoSavePage", "Auto Save Page", 0, 999, "Allowed range: 0...999");
			NumberOption autoSaveCountPages = Number(misc, "AutoSaveCountPages", "Count Save Pages", 1, 100, "Number of pages reserved for automatic saves.");
			NumberOption autoSaveTime = Number(misc, "AutoSaveTime", "Save After Time", 1, 999, "Timeout in game seconds before the next automatic save.");

			foreach (NumberOption option in new[] { autoSavePage, autoSaveCountPages, autoSaveTime })
			{
				option.EnableWhen(() => autoSave.Value == true, autoSave);
			}

			autoSave.PropertyChanged += (_, e) =>
			{
				// The auto save mod stores its saves in the extra quick save pages.
				if (e.PropertyName == nameof(BoolOption.Value) && autoSave.Value == true && !IsPageLoading)
				{
					extraSaveSlots.Value = true;
					if (quickSavePages.Value == 0)
					{
						quickSavePages.Value = 1;
					}
				}
			};

			// General
			BoolOption heroAppearance = Bool(sfall, "EnableHeroAppearanceMod", "Hero Appearance Mod", "Enables the built-in hero appearance mod.");
			BoolOption npcAutoLevel = Bool(sfall, "NPCAutoLevel", "NPC Auto Level");
			BoolOption displayKarma = Bool(sfall, "DisplayKarmaChanges", "Display Karma Changes", "Reports changes of the player's karma in the information window of the game interface.");
			BoolOption dontTurnOffSneak = Bool(sfall, "DontTurnOffSneakIfYouRun", "Don't Turn Off Sneak");
			BoolOption partySkills = Bool(mods.In("PartyMember"), "UsePartySkills", "Use Party Member Skills Mod");
			BoolOption autoOpenDoor = Bool(misc, "AutoOpenDoor", "Auto Open Door", "Automatically opens unlocked doors when walking through them.");
			BoolOption autoCloseDoor = Bool(misc, "AutoCloseDoor", "NPC Auto Close Door", "Automatically closes the doors that NPCs open when they pass through them.");
			BoolOption radIcon = Bool(mods.In("GameFX"), "ShowRadsIcon", "Show RAD Warning Icon");

			ChoiceOption showInfoWindow = Add(new ChoiceOption(
				misc,
				"ShowInfoWindow",
				"Show Information Window",
				["0 - Disable", "1 - Enable (short delay time)", "2 - Enable (long delay time)"])
			{
				IndexFromRaw = raw => (IniValue.ToInt32(raw) is 1 or 2) ? IniValue.ToInt32(raw) : 0,
			});

			NumberOption timeLimit = Number(sfall, "TimeLimit", "Game Years Limit:", -3, 100, "Time limit of the game in years.");

			NumberOption gameSpeed = Add(new NumberOption(sfall, "SpeedMultiInitial", "Game Speed %:", 10, 1000)
			{
				ToolTip = "Minimum 10%, maximum 1000%.",
				Increment = 10,
				AvailabilityCondition = () => configuration.Ddraw.HasSection("Speed"),
				Written = _ => configuration.SfallSource.Write("Speed", "Enable", "1"),
			});

			Groups =
			[
				new OptionGroup("Highlight Mod", highlightKey, highlightContainers, highlightCorpses),
				new OptionGroup("Quick Save Behavior", extraSaveSlots, startSavePage, quickSavePages),
				new OptionGroup("Auto Save", autoSave, autoSavePage, autoSaveCountPages, autoSaveTime),
				new OptionGroup(
					"General",
					heroAppearance,
					npcAutoLevel,
					displayKarma,
					dontTurnOffSneak,
					partySkills,
					autoOpenDoor,
					autoCloseDoor,
					radIcon,
					showInfoWindow,
					timeLimit,
					gameSpeed),
			];
		}
	}
}