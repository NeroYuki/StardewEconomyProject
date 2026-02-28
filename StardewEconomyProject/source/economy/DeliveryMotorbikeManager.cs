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
    /// Manages the Delivery Motorbike — a smaller, cheaper alternative to the
    /// Delivery Truck that can only deliver to regional (Pelican Town) customers.
    ///
    /// It CANNOT fulfil contracts from Zuzu City or International NPCs.
    /// Bargain offers are always regional, so the motorbike can fulfil those.
    /// Remaining items are shipped to market as normal.
    ///
    /// Internally uses a real <see cref="Chest"/> identical to the truck.
    /// </summary>
    public static class DeliveryMotorbikeManager
    {
        public const int MotorbikeSlots = 18; // Smaller than the truck's 36 (Doesnt work)

        /// <summary>The backing Chest for motorbike cargo.</summary>
        public static Chest MotorbikeChest { get; private set; } = CreateChest();

        /// <summary>Live item list inside the chest.</summary>
        public static IList<Item> MotorbikeContents => MotorbikeChest.Items;

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
            MotorbikeChest = CreateChest();
        }

        public static void Reset()
        {
            MotorbikeChest = CreateChest();
        }

        // ══════════════════════════════════════════════════════════════
        //  DAY-END PROCESSING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Process motorbike cargo at day start.
        /// Only regional contracts (Pelican Town NPCs) and bargain offers are eligible.
        /// Zuzu City / International contracts are skipped.
        /// </summary>
        public static void ProcessDayEnd()
        {
            if (MotorbikeContents.Count == 0) return;

            int totalDeliveries = 0;
            int totalShipped    = 0;
            int totalIncome     = 0;

            // Build a mutable working copy
            var remaining = MotorbikeContents
                .Where(i => i != null)
                .Select(i => { var copy = (Item)i.getOne(); copy.Stack = i.Stack; return copy; })
                .ToList();

            // ── Step 1: Collect regional-only deliverables ──
            var deliverables = new List<(int daysLeft, Action<List<Item>> fulfill)>();

            // Regional contracts only (Pelican Town NPCs)
            foreach (var contract in ContractManager.GetActiveContracts())
            {
                if (contract.IsCompleted || contract.IsFailed) continue;
                if (!ContractManager.IsRegionalContract(contract)) continue;

                deliverables.Add((contract.DaysRemaining, items =>
                    FulfillContract(contract, items, ref totalDeliveries, ref totalIncome)));
            }

            // Bargain offers (always regional / local NPCs)
            foreach (var offer in BargainManager.GetAcceptedOffers())
            {
                deliverables.Add((offer.DeliveryDaysRemaining, items =>
                    FulfillBargainOffer(offer, items, ref totalDeliveries, ref totalIncome)));
            }

            // Sort by deadline — closest first
            deliverables.Sort((a, b) => a.daysLeft.CompareTo(b.daysLeft));

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

            // ── Step 3: Clear chest ──
            MotorbikeContents.Clear();

            // ── Step 4: HUD summary ──
            if (totalDeliveries > 0 || totalShipped > 0)
            {
                string msg = string.Empty;
                if (totalDeliveries > 0) msg += $"{totalDeliveries} local delivery(s). ";
                if (totalShipped    > 0) msg += $"{totalShipped} item(s) shipped. ";
                msg += $"+{totalIncome}g total.";
                Game1.addHUDMessage(new HUDMessage(msg.Trim(), HUDMessage.achievement_type));
            }

            LogHelper.Info($"[Motorbike] Day-end: {totalDeliveries} deliveries, {totalShipped} shipped, {totalIncome}g earned.");
        }

        // ══════════════════════════════════════════════════════════════
        //  INDIVIDUAL FULFILLMENT HELPERS
        //  (Identical logic to DeliveryTruckManager)
        // ══════════════════════════════════════════════════════════════

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

            if (contract.IsCompleted && anyDelivered)
            {
                deliveryCount++;
                LogHelper.Info($"[Motorbike] Contract {contract.ContractId} fulfilled: {contract.Name}");
                Game1.addHUDMessage(new HUDMessage(
                    $"Motorbike delivery: {contract.Name}!",
                    HUDMessage.achievement_type));
            }
            else if (anyDelivered)
            {
                float pct = contract.CompletionPercentage;
                LogHelper.Info($"[Motorbike] Contract {contract.ContractId} progress: {pct:P0}");
            }
        }

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

            if (need > 0) return; // Not enough

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

            LogHelper.Info($"[Motorbike] Bargain {offer.OfferId} fulfilled: {offer.Quantity}x {offer.ItemDisplayName} → {reward}g");
            Game1.addHUDMessage(new HUDMessage(
                $"Motorbike delivery: {offer.ItemDisplayName} → {offer.NpcName}! +{reward}g",
                HUDMessage.achievement_type));
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            var dtos = MotorbikeContents
                .Where(i => i != null)
                .Select(i => new MotorbikeItemDto
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
            MotorbikeChest = CreateChest();

            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var dtos = JsonConvert.DeserializeObject<List<MotorbikeItemDto>>(json);
                if (dtos == null) return;

                foreach (var dto in dtos)
                {
                    var item = ItemRegistry.Create(dto.QualifiedItemId, dto.Stack);
                    if (item == null) continue;
                    if (item is SObject obj)
                        obj.Quality = dto.Quality;
                    MotorbikeChest.Items.Add(item);
                }

                LogHelper.Info($"[Motorbike] Loaded {MotorbikeChest.Items.Count} item(s) from save.");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Motorbike] Failed to deserialize save data:\n{ex}");
            }
        }

        private class MotorbikeItemDto
        {
            public string QualifiedItemId { get; set; }
            public int Quality { get; set; }
            public int Stack   { get; set; }
        }
    }
}
