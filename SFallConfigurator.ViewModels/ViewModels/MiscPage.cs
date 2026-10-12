namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Globalization;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Misc" tab of the sfall settings. The catalog says what the options are; the page says how they are read and written.
	/// </summary>
	internal sealed class MiscPage : OptionPage
	{
		// The color animation speed is stored as a factor (1.0 = normal, 2.0 = twice as slow); the slider counts in 1/20 steps.
		private const Int32 CycleSpeedSteps = 20;

		public MiscPage(OptionContext context)
			: base(OptionCatalog.Page("misc"), context)
		{
			BoolOption skipLoadingGameSettings = Catalog.Get<BoolOption>("skipLoadingGameSettings");
			skipLoadingGameSettings.Interpret = raw => raw == "2";

			ChoiceOption motionScanner = Catalog.Get<ChoiceOption>("motionScanner");
			motionScanner.IndexFromRaw = raw => IniValue.ToInt32(raw) & 0x3;

			// Animation & Effects
			BoolOption fadeMultiplier = Catalog.Get<BoolOption>("fadeMultiplier");
			fadeMultiplier.Interpret = raw => IniValue.ToInt32(raw) == 50;

			NumberOption soundBuffers = Catalog.Get<NumberOption>("soundBuffers");
			soundBuffers.Normalize = NormalizeSoundBuffers;

			NumberOption colorAnimationSpeed = Catalog.Get<NumberOption>("colorAnimationSpeed");
			colorAnimationSpeed.ParseRaw = raw => Math.Clamp((Int32)Math.Round(CycleSpeedSteps * (IniValue.ToSingle(raw, 1.0f) - 1.0f)), 0, CycleSpeedSteps);
			colorAnimationSpeed.FormatRaw = steps => (1.0f + (steps / (Single)CycleSpeedSteps)).ToString("F6", CultureInfo.InvariantCulture);

			// Object Lighting
			BoolOption useLightOrientation = Catalog.Get<BoolOption>("useLightOrientation");
			BoolOption legacyLighting = Catalog.Get<BoolOption>("legacyLighting");
			useLightOrientation.EnableWhen(() => legacyLighting.Value == false, legacyLighting);

			Groups = Catalog.BuildGroups();
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