using StardewValley;
using StardewValley.Menus;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Opens the Delivery Motorbike cargo hold using a real <see cref="StardewValley.Objects.Chest"/>.
    /// Identical UX to the truck, but the motorbike only delivers to regional NPCs.
    /// </summary>
    public static class DeliveryMotorbikeMenu
    {
        public static IClickableMenu Open()
        {
            var chest = economy.DeliveryMotorbikeManager.MotorbikeChest;
            chest.ShowMenu();
            return Game1.activeClickableMenu;
        }
    }
}
