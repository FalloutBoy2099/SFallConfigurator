namespace SFallConfigurator.ViewModels
{
	using System;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Interface" tab of the sfall settings. The catalog says what the options are; the page says how they depend on each other.
	/// </summary>
	internal sealed class InterfacePage : OptionPage
	{
		public InterfacePage(OptionContext context)
			: base(OptionCatalog.Page("interface"), context)
		{
			// Worldmap
			ChoiceOption expandWorldmap = Catalog.Get<ChoiceOption>("expandWorldmap");
			BoolOption wideWorldmap = Catalog.Get<BoolOption>("wideWorldmap");
			wideWorldmap.EnableWhen(() => expandWorldmap.SelectedIndex > 0, expandWorldmap);

			// Speed animation
			BoolOption pipboyTimeAnim = Catalog.Get<BoolOption>("pipboyTimeAnim");
			pipboyTimeAnim.Interpret = raw => IniValue.ToInt32(raw) <= 25;
			BoolOption speedInterfaceCounter = Catalog.Get<BoolOption>("speedInterfaceCounter");
			speedInterfaceCounter.Interpret = raw => IniValue.ToInt32(raw) is not (0 or 3);

			// Scrolling quests list: one key holds both the switch (0 = off) and the button type (1 or 2).
			ChoiceOption questButtonType = Catalog.Get<ChoiceOption>("questButtonType");
			questButtonType.IndexFromRaw = raw => IniValue.ToInt32(raw) == 2 ? 1 : 0;
			BoolOption scrollingQuests = Catalog.Get<BoolOption>("scrollingQuests");
			scrollingQuests.TrueValueSource = () => IniValue.Format(Math.Max(questButtonType.SelectedIndex, 0) + 1);
			foreach (ConfigOption dependent in new ConfigOption[] { questButtonType, Catalog.Get<NumberOption>("questButtonX"), Catalog.Get<NumberOption>("questButtonY") })
			{
				dependent.EnableWhen(() => scrollingQuests.Value == true, scrollingQuests);
			}

			Groups = Catalog.BuildGroups();
		}
	}
}