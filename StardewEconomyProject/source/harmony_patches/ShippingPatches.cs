using System;
using StardewModdingAPI;
using StardewValley;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches for end-of-day shipping to record sales into market bottles
    /// and track income for taxation.
    /// </summary>
    public class ShippingPatches
    {
        private static IMonitor Monitor;

        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        /// <summary>
        /// Prefix on ShippingMenu.parseItems — records all shipped items into the
        /// MarketManager bottles for saturation tracking, and records income for tax.
        /// </summary>
        public static void ParseItems_Prefix(object __instance)
        {
            try
            {
                foreach (var item in Game1.getFarm().getShippingBin(Game1.player))
                {
                    if (item != null)
                    {
                        // Record volume into market bottles
                        economy.MarketManager.RecordSale(item, item.Stack);

                        // Record income for taxation
                        int sellPrice = 0;
                        if (item is StardewValley.Object obj)
                        {
                            sellPrice = obj.sellToStorePrice(-1L) * item.Stack;
                        }
                        if (sellPrice > 0)
                        {
                            economy.TaxManager.RecordIncome(sellPrice);
                        }

                        LogHelper.Trace($"[Shipping] {item.Stack}x {item.Name} → market bottle (income: {sellPrice}g)");
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed in {nameof(ParseItems_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}
