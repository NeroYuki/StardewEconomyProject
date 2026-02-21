using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches targeting StardewValley.Menus.ShopMenu.
    ///
    /// The ShopMenu is where items are bought/sold at stores. The sell price
    /// in-shop is calculated as: sellToStorePrice() * this.sellPercentage
    ///
    /// We don't need to patch the sell logic directly because our
    /// ObjectPatches.SellToStorePrice_Postfix already intercepts the
    /// underlying sellToStorePrice() call. However, we hook into shop
    /// interactions for supply/demand tracking and potential future
    /// per-shop price adjustments.
    /// </summary>
    public class ShopPatches
    {
        private static IMonitor Monitor;

        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        // ════════════════════════════════════════════════════════════════
        //  POSTFIX: Utility.getSellToStorePriceOfItem
        //  This utility method is used in some contexts to calculate sell values.
        //  Vanilla (Utility.cs line ~2634):
        //    return i.sellToStorePrice(-1) * (countStack ? i.Stack : 1);
        //
        //  Since sellToStorePrice is already patched, this should be covered,
        //  but we hook here as a safety net for any code path that caches prices.
        // ════════════════════════════════════════════════════════════════

        // Reserved for future per-shop multipliers (e.g. Pierre vs Joja pricing)
    }
}
