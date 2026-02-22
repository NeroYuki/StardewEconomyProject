using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Manages the Delivery Truck container — a farm big-craftable that the player
    /// loads with produce each day to fulfil active contracts and ship leftovers.
    ///
    /// Flow:
    ///   1. Player right-clicks the Delivery Truck → ItemGrabMenu opens (backed by TruckContents).
    ///   2. Player drags items in throughout the day.
    ///   3. At DayStarted, ProcessDayEnd() runs:
    ///        a. Try to fulfil every accepted BargainManager offer that matches truck items.
    ///        b. Ship remaining items as normal market sales (bottle fill + income).
    ///        c. Clear the truck.
    /// </summary>
    public static class DeliveryTruckManager
    {
        // Max slots in the truck cargo hold
        public const int TruckSlots = 36;

        /// <summary>Persistent truck contents — the live ItemGrabMenu is backed by this list.</summary>
        public static List<Item> TruckContents { get; private set; } = new List<Item>();

        // ══════════════════════════════════════════════════════════════
        //  INIT / RESET
        // ══════════════════════════════════════════════════════════════

        public static void Initialize()
        {
            TruckContents.Clear();
        }

        public static void Reset()
        {
            TruckContents.Clear();
        }

        // ══════════════════════════════════════════════════════════════
        //  DAY-END PROCESSING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called at DayStarted (before the new day fully loads).
        /// Processes all truck contents: contract delivery → normal shipping.
        /// </summary>
        public static void ProcessDayEnd()
        {
            if (TruckContents.Count == 0) return;

            int totalContracts = 0;
            int totalShipped   = 0;
            int totalIncome    = 0;

            // ── Step 1: fulfil accepted bargain offers ──
            var pending = TruckContents.Where(i => i != null).ToList();
            pending = BargainManager.FulfillFromTruck(pending, out int contractCount, out int contractIncome);
            totalContracts = contractCount;
            totalIncome   += contractIncome;

            // ── Step 2: ship remaining items normally ──
            foreach (var item in pending)
            {
                if (item == null) continue;

                // Fill market bottle
                MarketManager.RecordSale(item, item.Stack);

                // Record income
                int sellPrice = 0;
                if (item is SObject obj)
                    sellPrice = obj.sellToStorePrice(-1L) * item.Stack;

                if (sellPrice > 0)
                {
                    TaxManager.RecordIncome(sellPrice);
                    Game1.player.Money += sellPrice;
                    totalIncome += sellPrice;
                }

                totalShipped += item.Stack;
            }

            // ── Step 3: clear truck ──
            TruckContents.Clear();

            // ── Step 4: HUD summary ──
            if (totalContracts > 0 || totalShipped > 0)
            {
                string msg = string.Empty;
                if (totalContracts > 0) msg += $"{totalContracts} contract(s) fulfilled. ";
                if (totalShipped   > 0) msg += $"{totalShipped} item(s) shipped. ";
                msg += $"+{totalIncome}g total.";
                Game1.addHUDMessage(new HUDMessage(msg.Trim(), HUDMessage.achievement_type));
            }

            LogHelper.Info($"[Truck] Day-end: {totalContracts} contracts, {totalShipped} shipped, {totalIncome}g earned.");
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            var dtos = TruckContents
                .Where(i => i != null)
                .Select(i => new TruckItemDto
                {
                    QualifiedItemId = i.QualifiedItemId,
                    Quality = (i is SObject o) ? o.Quality : 0,
                    Stack   = i.Stack
                })
                .ToList();

            return JsonConvert.SerializeObject(dtos);
        }

        public static void Deserialize(string json)
        {
            TruckContents.Clear();

            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var dtos = JsonConvert.DeserializeObject<List<TruckItemDto>>(json);
                if (dtos == null) return;

                foreach (var dto in dtos)
                {
                    var item = ItemRegistry.Create(dto.QualifiedItemId, dto.Stack);
                    if (item == null) continue;
                    if (item is SObject obj)
                        obj.Quality = dto.Quality;
                    TruckContents.Add(item);
                }

                LogHelper.Info($"[Truck] Loaded {TruckContents.Count} item(s) from save.");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Truck] Failed to deserialize save data:\n{ex}");
            }
        }

        // ── Serialization DTO ──
        private class TruckItemDto
        {
            public string QualifiedItemId { get; set; }
            public int Quality { get; set; }
            public int Stack   { get; set; }
        }
    }
}
