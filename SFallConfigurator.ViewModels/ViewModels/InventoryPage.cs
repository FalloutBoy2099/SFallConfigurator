namespace SFallConfigurator.ViewModels
{
	using Options;

	/// <summary>
	/// The "Inventory" tab of the sfall settings. Everything it shows is in the catalog.
	/// </summary>
	internal sealed class InventoryPage : OptionPage
	{
		public InventoryPage(OptionContext context)
			: base(OptionCatalog.Page("inventory"), context)
		{
			Groups = Catalog.BuildGroups();
		}
	}
}