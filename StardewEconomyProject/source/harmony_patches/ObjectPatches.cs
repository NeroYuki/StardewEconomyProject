using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches targeting StardewValley.Object to intercept sale price calculations
    /// and machine interactions for the economy system.
    /// </summary>
    public class ObjectPatches
    {
        private static IMonitor Monitor;

        // Artisan machine qualified IDs → config cost lookup
        private static readonly Dictionary<string, Func<ModConfig, int>> ArtisanMachineCosts = new()
        {
            ["(BC)12"] = c => c.KegOperationCost,           // Keg
            ["(BC)15"] = c => c.PreservesJarOperationCost,   // Preserves Jar
            ["(BC)246"] = c => c.DehydratorOperationCost,    // Dehydrator
            ["(BC)FishSmoker"] = c => c.FishSmokerOperationCost, // Fish Smoker
        };

        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        // ════════════════════════════════════════════════════════════════
        //  POSTFIX: Object.sellToStorePrice
        //  Applies the full economy multiplier chain:
        //    GlobalSellMultiplier * CategoryMultiplier * Saturation * Seasonal
        // ════════════════════════════════════════════════════════════════
        public static void SellToStorePrice_Postfix(SObject __instance, ref int __result, long specificPlayerID)
        {
            try
            {
                if (__instance == null || __result <= 0) return;

                float multiplier = economy.EconomyEngine.GetPriceMultiplier(__instance);
                int original = __result;
                __result = Math.Max(1, (int)(__result * multiplier));

                if (original != __result)
                {
                    LogHelper.Trace($"[SellPrice] {__instance.Name}: {original}g → {__result}g (x{multiplier:F3})");
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed in {nameof(SellToStorePrice_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  POSTFIX: Object.salePrice
        //  Adjusts shop buy prices. We apply a moderated version of the
        //  economy multiplier so shops don't become absurdly cheap.
        //  Shop buy price uses category + saturation but NOT GlobalSellMultiplier.
        // ════════════════════════════════════════════════════════════════
        public static void SalePrice_Postfix(SObject __instance, ref int __result, bool ignoreProfitMargins)
        {
            try
            {
                if (__instance == null || __result <= 0) return;

                // For buy prices, apply saturation only — not the global deflation
                // This makes shops "normal" priced while sell is nerfed
                float saturation = economy.MarketManager.GetSaturationMultiplier(__instance);
                __result = Math.Max(1, (int)(__result * saturation));
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed in {nameof(SalePrice_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  POSTFIX: Object.getPriceAfterMultipliers
        //  Applies custom quality margin multipliers from the spec.
        //  Undoes vanilla quality scaling and applies our own.
        // ════════════════════════════════════════════════════════════════
        public static void GetPriceAfterMultipliers_Postfix(SObject __instance, ref float __result, float startPrice, long specificPlayerID)
        {
            try
            {
                if (__instance == null || __instance.Quality <= 0) return;

                // Vanilla applied: price * (1 + quality * 0.25)
                // We undo that and apply our quality margin multipliers
                float vanillaQualityFactor = 1f + __instance.Quality * 0.25f;
                float customQualityFactor = economy.EconomyEngine.GetQualityMarginMultiplier(__instance.Quality);

                float basePrice = startPrice / vanillaQualityFactor;
                float professionMultiplier = __result / startPrice;

                __result = basePrice * customQualityFactor * professionMultiplier;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed in {nameof(GetPriceAfterMultipliers_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  POSTFIX: Object.performObjectDropInAction
        //  Tracks machine activations for:
        //    1. Utility tax (machine usage fee)
        //    2. Artisan machine operation cost deduction
        // ════════════════════════════════════════════════════════════════
        public static void PerformObjectDropInAction_Postfix(SObject __instance, ref bool __result, Item dropInItem, bool probe, Farmer who)
        {
            try
            {
                // Only act on real (non-probe) successful activations
                if (!__result || probe || __instance == null || who == null) return;

                // Track machine activation for utility tax
                economy.TaxManager.RecordMachineActivation();

                // Deduct artisan machine operation cost
                var config = ModConfig.GetInstance();
                string machineId = __instance.QualifiedItemId;
                if (ArtisanMachineCosts.TryGetValue(machineId, out var costGetter))
                {
                    int cost = costGetter(config);
                    if (cost > 0 && who.Money >= cost)
                    {
                        who.Money -= cost;
                        LogHelper.Debug($"[Machine] {__instance.Name} operation cost: -{cost}g");
                    }
                    else if (cost > 0)
                    {
                        LogHelper.Debug($"[Machine] {__instance.Name} operation cost: {cost}g (insufficient funds)");
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed in {nameof(PerformObjectDropInAction_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}
