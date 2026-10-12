namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Graphics" tab of the Hi-Res patch settings. The catalog says what the options are; the page adds the resolution editor.
	/// </summary>
	internal sealed class HiResGraphicsPage : OptionPage
	{
		public HiResGraphicsPage(OptionContext context)
			: base(OptionCatalog.Page("hiresGraphics"), context)
		{
			Resolution = new HiResResolutionEditor(
				Catalog.Get<NumberOption>("width"),
				Catalog.Get<NumberOption>("height"),
				Catalog.Get<BoolOption>("scaleX2"),
				context.Services.DisplayModes)
			{
				IsFileAvailable = () => context.Configuration.HiRes is not null,
			};

			Groups = Catalog.BuildGroups(new Dictionary<String, Object> { ["resolution"] = Resolution });
		}

		public HiResResolutionEditor Resolution { get; }

		public override void Load()
		{
			base.Load();
			Resolution.Refresh();
		}
	}

	/// <summary>
	/// The "Interface" tab of the Hi-Res patch settings. The catalog says what the options are; the page says how they depend on each other.
	/// </summary>
	internal sealed class HiResInterfacePage : OptionPage
	{
		public HiResInterfacePage(OptionContext context)
			: base(OptionCatalog.Page("hiresInterface"), context)
		{
			BoolOption barMode = Catalog.Get<BoolOption>("barMode");
			Catalog.Get<NumberOption>("lightColor").PaletteSource = () => context.Palette;
			Catalog.Get<NumberOption>("darkColor").PaletteSource = () => context.Palette;
			Catalog.Get<BarArtOption>("sideArt").EnableWhen(() => barMode.Value != true, barMode);
			Catalog.Get<BoolOption>("sidesOrientation").EnableWhen(() => barMode.Value != true, barMode);

			Groups = Catalog.BuildGroups();
		}
	}

	/// <summary>
	/// The "Screens" tab of the Hi-Res patch settings. Everything it shows is in the catalog.
	/// </summary>
	internal sealed class HiResScreensPage : OptionPage
	{
		public HiResScreensPage(OptionContext context)
			: base(OptionCatalog.Page("hiresScreens"), context)
		{
			Groups = Catalog.BuildGroups();
		}
	}

	/// <summary>
	/// The "Map" tab of the Hi-Res patch settings. The catalog says what the options are; the page says how a step is shown.
	/// </summary>
	internal sealed class HiResMapPage : OptionPage
	{
		private const Int32 NodesPerStep = 2000;

		public HiResMapPage(OptionContext context)
			: base(OptionCatalog.Page("hiresMap"), context)
		{
			Catalog.Get<NumberOption>("pathNodes").DisplayText = steps => (steps * NodesPerStep).ToString(CultureInfo.InvariantCulture);

			BoolOption vanillaMapBlockers = Catalog.Get<BoolOption>("vanillaMapBlockers");
			vanillaMapBlockers.Interpret = raw => IniValue.ToInt32(raw) == 2;

			Groups = Catalog.BuildGroups();
		}
	}

	/// <summary>
	/// The "Misc" tab of the Hi-Res patch settings. The catalog says what the options are; the page says how a path is stored.
	/// </summary>
	internal sealed class HiResMiscPage : OptionPage
	{
		public HiResMiscPage(OptionContext context)
			: base(OptionCatalog.Page("hiresMisc"), context)
		{
			Catalog.Get<PathOption>("resourceData").Normalize = text => text.ToLowerInvariant();
			Catalog.Get<PathOption>("resourcePatches").Normalize = text => text.ToLowerInvariant();

			Groups = Catalog.BuildGroups();
		}
	}
}