using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Central registry for all Harmony patches. Called once from ModEntry.
    ///
    /// Patch targets:
    /// ─────────────────────────────────────────────────────────────────────
    /// 1. Object.sellToStorePrice(long)
    ///    → Postfix: apply GlobalSellMultiplier + saturation + category
    ///
    /// 2. Object.salePrice(bool)
    ///    → Postfix: adjust shop buy prices to match economy
    ///
    /// 3. Object.getPriceAfterMultipliers(float, long)
    ///    → Postfix: custom quality margin scaling
    ///
    /// 4. ShippingMenu.parseItems(IList<Item>)
    ///    → Prefix: record sales to MarketManager bottles
    ///
    /// 5. Object.performObjectDropInAction(Item, bool, Farmer)
    ///    → Postfix: track machine activations for utility tax
    ///    → Postfix: deduct artisan machine operation costs
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class HarmonyPatches
    {
        public static void InitPatches(string uniqueModId, IMonitor monitor)
        {
            ObjectPatches.Initialize(monitor);
            ShopPatches.Initialize(monitor);
            ShippingPatches.Initialize(monitor);
            BigCraftablePatches.Initialize(monitor);
            TvPatches.Initialize(monitor);
            CollectionsPagePatch.Initialize(monitor);
            BuildingPatches.Initialize(monitor);

            var harmony = new Harmony(uniqueModId);

            // ── Object.sellToStorePrice ──
            harmony.Patch(
                original: AccessTools.Method(typeof(SObject), nameof(SObject.sellToStorePrice), new[] { typeof(long) }),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.SellToStorePrice_Postfix))
            );

            // ── Object.salePrice ──
            harmony.Patch(
                original: AccessTools.Method(typeof(SObject), nameof(SObject.salePrice), new[] { typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.SalePrice_Postfix))
            );

            // ── Object.getPriceAfterMultipliers ──
            harmony.Patch(
                original: AccessTools.Method(typeof(SObject), "getPriceAfterMultipliers", new[] { typeof(float), typeof(long) }),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.GetPriceAfterMultipliers_Postfix))
            );

            // ── ShippingMenu.parseItems ──
            harmony.Patch(
                original: AccessTools.Method(typeof(ShippingMenu), nameof(ShippingMenu.parseItems),
                    new[] { typeof(IList<Item>) }),
                prefix: new HarmonyMethod(typeof(ShippingPatches), nameof(ShippingPatches.ParseItems_Prefix))
            );

            // ── Object.performObjectDropInAction ──
            // Tracks machine activations for utility tax and deducts operation costs
            try
            {
                var dropInMethod = AccessTools.Method(typeof(SObject), nameof(SObject.performObjectDropInAction),
                    new[] { typeof(Item), typeof(bool), typeof(Farmer) });
                if (dropInMethod != null)
                {
                    harmony.Patch(
                        original: dropInMethod,
                        postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.PerformObjectDropInAction_Postfix))
                    );
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"Could not patch performObjectDropInAction: {ex.Message}", LogLevel.Warn);
            }

            // ── Object.checkForAction ──
            // Opens economy menus when interacting with placed SEP big craftables
            try
            {
                var checkForActionMethod = AccessTools.Method(typeof(SObject), nameof(SObject.checkForAction),
                    new[] { typeof(Farmer), typeof(bool) });
                if (checkForActionMethod != null)
                {
                    harmony.Patch(
                        original: checkForActionMethod,
                        postfix: new HarmonyMethod(typeof(BigCraftablePatches), nameof(BigCraftablePatches.CheckForAction_Postfix))
                    );
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"Could not patch checkForAction: {ex.Message}", LogLevel.Warn);
            }

            monitor.Log("Economy harmony patches applied successfully.", LogLevel.Info);

            // ── ShopMenu sell tracking (bottles + income) ──
            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick),
                    new[] { typeof(int), typeof(int), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(ShopPatches), nameof(ShopPatches.ReceiveLeftClick_Prefix)),
                postfix: new HarmonyMethod(typeof(ShopPatches), nameof(ShopPatches.ReceiveLeftClick_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveRightClick),
                    new[] { typeof(int), typeof(int), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(ShopPatches), nameof(ShopPatches.ReceiveRightClick_Prefix)),
                postfix: new HarmonyMethod(typeof(ShopPatches), nameof(ShopPatches.ReceiveRightClick_Postfix))
            );

            // ── TV Market Report channel ──
            TvPatches.Apply(harmony);

            // ── Collections page hover — per-item market info ──
            CollectionsPagePatch.Apply(harmony);

            // ── Building tile actions (Delivery Truck etc.) ──
            BuildingPatches.Apply(harmony);
        }
    }
}
