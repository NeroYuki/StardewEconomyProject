using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewEconomyProject.source.economy;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Patches the Collections page to show per-item market supply/demand
    /// information in the hover tooltip when viewing shipped items.
    /// </summary>
    public static class CollectionsPagePatch
    {
        private static IMonitor _monitor;

        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Register the Harmony patch on CollectionsPage.createDescription.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                var original = AccessTools.Method(typeof(CollectionsPage), "createDescription",
                    new[] { typeof(string) });
                if (original != null)
                {
                    harmony.Patch(
                        original: original,
                        postfix: new HarmonyMethod(typeof(CollectionsPagePatch),
                            nameof(CreateDescription_Postfix))
                    );
                    _monitor?.Log("Patched CollectionsPage.createDescription for market hover.", LogLevel.Debug);
                }
                else
                {
                    _monitor?.Log("Could not find CollectionsPage.createDescription.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"CollectionsPage patch failed: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Postfix on CollectionsPage.createDescription — appends market economy
        /// supply/demand information for shipped-items (tab 0) and fish (tab 1).
        /// </summary>
        public static void CreateDescription_Postfix(
            CollectionsPage __instance,
            string id,
            ref string __result)
        {
            try
            {
                // Only add market info for shipped items (tab 0), fish (tab 1), cooking (tab 4)
                int currentTab = Traverse.Create(__instance).Field("currentTab").GetValue<int>();
                if (currentTab != 0 && currentTab != 1 && currentTab != 4)
                    return;

                // Create a temporary item to look up its category
                var item = ItemRegistry.Create("(O)" + id) as SObject;
                if (item == null)
                    return;

                string category = MarketCategories.FromItemCategory(item.Category);
                var bottle = MarketManager.GetBottleForItem(item);
                if (bottle == null)
                    return;

                // Calculate the economy-adjusted sell price
                int rawPrice = item.sellToStorePrice(-1L);
                float satMult = bottle.DynamicPriceMultiplier;

                __result += Environment.NewLine;
                __result += Environment.NewLine + $"== Market Supply ({item.DisplayName}) ==";
                __result += Environment.NewLine + $"  Category: {category}";
                __result += Environment.NewLine + $"  Saturation: {bottle.Saturation:P0}";
                __result += Environment.NewLine + $"  State: {bottle.MarketState}";
                __result += Environment.NewLine + $"  Price Multiplier: x{satMult:F2}";
                __result += Environment.NewLine + $"  Economy Sell Price: {rawPrice}g";
            }
            catch
            {
                // Silently fail — don't break the Collections page over a tooltip enhancement
            }
        }
    }
}
