namespace SFallConfigurator.ViewModels
{
	using System;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Combat" tab of the sfall settings. The catalog says what the options are; the page says how they depend on each other.
	/// </summary>
	internal sealed class CombatPage : OptionPage
	{
		private const String GlovzAmmoFile = "AmmoGlovz.ini";
		private const String YaamAmmoFile = "AmmoYAAM.ini";

		private readonly ChoiceOption _damageFormula;
		private readonly TextOption _ammoFile;

		public CombatPage(OptionContext context)
			: base(OptionCatalog.Page("combat"), context)
		{
			GameConfiguration configuration = context.Configuration;

			// Party combat control (the setting moved from ddraw.ini to sfall-mods.ini in sfall 4.x)
			ChoiceOption controlCombat = Catalog.Get<ChoiceOption>("controlCombat");
			controlCombat.ReadFallback = () => configuration.SfallSource.Read(null, "ControlCombat");
			controlCombat.Written = raw =>
			{
				if (IniValue.ToInt32(raw) < 3)
				{
					configuration.SfallSource.Write(null, "ControlCombat", raw);
				}
			};

			KeyOption toggleCombatKey = Catalog.Get<KeyOption>("toggleCombatKey");
			toggleCombatKey.EnableWhen(() => !controlCombat.IsAvailable || controlCombat.SelectedIndex == 2, controlCombat);

			// The ammo file is always available: when sfall-mods.ini has no active AmmoFile line, writing adds it
			// (a commented example stays as it is).
			_damageFormula = Catalog.Get<ChoiceOption>("damageFormula");
			_ammoFile = Catalog.Get<TextOption>("ammoFile");
			_damageFormula.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(ChoiceOption.SelectedIndex))
				{
					UpdateAmmoFile();
				}
			};

			BoolOption computeSpray = Catalog.Get<BoolOption>("computeSpray");
			computeSpray.Interpret = raw => raw == "2";

			BoolOption smartBehavior = Catalog.Get<BoolOption>("smartBehavior");
			foreach (BoolOption dependent in new[] { Catalog.Get<BoolOption>("takeBetterWeapons"), Catalog.Get<BoolOption>("tryFindTarget"), Catalog.Get<BoolOption>("hidingTactics") })
			{
				dependent.EnableWhen(() => !smartBehavior.IsAvailable || smartBehavior.Value == true, smartBehavior);
			}

			Groups = Catalog.BuildGroups();
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