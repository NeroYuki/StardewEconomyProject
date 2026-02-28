using StardewValley;
using StardewValley.Menus;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Opens the Delivery Truck cargo hold using a real <see cref="StardewValley.Objects.Chest"/>.
    /// The chest handles all deposit / withdrawal / stacking logic natively —
    /// it behaves exactly like any other chest in the game.
    ///
    /// Items placed here are processed at the start of the next day:
    ///   • Accepted trade contracts are fulfilled first (urgency order).
    ///   • Remaining items are shipped to the market (bottle fill + income).
    /// </summary>
    public static class DeliveryTruckMenu
    {
        /// <summary>
        /// Opens the truck's backing Chest menu.
        /// Returns the menu so BuildingPatches can assign it to Game1.activeClickableMenu.
        /// </summary>
        public static IClickableMenu Open()
        {
            var chest = economy.DeliveryTruckManager.TruckChest;
            chest.ShowMenu();
            return Game1.activeClickableMenu;
        }
    }
}
