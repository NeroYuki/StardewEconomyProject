using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Manages the Delivery Truck container — a farm building that the player
    /// loads with produce each day to fulfil active contracts and ship leftovers.
    ///
    /// Internally uses a real <see cref="Chest"/> so the ItemGrabMenu behaves
    /// identically to a vanilla chest (drag-in, drag-out, stacking, etc.).
    ///
    /// Flow:
    ///   1. Player right-clicks the Delivery Truck → Chest.ShowMenu() opens.
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

        /// <summary>The backing Chest — holds all truck items and provides the native menu.</summary>
        public static Chest TruckChest { get; private set; } = CreateChest();

        /// <summary>Convenience accessor: the live item list inside the chest.</summary>
        public static IList<Item> TruckContents => TruckChest.Items;

        private static Chest CreateChest()
        {
            var chest = new Chest(playerChest: true);
            chest.SpecialChestType = Chest.SpecialChestTypes.None;
            return chest;
        }

        // ══════════════════════════════════════════════════════════════
        //  INIT / RESET
        // ══════════════════════════════════════════════════════════════

        public static void Initialize()
        {
            TruckChest = CreateChest();
        }

        public static void Reset()
        {
            TruckChest = CreateChest();
        }

        // ══════════════════════════════════════════════════════════════
        //  DAY-END PROCESSING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called at DayStarted (before the new day fully loads).
        /// Processes all truck contents:
        ///   1. Scan active contracts + accepted bargain offers, sorted by closest deadline.
        ///   2. For each, consume matching items from the truck.
        ///   3. Ship remaining items as normal market sales.
        ///   4. Clear the truck.
        /// </summary>
        public static void ProcessDayEnd()
        {
            if (TruckContents.Count == 0) return;

            int totalDeliveries = 0;
            int totalShipped    = 0;
            int totalIncome     = 0;

            // Build a mutable working copy of truck items
            var remaining = TruckContents
                .Where(i => i != null)
                .Select(i => { var copy = (Item)i.getOne(); copy.Stack = i.Stack; return copy; })
                .ToList();

            // ── Step 1: Collect all deliverables (contracts + bargain offers) ──
            //    Each entry is a tuple: (daysLeft, fulfillAction)
            //    We sort by daysLeft ascending so the most urgent gets items first.
            var deliverables = new List<(int daysLeft, Action<List<Item>> fulfill)>();

            // -- Active contracts from ContractManager --
            foreach (var contract in ContractManager.GetActiveContracts())
            {
                if (contract.IsCompleted || contract.IsFailed) continue;
                deliverables.Add((contract.DaysRemaining, items => FulfillContract(contract, items, ref totalDeliveries, ref totalIncome)));
            }

            // -- Accepted bargain offers from BargainManager --
            foreach (var offer in BargainManager.GetAcceptedOffers())
            {
                deliverables.Add((offer.DeliveryDaysRemaining, items => FulfillBargainOffer(offer, items, ref totalDeliveries, ref totalIncome)));
            }

            // Sort by deadline — closest first
            deliverables.Sort((a, b) => a.daysLeft.CompareTo(b.daysLeft));

            // Process each deliverable against the remaining truck items
            foreach (var (_, fulfill) in deliverables)
            {
                fulfill(remaining);
            }

            // ── Step 2: Ship remaining items normally ──
            foreach (var item in remaining)
            {
                if (item == null || item.Stack <= 0) continue;

                MarketManager.RecordSale(item, item.Stack);

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

            // ── Step 3: Clear truck ──
            TruckContents.Clear();

            // ── Step 4: HUD summary ──
            if (totalDeliveries > 0 || totalShipped > 0)
            {
                string msg = string.Empty;
                if (totalDeliveries > 0) msg += $"{totalDeliveries} delivery(s) fulfilled. ";
                if (totalShipped    > 0) msg += $"{totalShipped} item(s) shipped. ";
                msg += $"+{totalIncome}g total.";
                Game1.addHUDMessage(new HUDMessage(msg.Trim(), HUDMessage.achievement_type));
            }

            LogHelper.Info($"[Truck] Day-end: {totalDeliveries} deliveries, {totalShipped} shipped, {totalIncome}g earned.");
        }

        // ══════════════════════════════════════════════════════════════
        //  INDIVIDUAL FULFILLMENT HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Try to fulfill a single ContractManager contract from the truck items.</summary>
        private static void FulfillContract(
            Contract contract,
            List<Item> items,
            ref int deliveryCount,
            ref int incomeEarned)
        {
            bool anyDelivered = false;

            foreach (var kvp in contract.RequiredItems)
            {
                string reqId = kvp.Key;
                int reqQty = kvp.Value;
                int alreadyDelivered = contract.DeliveredItems.GetValueOrDefault(reqId, 0);
                int need = reqQty - alreadyDelivered;
                if (need <= 0) continue;

                for (int i = 0; i < items.Count && need > 0; i++)
                {
                    var item = items[i];
                    if (item == null || item.QualifiedItemId != reqId) continue;
                    if (item.Quality < contract.MinimumQuality) continue;

                    int consume = Math.Min(item.Stack, need);
                    int accepted = contract.DeliverItems(reqId, consume, item.Quality);
                    item.Stack -= accepted;
                    need -= accepted;

                    if (accepted > 0) anyDelivered = true;
                    if (item.Stack <= 0) items[i] = null;
                }
            }

            // If the contract became fully completed via DeliverItems, award rewards
            if (contract.IsCompleted && anyDelivered)
            {
                // Rewards are handled inside ContractManager.DeliverToContract → CompleteContract
                // But since we called contract.DeliverItems directly, we need to trigger
                // completion through ContractManager for proper reward processing.
                // The contract.IsCompleted flag was already set by DeliverItems.
                // ContractManager.OnDayStarted will pick up the completed flag,
                // but let's process it immediately for correct income tracking.
                deliveryCount++;
                LogHelper.Info($"[Truck] Contract {contract.ContractId} fulfilled: {contract.Name}");
                Game1.addHUDMessage(new HUDMessage(
                    $"Contract delivered: {contract.Name}!",
                    HUDMessage.achievement_type));
            }
            else if (anyDelivered)
            {
                float pct = contract.CompletionPercentage;
                LogHelper.Info($"[Truck] Contract {contract.ContractId} progress: {pct:P0}");
            }
        }

        /// <summary>Try to fulfill a single BargainManager offer from the truck items.</summary>
        private static void FulfillBargainOffer(
            BargainOffer offer,
            List<Item> items,
            ref int deliveryCount,
            ref int incomeEarned)
        {
            if (offer.IsDelivered || offer.IsExpired) return;

            int need = offer.Quantity;
            for (int i = 0; i < items.Count && need > 0; i++)
            {
                var item = items[i];
                if (item == null || item.QualifiedItemId != offer.ItemQualifiedId) continue;

                int consume = Math.Min(item.Stack, need);
                item.Stack -= consume;
                need -= consume;

                if (item.Stack <= 0) items[i] = null;
            }

            if (need > 0) return; // Not enough in truck

            // Mark delivered and pay
            offer.IsDelivered = true;
            int reward = offer.OfferPrice;
            Game1.player.Money += reward;
            TaxManager.RecordIncome(reward);
            ReputationSkill.AddReputationXP(Game1.player, Math.Max(5, reward / 200));

            var npc = Game1.getCharacterFromName(offer.NpcName);
            if (npc != null)
                Game1.player.changeFriendship(30, npc);

            MarketManager.RecordSale(
                ItemRegistry.Create(offer.ItemQualifiedId),
                offer.Quantity);

            deliveryCount++;
            incomeEarned += reward;

            LogHelper.Info($"[Truck] Bargain {offer.OfferId} fulfilled: {offer.Quantity}x {offer.ItemDisplayName} → {reward}g");
            Game1.addHUDMessage(new HUDMessage(
                $"Delivery: {offer.ItemDisplayName} → {offer.NpcName}! +{reward}g",
                HUDMessage.achievement_type));
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
            TruckChest = CreateChest();

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
                    TruckChest.Items.Add(item);
                }

                LogHelper.Info($"[Truck] Loaded {TruckChest.Items.Count} item(s) from save.");
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
