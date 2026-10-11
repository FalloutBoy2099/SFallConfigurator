namespace SFallConfigurator.ViewModels
{
	using System;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Interface" tab of the sfall settings.
	/// </summary>
	internal sealed class InterfacePage : OptionPage
	{
		public InterfacePage(OptionContext context)
			: base("Interface", context)
		{
			OptionLocation sfall = context.Sfall;

			BoolOption bonusDamage = Bool(sfall, "DisplayBonusDamage", "Display Bonus Damage");
			BoolOption electricalResist = Bool(
				sfall,
				"DisplayElectricalResist",
				"Display Electrical Resist",
				"Additionally shows the electrical resistance of the armor in the player's inventory.");
			BoolOption cityReputation = Bool(sfall, "DisplayCityReputation", "Display City Reputation");
			BoolOption partyExtraInfo = Bool(sfall, "PartyMemberExtraInfo", "Party Member Extra Info");
			BoolOption numbersInDialogue = Bool(sfall, "NumbersInDialogue", "Numbers in Dialogue", "Shows numbers next to the player's replies in dialogues.");
			BoolOption musicInDialogue = Bool(sfall, "EnableMusicInDialogue", "Music in Dialogue");
			BoolOption actionPointsBar = Bool(sfall, "ActionPointsBar", "Expand Action Points Bar");
			BoolOption expandInventory = Add(new BoolOption(sfall, "ExpandInventory", "Expand Inventory Interface")
			{
				ToolTip = "Expands the inventory interface.",
				TrueValue = "2",
			});

			// Worldmap
			ChoiceOption expandWorldmap = Choice(
				sfall,
				"ExpandWorldMap",
				"Expanded Worldmap Interface",
				["Default size", "Expanded size", "Expanded size w/o scaled town markers"],
				"Uses the expanded world map interface.");
			BoolOption wideWorldmap = Bool(sfall, "WideWorldmap", "Wide Worldmap", "Uses an interface of 1280x720 pixels for the world map.");
			wideWorldmap.EnableWhen(() => expandWorldmap.SelectedIndex > 0, expandWorldmap);
			BoolOption worldmapFontFix = Bool(sfall, "WorldMapFontPatch", "Worldmap Font Fix");
			BoolOption terrainInfo = Bool(sfall, "WorldMapTerrainInfo", "Worldmap Terrain Info", "Shows the terrain type of the world map when the cursor is over the player's marker.");
			BoolOption travelMarkers = Bool(sfall, "WorldMapTravelMarkers", "Worldmap Travel Markers");
			BoolOption translucency = Bool(sfall, "WorldmapBlackTranslucency", "Worldmap Translucency", "Makes the black unexplored areas of the world map translucent.");

			// Speed animation
			BoolOption pipboyTimeAnim = Add(new BoolOption(sfall, "PipboyTimeAnimDelay", "Pipboy Time Anim")
			{
				ToolTip = "Speeds up the animation of the alarm clock time counter in the Pip-Boy.",
				TrueValue = "10",
				FalseValue = "50",
				Interpret = raw => IniValue.ToInt32(raw) <= 25,
			});
			BoolOption speedInterfaceCounter = Add(new BoolOption(sfall, "SpeedInterfaceCounterAnims", "Speed Interface Counter")
			{
				ToolTip = "Speeds up the animation of the HP/AC counter.",
				TrueValue = "1",
				FalseValue = "3",
				Interpret = raw => IniValue.ToInt32(raw) is not (0 or 3),
			});
			NumberOption combatPanelDelay = Number(
				sfall,
				"CombatPanelAnimDelay",
				"Combat Panel Anim Delay",
				0,
				10000,
				"Controls the animation speed of the combat panel.\nLower is faster.",
				increment: 100);
			NumberOption dialogPanelDelay = Number(
				sfall,
				"DialogPanelAnimDelay",
				"Dialog Panel Anim Delay",
				0,
				60,
				"Controls the animation speed of the dialogue panel.\nLower is faster.");

			// Scrolling quests list: one key holds both the switch (0 = off) and the button type (1 or 2).
			ChoiceOption questButtonType = Add(new ChoiceOption(sfall, "UseScrollingQuestsList", "Buttons", ["Small", "Big"])
			{
				ToolTip = "Type of the buttons.",
				Values = ["1", "2"],
				IndexFromRaw = raw => IniValue.ToInt32(raw) == 2 ? 1 : 0,
			});
			BoolOption scrollingQuests = Add(new BoolOption(sfall, "UseScrollingQuestsList", "Scrolling Quests List")
			{
				ToolTip = "Shows buttons in the PipBoy to scroll the list of quests.\n\nSince sfall 5.0.1 pages for quests are the default,\nthis option changes the default display to buttons.",
				TrueValueSource = () => IniValue.Format(Math.Max(questButtonType.SelectedIndex, 0) + 1),
			});
			NumberOption questButtonX = Number(sfall, "QuestsScrollButtonsX", "Buttons position X", 0, 640, "Position of the buttons on the X axis");
			NumberOption questButtonY = Number(sfall, "QuestsScrollButtonsY", "Buttons position Y", 0, 480, "Position of the buttons on the Y axis");
			foreach (ConfigOption dependent in new ConfigOption[] { questButtonType, questButtonX, questButtonY })
			{
				dependent.EnableWhen(() => scrollingQuests.Value == true, scrollingQuests);
			}

			KeyOption hideInterfaceKey = Key(context.SfallMods.In("Misc"), "HideInterfaceKey", "Hide Interface Key :", 45);

			Groups =
			[
				new OptionGroup(
					"Display",
					bonusDamage,
					electricalResist,
					cityReputation,
					partyExtraInfo,
					numbersInDialogue,
					musicInDialogue,
					actionPointsBar,
					expandInventory,
					hideInterfaceKey),
				new OptionGroup("Speed Animation", pipboyTimeAnim, speedInterfaceCounter, combatPanelDelay, dialogPanelDelay),
				new OptionGroup("Worldmap", expandWorldmap, wideWorldmap, worldmapFontFix, terrainInfo, travelMarkers, translucency),
				new OptionGroup("PipBoy quests", scrollingQuests, questButtonType, questButtonX, questButtonY),
			];
		}
	}
}