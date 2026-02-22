using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches targeting StardewValley.Menus.ShopMenu.
    ///
    /// Tracks items sold at shops (Pierre, etc.) so that:
    ///   1. MarketManager bottles are filled  (saturation tracking)
    ///   2. TaxManager records the income      (income-tax assessment)
    ///
    /// Strategy: prefix/postfix pairs on ShopMenu.receiveLeftClick and
    /// receiveRightClick snapshot the player's money and inventory before
    /// the click, then diff afterwards to detect what was sold.
    /// </summary>
    public class ShopPatches
    {
        private static IMonitor Monitor;

        // ── Snapshot state between prefix/postfix ──
        private static int _preClickMoney;
        private static Dictionary<(string qualifiedId, int quality), int> _preClickStacks;

        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        // ════════════════════════════════════════════════════════════════
        //  SHARED: Snapshot / diff helpers
        // ════════════════════════════════════════════════════════════════

        /// <summary>Capture player money + inventory stacks before a click.</summary>
        private static void SnapshotInventory()
        {
            _preClickMoney = Game1.player.Money;
            _preClickStacks = new Dictionary<(string, int), int>();
            foreach (var item in Game1.player.Items)
            {
                if (item == null) continue;
                int quality = (item is SObject obj) ? obj.Quality : 0;
                var key = (item.QualifiedItemId, quality);
                _preClickStacks.TryGetValue(key, out int existing);
                _preClickStacks[key] = existing + item.Stack;
            }
        }

        /// <summary>
        /// Compare current inventory to the snapshot and record any sold
        /// items into the market bottles + income tracker.
        /// </summary>
        private static void DiffAndRecord()
        {
            int earned = Game1.player.Money - _preClickMoney;
            if (earned <= 0) return; // No sale happened

            // Record income for tax assessment
            economy.TaxManager.RecordIncome(earned);

            // Build current inventory map
            var postStacks = new Dictionary<(string, int), int>();
            foreach (var item in Game1.player.Items)
            {
                if (item == null) continue;
                int quality = (item is SObject obj) ? obj.Quality : 0;
                var key = (item.QualifiedItemId, quality);
                postStacks.TryGetValue(key, out int existing);
                postStacks[key] = existing + item.Stack;
            }

            // Find items whose stack decreased → those were sold
            foreach (var kvp in _preClickStacks)
            {
                postStacks.TryGetValue(kvp.Key, out int remaining);
                int sold = kvp.Value - remaining;
                if (sold > 0)
                {
                    economy.MarketManager.GetOrCreateBottle(kvp.Key.qualifiedId, kvp.Key.quality)
                        .AddVolume(sold);
                    LogHelper.Trace($"[Shop] Sold {sold}x {kvp.Key.qualifiedId} Q{kvp.Key.quality} → bottle (income: {earned}g)");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  PREFIX / POSTFIX: ShopMenu.receiveLeftClick
        // ════════════════════════════════════════════════════════════════

        public static void ReceiveLeftClick_Prefix(ShopMenu __instance, int x, int y)
        {
            try { SnapshotInventory(); }
            catch (Exception ex) { Monitor?.Log($"ShopPatches left-prefix error:\n{ex}", LogLevel.Error); }
        }

        public static void ReceiveLeftClick_Postfix(ShopMenu __instance, int x, int y)
        {
            try { DiffAndRecord(); }
            catch (Exception ex) { Monitor?.Log($"ShopPatches left-postfix error:\n{ex}", LogLevel.Error); }
        }

        // ════════════════════════════════════════════════════════════════
        //  PREFIX / POSTFIX: ShopMenu.receiveRightClick
        // ════════════════════════════════════════════════════════════════

        public static void ReceiveRightClick_Prefix(ShopMenu __instance, int x, int y)
        {
            try { SnapshotInventory(); }
            catch (Exception ex) { Monitor?.Log($"ShopPatches right-prefix error:\n{ex}", LogLevel.Error); }
        }

        public static void ReceiveRightClick_Postfix(ShopMenu __instance, int x, int y)
        {
            try { DiffAndRecord(); }
            catch (Exception ex) { Monitor?.Log($"ShopPatches right-postfix error:\n{ex}", LogLevel.Error); }
        }
    }
}
