namespace SFallConfigurator.ViewModels
{
	using System;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Combat" tab of the sfall settings.
	/// </summary>
	internal sealed class CombatPage : OptionPage
	{
		private const String GlovzAmmoFile = "AmmoGlovz.ini";
		private const String YaamAmmoFile = "AmmoYAAM.ini";

		private readonly ChoiceOption _damageFormula;
		private readonly TextOption _ammoFile;

		public CombatPage(OptionContext context)
			: base("Combat", context)
		{
			GameConfiguration configuration = context.Configuration;
			OptionLocation sfall = context.Sfall;
			OptionLocation mods = context.SfallMods;
			OptionLocation misc = mods.In("Misc");

			ChoiceOption saveInCombat = Choice(
				sfall,
				"SaveInCombatFix",
				"Save In Combat Mode",
				["Allow Saving (default)", "Save only when full AP (recommended)", "Block All Saving"]);

			// Party combat control (the setting moved from ddraw.ini to sfall-mods.ini in sfall 4.x)
			ChoiceOption controlCombat = Add(new ChoiceOption(
				mods.In("CombatControl"),
				"Mode",
				"Party Combat Control Mod",
				["0 - Disable", "1 - Direct Control mode (only Human)", "2 - Direct Control mode (all)", "3 - Order Attack mode"])
			{
				ToolTip = "Lets you directly control every party member in combat.",
				ReadFallback = () => configuration.SfallSource.Read(null, "ControlCombat"),
				Written = raw =>
				{
					if (IniValue.ToInt32(raw) < 3)
					{
						configuration.SfallSource.Write(null, "ControlCombat", raw);
					}
				},
			});

			KeyOption toggleCombatKey = Add(new KeyOption(mods.In("CombatControl"), "Key", "Toggle Control Key :", 0, context.Interaction));
			toggleCombatKey.EnableWhen(() => !controlCombat.IsAvailable || controlCombat.SelectedIndex == 2, controlCombat);

			KeyOption reloadWeaponKey = Key(sfall, "ReloadWeaponKey", "Reload Weapon Key :", 17);

			ChoiceOption fastShot = Choice(
				sfall,
				"FastShotFix",
				"Fast Shot Trait",
				[
					"Fallout 2 behaviour",
					"Aimed shots are allowed for melee and HtH attacks",
					"Alternative behaviour: -1 AP for all attacks",
					"Fallout 1 behaviour: -1 AP for all weapons",
				],
				"Alternative behaviors for hand-to-hand attacks and the \"Fast Shot\" trait.");

			_damageFormula = Add(new ChoiceOption(
				sfall,
				"DamageFormula",
				"Damage Formula Mod",
				[
					"Fallout default",
					"Glovz's Damage Fix",
					"Glovz's Damage Fix with Damage Multiplier",
					"Jim's Formula (only Sfall v3.3 - v3.6)",
					"Haenlomal's Yet Another Ammo Mod",
				])
			{
				ToolTip = "Damage formula (used to calculate damage in various mods).",
				Values = ["0", "1", "2", "4", "5"],
			});

			// The option is always available: when sfall-mods.ini has no active AmmoFile line, writing adds it (a commented example stays as it is).
			OptionLocation ammoFileLocation = new OptionLocation(new DocumentSource(() => configuration.SfallMods, IniWriteMode.AddIfMissing), "Misc");
			_ammoFile = Add(new TextOption(ammoFileLocation, "AmmoFile", "Ammo File") { DefaultValue = String.Empty });
			_damageFormula.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(ChoiceOption.SelectedIndex))
				{
					UpdateAmmoFile();
				}
			};

			// Mods
			BoolOption combatKeyMod = Bool(misc, "CombatKeyMod", "Combat Key Mod");
			BoolOption grenadeThrowing = Bool(misc, "GrenadeThrowing", "Grenade Throwing Mod");
			BoolOption accuracyMod = Bool(misc, "AccuracyMod", "Accuracy Mod");
			BoolOption dropItemsOnDeath = Bool(mods.In("Inventory"), "DropItemsOnDeath", "Drop Items On Death");

			// Combat tweaks
			NumberOption npcSpendExtraAp = Number(sfall, "NPCsTryToSpendExtraAP", "NPC Try To Spend AP", 0, 10, "Number of AP. 0 to disable.");
			BoolOption punchKnockback = Bool(sfall, "DisablePunchKnockback", "Disable Punch Knockback");
			BoolOption autoMoveToAttack = Bool(sfall, "AutoMoveToAttack", "Auto Move To Attack");
			BoolOption computeSpray = Add(new BoolOption(sfall, "ComputeSprayMod", "Compute Spray Mod")
			{
				ToolTip = "Enables an alternative spray formula for the flight of bullets in burst fire.\nThe formula is based on the weapon skill level of your character (or of a party member):\nthe higher the skill (above 100%), the more bullets land in the central group,\nand with a low skill you get a PENALTY and more bullets are spread\nover the adjacent lines of the spray.",
				TrueValue = "2",
				Interpret = raw => raw == "2",
			});

			ChoiceOption burstFriendlyFire = Add(new ChoiceOption(
				sfall,
				"CheckBurstFriendlyFire",
				"Check Burst Friendly Fire Mode:",
				["0: Random", "1: Block", "2: More Check", "3: Combo Mode", "-1: Disable Check"])
			{
				ToolTip = "0 - Prevents a burst with a probability of INT*10% of the attacking NPC.\n1 - Always prevents the attack when a friendly NPC is on the direct line of fire.\n2 - Increases the number of checks before an attack; the number of checks is based\n     on the INT*16 of the attacking NPC (by default the game makes only one check).\n3 - Enables modes 1 and 2: mode 1 plus extra checks for the adjacent lines of fire.\n-1 - Completely disables this feature of sfall.",
				Values = ["0", "1", "2", "3", "-1"],
			});

			// Improve Combat
			BoolOption smartBehavior = Bool(sfall, "SmartBehavior", "AI Smart Behavior", "Enables improved AI behavior algorithms in combat.");
			BoolOption tryFindTarget = Bool(sfall, "TryToFindTargets", "Try Find New Target");
			BoolOption takeBetterWeapons = Bool(sfall, "TakeBetterWeapons", "Take Better Weapons");
			BoolOption hidingTactics = Bool(sfall, "HidingTactics", "Hiding Tactics");
			BoolOption lootingCorpses = Bool(sfall, "LootingCorpses", "Looting Corpses");
			BoolOption npcPushOnMoveBlock = Bool(sfall, "NPCPushOnMoveBlock", "Push On Move");

			foreach (BoolOption dependent in new[] { takeBetterWeapons, tryFindTarget, hidingTactics })
			{
				dependent.EnableWhen(() => !smartBehavior.IsAvailable || smartBehavior.Value == true, smartBehavior);
			}

			Groups =
			[
				new OptionGroup(
					"Combat",
					saveInCombat,
					controlCombat,
					toggleCombatKey,
					reloadWeaponKey,
					fastShot,
					burstFriendlyFire,
					npcSpendExtraAp,
					punchKnockback,
					autoMoveToAttack,
					computeSpray),
				new OptionGroup("Damage formula", _damageFormula, _ammoFile),
				new OptionGroup("Mods", combatKeyMod, grenadeThrowing, accuracyMod, dropItemsOnDeath),
				new OptionGroup("Improve Combat", smartBehavior, tryFindTarget, takeBetterWeapons, lootingCorpses, npcPushOnMoveBlock, hidingTactics),
			];
		}

		public override void Load()
		{
			base.Load();
			UpdateAmmoFile();
		}

		/// <summary>
		/// The ammo file is imposed by some damage formulas; for the others it can be edited.
		/// </summary>
		private void UpdateAmmoFile()
		{
			switch (_damageFormula.SelectedValue)
			{
				case "1":
				case "2":
					_ammoFile.ShowFixed(GlovzAmmoFile, readOnly: true);
					break;
				case "5":
					_ammoFile.ShowFixed(YaamAmmoFile, readOnly: true);
					break;
				default:
					_ammoFile.ShowFixed(Context.Configuration.SfallMods?.GetValue("Misc", "AmmoFile") ?? String.Empty, readOnly: false);
					break;
			}
		}
	}
}