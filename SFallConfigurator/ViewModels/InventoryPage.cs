namespace SFallConfigurator.ViewModels
{
	using Options;

	/// <summary>
	/// The "Inventory" tab of the sfall settings.
	/// </summary>
	internal sealed class InventoryPage : OptionPage
	{
		public InventoryPage(OptionContext context)
			: base("Inventory", context)
		{
			OptionLocation sfall = context.Sfall;
			OptionLocation inventory = context.SfallMods.In("Inventory");

			KeyOption itemFastMoveKey = Key(sfall, "ItemFastMoveKey", "Item Fast Move Key :", 29);
			KeyOption itemInstantMoveKey = Key(sfall, "ItemInstantMoveKey", "Instant Item Move Key :", 42);
			KeyOption pickupItemKey = Key(
				inventory,
				"PickupItemKey",
				"Pickup Item Key :",
				34,
				"A hot key to quickly pick up the nearest item on the map.");

			BoolOption fastMoveFromContainer = Bool(
				sfall,
				"FastMoveFromContainer",
				"Fast Move from Container",
				"Allows moving a whole stack of items from containers to the player's inventory\nwithout holding the Ctrl key (or the key assigned to the ItemFastMoveKey option).\nThe ItemFastMoveKey option must be enabled; holding the assigned key (Ctrl by default)\nshows the item move window.");
			BoolOption itemCounterDefaultMax = Bool(sfall, "ItemCounterDefaultMax", "Item Counter Default Max");
			BoolOption instantWeaponEquip = Bool(sfall, "InstantWeaponEquip", "Instant Weapon Equip");
			BoolOption stackEmptyWeapons = Bool(sfall, "StackEmptyWeapons", "Stack Empty Weapons");
			BoolOption partyMemberTakeOffItem = Bool(sfall, "PartyMemberTakeOffItem", "Party Member Take Off Item");
			BoolOption canSellGeiger = Bool(
				sfall,
				"CanSellUsedGeiger",
				"Sell Geiger/Stealth Boy",
				"Allows selling Geiger counters or Stealth Boys that were used before.");

			BoolOption quickAmmoReload = Bool(inventory, "QuickAmmoReload", "Quick Ammo Reload");
			BoolOption autoReloadWeapon = Bool(
				inventory,
				"AutoReloadWeapon",
				"Auto Reload Weapon",
				"Automatically reloads the player's weapon after the combat.");
			BoolOption autoCloseContainers = Bool(
				inventory,
				"AutoCloseContainers",
				"Auto Close Containers",
				"Automatically closes a container after it was searched.");

			NumberOption reloadReserve = Number(
				sfall,
				"ReloadReserve",
				"Reload Reserve",
				-1,
				999,
				"Behavior when reloading the ammo of a weapon in the player's inventory.\n\n-1 - Default behavior with a pop-up window\n 0 - Use all the ammo packs to reload\n 1 or more - Reserves extra magazines");

			Groups =
			[
				new OptionGroup("Hot keys", itemFastMoveKey, itemInstantMoveKey, pickupItemKey),
				new OptionGroup(
					"Items",
					fastMoveFromContainer,
					itemCounterDefaultMax,
					instantWeaponEquip,
					stackEmptyWeapons,
					partyMemberTakeOffItem,
					canSellGeiger),
				new OptionGroup("Mods", quickAmmoReload, autoReloadWeapon, autoCloseContainers),
				new OptionGroup("Ammo", reloadReserve),
			];
		}
	}
}