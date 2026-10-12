namespace SFallConfigurator.ViewModels
{
	using Configuration;
	using Options;

	/// <summary>
	/// The "Common" tab of the sfall settings. The catalog says what the options are; the page says how they depend on each other.
	/// </summary>
	internal sealed class CommonPage : OptionPage
	{
		public CommonPage(OptionContext context)
			: base(OptionCatalog.Page("common"), context)
		{
			GameConfiguration configuration = context.Configuration;

			// Highlight Mod
			KeyOption highlightKey = Catalog.Get<KeyOption>("highlightKey");
			highlightKey.ReadFallback = () => configuration.SfallSource.Read(null, "ToggleItemHighlightsKey");
			highlightKey.Written = raw => configuration.SfallSource.Write(null, "ToggleItemHighlightsKey", raw);

			BoolOption highlightContainers = Catalog.Get<BoolOption>("highlightContainers");
			highlightContainers.ReadFallback = () => configuration.SfallSource.Read(null, "HighlightContainers") ?? configuration.SfallSource.Read(null, "TurnHighlightContainers");
			highlightContainers.Written = raw =>
			{
				configuration.SfallSource.Write(null, "HighlightContainers", raw);
				configuration.SfallSource.Write(null, "TurnHighlightContainers", raw);
			};

			BoolOption highlightCorpses = Catalog.Get<BoolOption>("highlightCorpses");

			highlightContainers.EnableWhen(() => highlightKey.IsChecked == true, highlightKey);
			highlightCorpses.EnableWhen(() => highlightKey.IsChecked == true, highlightKey);

			// Quick Save Behavior
			BoolOption extraSaveSlots = Catalog.Get<BoolOption>("extraSaveSlots");
			NumberOption startSavePage = Catalog.Get<NumberOption>("startSavePage");
			NumberOption quickSavePages = Catalog.Get<NumberOption>("quickSavePages");

			startSavePage.EnableWhen(() => extraSaveSlots.Value != false, extraSaveSlots);
			quickSavePages.EnableWhen(() => extraSaveSlots.Value != false, extraSaveSlots);

			// Auto Save
			BoolOption autoSave = Catalog.Get<BoolOption>("autoSave");
			foreach (NumberOption option in new[] { Catalog.Get<NumberOption>("autoSavePage"), Catalog.Get<NumberOption>("autoSaveCountPages"), Catalog.Get<NumberOption>("autoSaveTime") })
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
			ChoiceOption showInfoWindow = Catalog.Get<ChoiceOption>("showInfoWindow");
			showInfoWindow.IndexFromRaw = raw => (IniValue.ToInt32(raw) is 1 or 2) ? IniValue.ToInt32(raw) : 0;

			NumberOption gameSpeed = Catalog.Get<NumberOption>("gameSpeed");
			gameSpeed.AvailabilityCondition = () => configuration.Ddraw.HasSection("Speed");
			gameSpeed.Written = _ => configuration.SfallSource.Write("Speed", "Enable", "1");

			Groups = Catalog.BuildGroups();
		}
	}
}