using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// NPC patience class definitions for the Rubinstein Bargaining Model.
    /// </summary>
    public enum NpcPatienceClass
    {
        HighPatience,   // δ = 0.95 — Generous/Patient (Gus, Evelyn)
        Standard,       // δ = 0.85 — Rational (Robin, Clint)
        LowPatience     // δ = 0.70 — Frugal/Stubborn (Pierre, George)
    }

    /// <summary>
    /// Represents an active bargaining offer from an NPC.
    /// </summary>
    public class BargainOffer
    {
        public string OfferId { get; set; }
        public string NpcName { get; set; }
        public string ItemQualifiedId { get; set; }
        public string ItemDisplayName { get; set; }
        public int Quantity { get; set; }
        public int OfferPrice { get; set; }
        public int RoundNumber { get; set; } = 1;
        public int MaxRounds { get; set; } = 3;
        public int CreatedDay { get; set; }
        public int DeliveryDeadlineDays { get; set; } = 2;
        public bool IsAccepted { get; set; } = false;
        public bool IsRejected { get; set; } = false;
        public bool IsExpired { get; set; } = false;
        public bool IsDelivered { get; set; } = false;

        /// <summary>The NPC's maximum willingness to pay.</summary>
        public int WillingnessToPay { get; set; }

        /// <summary>The calculated equilibrium price.</summary>
        public int EquilibriumPrice { get; set; }

        /// <summary>Days remaining for delivery (if accepted).</summary>
        [JsonIgnore]
        public int DeliveryDaysRemaining => IsAccepted ? Math.Max(0, DeliveryDeadlineDays - (Game1.Date.TotalDays - CreatedDay)) : -1;
    }

    /// <summary>
    /// Implements the Social Marketplace bargaining system based on the
    /// Rubinstein Alternating-Offer Bargaining Model.
    ///
    /// NPCs make offers for items based on:
    /// - Their willingness to pay (WTP) derived from item base price and market saturation
    /// - Their patience factor (δ) based on personality
    /// - Friendship level modifier
    /// - Stubbornness coefficient (k) for counter-offer acceptance
    /// </summary>
    public class BargainManager
    {
        /// <summary>Active offers on the social marketplace.</summary>
        private static List<BargainOffer> _activeOffers = new();

        /// <summary>NPC cooldown tracking. Key = NPC name, Value = day cooldown expires.</summary>
        private static Dictionary<string, int> _npcCooldowns = new();

        /// <summary>Random for bargaining calculations.</summary>
        private static Random _bargainRng;

        /// <summary>Counter for offer IDs.</summary>
        private static int _nextOfferId = 1;

        // ── NPC patience classifications ──
        private static readonly Dictionary<string, NpcPatienceClass> NpcPatience = new()
        {
            // High Patience (δ = 0.95)
            ["Gus"] = NpcPatienceClass.HighPatience,
            ["Evelyn"] = NpcPatienceClass.HighPatience,
            ["Willy"] = NpcPatienceClass.HighPatience,
            ["Linus"] = NpcPatienceClass.HighPatience,
            ["Emily"] = NpcPatienceClass.HighPatience,

            // Standard (δ = 0.85)
            ["Robin"] = NpcPatienceClass.Standard,
            ["Clint"] = NpcPatienceClass.Standard,
            ["Harvey"] = NpcPatienceClass.Standard,
            ["Leah"] = NpcPatienceClass.Standard,
            ["Shane"] = NpcPatienceClass.Standard,
            ["Demetrius"] = NpcPatienceClass.Standard,
            ["Marnie"] = NpcPatienceClass.Standard,
            ["Lewis"] = NpcPatienceClass.Standard,
            ["Caroline"] = NpcPatienceClass.Standard,
            ["Jodi"] = NpcPatienceClass.Standard,

            // Low Patience (δ = 0.70)
            ["Pierre"] = NpcPatienceClass.LowPatience,
            ["George"] = NpcPatienceClass.LowPatience,
            ["Morris"] = NpcPatienceClass.LowPatience,
            ["Haley"] = NpcPatienceClass.LowPatience,
        };

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static void Initialize()
        {
            _bargainRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays * 17);
        }

        // ══════════════════════════════════════════════════════════════
        //  DAILY UPDATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>Called at DayStarted to generate new NPC offers and expire old ones.</summary>
        public static void OnDayStarted()
        {
            _bargainRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays * 17);

            // Expire old offers
            foreach (var offer in _activeOffers)
            {
                if (!offer.IsAccepted && !offer.IsRejected && !offer.IsExpired)
                {
                    // Offers expire after 1 day if not responded to
                    if (Game1.Date.TotalDays - offer.CreatedDay >= 1)
                    {
                        offer.IsExpired = true;
                    }
                }

                // Check delivery deadline for accepted offers
                if (offer.IsAccepted && !offer.IsDelivered && offer.DeliveryDaysRemaining <= 0)
                {
                    offer.IsExpired = true;
                    LogHelper.Warn($"[Bargain] Delivery deadline missed for {offer.NpcName}'s offer on {offer.ItemDisplayName}!");
                }
            }

            // Clean up expired/completed offers
            _activeOffers.RemoveAll(o => o.IsExpired || o.IsDelivered || o.IsRejected);

            // Generate new offers from NPCs (1-3 per day)
            int offerCount = 1 + _bargainRng.Next(3);
            for (int i = 0; i < offerCount; i++)
            {
                GenerateRandomOffer();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  OFFER GENERATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>Generate a random NPC offer based on current market conditions.</summary>
        private static void GenerateRandomOffer()
        {
            // Pick an NPC who isn't on cooldown
            var availableNpcs = NpcPatience.Keys
                .Where(n => !_npcCooldowns.ContainsKey(n) || _npcCooldowns[n] <= Game1.Date.TotalDays)
                .ToList();

            if (availableNpcs.Count == 0) return;

            string npcName = availableNpcs[_bargainRng.Next(availableNpcs.Count)];

            // Check if the NPC exists in this save
            var npc = Game1.getCharacterFromName(npcName);
            if (npc == null) return;

            // Pick a random item the player has in their inventory or chests
            var playerItems = GetPlayerAvailableItems();
            if (playerItems.Count == 0) return;

            var targetItem = playerItems[_bargainRng.Next(playerItems.Count)];

            // Calculate WTP based on item price and market saturation
            int vanillaPrice = targetItem.sellToStorePrice(-1L);
            // WTP for bargaining should be much higher than the nerfed free-sell price
            // It's based on vanilla price modified by saturation
            float saturation = MarketManager.GetRawSaturationMultiplier(targetItem);
            int wtp = Math.Max(1, (int)(vanillaPrice * (10.0 / ModConfig.GetInstance().GlobalSellMultiplier) * saturation));

            if (wtp <= vanillaPrice) return; // Not worth offering

            // Calculate patience factor
            float delta = GetPatienceFactor(npcName);

            // Friendship modifier (higher friendship = more cooperative)
            int friendshipPoints = Game1.player.getFriendshipLevelForNPC(npcName);
            float friendshipModifier = 1.0f + (friendshipPoints / 2500f) * 0.3f;

            // First offer: O1 = WTP * (1 - δ) / (1 - δ²)
            float firstOffer = wtp * (1f - delta) / (1f - delta * delta);
            firstOffer *= friendshipModifier;

            // Equilibrium price (midpoint adjusted by friendship)
            int equilibrium = (int)((wtp + vanillaPrice) / 2f * friendshipModifier);

            // Determine quantity (1-5 based on what player has)
            int maxQty = Math.Min(targetItem.Stack, 5);
            int quantity = Math.Max(1, _bargainRng.Next(1, maxQty + 1));

            var offer = new BargainOffer
            {
                OfferId = $"BRG_{_nextOfferId++}",
                NpcName = npcName,
                ItemQualifiedId = targetItem.QualifiedItemId,
                ItemDisplayName = targetItem.DisplayName,
                Quantity = quantity,
                OfferPrice = Math.Max(1, (int)(firstOffer * quantity)),
                RoundNumber = 1,
                MaxRounds = 3,
                CreatedDay = Game1.Date.TotalDays,
                DeliveryDeadlineDays = ModConfig.GetInstance().BargainingDeliveryDays,
                WillingnessToPay = wtp * quantity,
                EquilibriumPrice = equilibrium * quantity,
            };

            _activeOffers.Add(offer);
            LogHelper.Debug($"[Bargain] {npcName} offers {offer.OfferPrice}g for {quantity}x {targetItem.DisplayName} (WTP: {wtp * quantity}g)");
        }

        /// <summary>Get items available in player's inventory for bargaining.</summary>
        private static List<Item> GetPlayerAvailableItems()
        {
            var items = new List<Item>();

            foreach (var item in Game1.player.Items)
            {
                if (item != null && item is StardewValley.Object obj && obj.canBeShipped() && obj.sellToStorePrice(-1L) > 0)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        // ══════════════════════════════════════════════════════════════
        //  PLAYER ACTIONS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Accept an NPC's offer. Creates a delivery quest with a 2-day timer.
        /// </summary>
        public static bool AcceptOffer(string offerId)
        {
            var offer = _activeOffers.FirstOrDefault(o => o.OfferId == offerId);
            if (offer == null || offer.IsAccepted || offer.IsRejected || offer.IsExpired) return false;

            offer.IsAccepted = true;
            offer.CreatedDay = Game1.Date.TotalDays; // Reset timer for delivery

            LogHelper.Info($"[Bargain] Accepted {offer.NpcName}'s offer: {offer.OfferPrice}g for {offer.Quantity}x {offer.ItemDisplayName}");
            Game1.addHUDMessage(new HUDMessage(
                $"Deal! Deliver {offer.Quantity}x {offer.ItemDisplayName} to {offer.NpcName} within {offer.DeliveryDeadlineDays} days for {offer.OfferPrice}g.",
                HUDMessage.achievement_type));
            return true;
        }

        /// <summary>
        /// Reject an NPC's offer. Imposes a cooldown on that NPC.
        /// </summary>
        public static void RejectOffer(string offerId)
        {
            var offer = _activeOffers.FirstOrDefault(o => o.OfferId == offerId);
            if (offer == null) return;

            offer.IsRejected = true;

            // Apply cooldown
            int cooldownHours = ModConfig.GetInstance().BargainingCooldownHours;
            int cooldownDays = cooldownHours / 24;
            _npcCooldowns[offer.NpcName] = Game1.Date.TotalDays + cooldownDays;

            LogHelper.Info($"[Bargain] Rejected {offer.NpcName}'s offer. Cooldown: {cooldownDays} days.");
        }

        /// <summary>
        /// Counter-offer a higher price. NPC may accept, reject, or make a final offer.
        /// Returns: 1 = accepted, 0 = rejected (NPC walks away), -1 = NPC makes final offer
        /// </summary>
        public static int CounterOffer(string offerId, int counterPrice)
        {
            var offer = _activeOffers.FirstOrDefault(o => o.OfferId == offerId);
            if (offer == null || offer.IsAccepted || offer.IsRejected || offer.IsExpired) return 0;

            var config = ModConfig.GetInstance();
            float k = (float)config.BargainingStubbornness;

            // Probability of acceptance: P_acc = 1 / (1 + e^(k * (C - E)))
            float distanceFromEquilibrium = (counterPrice - offer.EquilibriumPrice) / (float)Math.Max(1, offer.EquilibriumPrice);
            float exponent = k * distanceFromEquilibrium * 10f; // Scale factor
            float pAccept = 1f / (1f + (float)Math.Exp(exponent));

            // Friendship modifier increases acceptance chance
            int friendship = Game1.player.getFriendshipLevelForNPC(offer.NpcName);
            pAccept += (friendship / 2500f) * 0.15f;
            pAccept = Math.Clamp(pAccept, 0.05f, 0.95f);

            double roll = _bargainRng.NextDouble();

            if (roll < pAccept)
            {
                // NPC accepts the counter-offer
                offer.OfferPrice = counterPrice;
                offer.IsAccepted = true;
                offer.CreatedDay = Game1.Date.TotalDays;

                LogHelper.Info($"[Bargain] {offer.NpcName} ACCEPTED counter-offer: {counterPrice}g (P={pAccept:F2})");
                Game1.addHUDMessage(new HUDMessage(
                    $"{offer.NpcName} accepts your price of {counterPrice}g! Deliver within {offer.DeliveryDeadlineDays} days.",
                    HUDMessage.achievement_type));
                return 1;
            }
            else if (offer.RoundNumber < offer.MaxRounds)
            {
                // NPC makes a discounted final offer
                float delta = GetPatienceFactor(offer.NpcName);
                offer.OfferPrice = (int)(offer.OfferPrice * delta);
                offer.RoundNumber++;

                LogHelper.Info($"[Bargain] {offer.NpcName} COUNTERS with: {offer.OfferPrice}g (round {offer.RoundNumber})");
                Game1.addHUDMessage(new HUDMessage(
                    $"{offer.NpcName} counters: {offer.OfferPrice}g (round {offer.RoundNumber}/{offer.MaxRounds})",
                    HUDMessage.error_type));
                return -1;
            }
            else
            {
                // NPC walks away
                offer.IsRejected = true;
                int cooldownDays = config.BargainingCooldownHours / 24;
                _npcCooldowns[offer.NpcName] = Game1.Date.TotalDays + cooldownDays;

                LogHelper.Info($"[Bargain] {offer.NpcName} WALKS AWAY. Cooldown: {cooldownDays} days.");
                Game1.addHUDMessage(new HUDMessage(
                    $"{offer.NpcName} walks away from the deal. No offers for {cooldownDays} days.",
                    HUDMessage.error_type));
                return 0;
            }
        }

        /// <summary>
        /// Deliver items for an accepted offer. Returns gold earned.
        /// </summary>
        public static int DeliverOffer(string offerId, Item item, int quantity)
        {
            var offer = _activeOffers.FirstOrDefault(o => o.OfferId == offerId);
            if (offer == null || !offer.IsAccepted || offer.IsDelivered || offer.IsExpired) return 0;

            if (item.QualifiedItemId != offer.ItemQualifiedId) return 0;
            if (quantity < offer.Quantity) return 0;

            offer.IsDelivered = true;
            int reward = offer.OfferPrice;
            Game1.player.Money += reward;

            // Track income for taxes
            TaxManager.RecordIncome(reward);

            // Award small reputation XP
            ReputationSkill.AddReputationXP(Game1.player, Math.Max(5, reward / 200));

            // Friendship bonus for successful trade
            var npc = Game1.getCharacterFromName(offer.NpcName);
            if (npc != null)
            {
                Game1.player.changeFriendship(30, npc);
            }

            // Add volume to market
            MarketManager.RecordSale(item, quantity);

            LogHelper.Info($"[Bargain] Delivered {quantity}x {offer.ItemDisplayName} to {offer.NpcName} for {reward}g!");
            Game1.addHUDMessage(new HUDMessage(
                $"Delivered to {offer.NpcName}! +{reward}g",
                HUDMessage.achievement_type));

            return reward;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Get the patience factor (δ) for an NPC.</summary>
        public static float GetPatienceFactor(string npcName)
        {
            var patienceClass = NpcPatience.GetValueOrDefault(npcName, NpcPatienceClass.Standard);
            float factor = patienceClass switch
            {
                NpcPatienceClass.HighPatience => 0.95f,
                NpcPatienceClass.Standard => 0.85f,
                NpcPatienceClass.LowPatience => 0.70f,
                _ => 0.85f,
            };

            // Reputation Level 7 perk: +0.05 patience bonus (NPCs are more willing to wait)
            if (ReputationSkill.GetLevel(Game1.player) >= 7)
                factor += 0.05f;

            // Cap at 0.99 to preserve Rubinstein model convergence
            return Math.Min(factor, 0.99f);
        }

        /// <summary>Get the patience class for an NPC.</summary>
        public static NpcPatienceClass GetPatienceClass(string npcName)
        {
            return NpcPatience.GetValueOrDefault(npcName, NpcPatienceClass.Standard);
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════════

        public static IReadOnlyList<BargainOffer> GetActiveOffers() => _activeOffers.AsReadOnly();

        public static IReadOnlyList<BargainOffer> GetPendingOffers() =>
            _activeOffers.Where(o => !o.IsAccepted && !o.IsRejected && !o.IsExpired).ToList().AsReadOnly();

        public static IReadOnlyList<BargainOffer> GetAcceptedOffers() =>
            _activeOffers.Where(o => o.IsAccepted && !o.IsDelivered && !o.IsExpired).ToList().AsReadOnly();

        // ══════════════════════════════════════════════════════════════
        //  DELIVERY TRUCK INTEGRATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempt to fulfil accepted bargain offers using items from the delivery truck.
        /// Offers are processed urgency-first (fewest delivery days remaining first).
        ///
        /// Returns the items that were NOT consumed by any contract (leftover produce for
        /// normal shipping). Also outputs counts for the HUD summary.
        /// </summary>
        public static List<Item> FulfillFromTruck(
            List<Item> truckItems,
            out int contractsFulfilled,
            out int incomeEarned)
        {
            contractsFulfilled = 0;
            incomeEarned = 0;

            // Mutable working copy — we reduce stacks as offers are satisfied
            var remaining = truckItems.Select(i => (Item)i.getOne()).ToList();
            for (int idx = 0; idx < remaining.Count; idx++)
                remaining[idx].Stack = truckItems[idx].Stack;

            // Sort accepted offers by urgency (deadline closest first)
            var pending = _activeOffers
                .Where(o => o.IsAccepted && !o.IsDelivered && !o.IsExpired)
                .OrderBy(o => o.DeliveryDaysRemaining)
                .ToList();

            foreach (var offer in pending)
            {
                // Find a matching item in the truck
                int need = offer.Quantity;
                for (int i = 0; i < remaining.Count && need > 0; i++)
                {
                    var item = remaining[i];
                    if (item == null || item.QualifiedItemId != offer.ItemQualifiedId) continue;

                    int consume = Math.Min(item.Stack, need);
                    item.Stack -= consume;
                    need -= consume;

                    if (item.Stack <= 0)
                        remaining[i] = null;
                }

                if (need > 0) continue; // not enough in the truck for this offer

                // Mark delivered and pay out
                offer.IsDelivered = true;
                int reward = offer.OfferPrice;
                Game1.player.Money += reward;
                TaxManager.RecordIncome(reward);
                ReputationSkill.AddReputationXP(Game1.player, Math.Max(5, reward / 200));

                var npc = Game1.getCharacterFromName(offer.NpcName);
                if (npc != null)
                    Game1.player.changeFriendship(30, npc);

                contractsFulfilled++;
                incomeEarned += reward;

                MarketManager.RecordSale(
                    ItemRegistry.Create(offer.ItemQualifiedId),
                    offer.Quantity);

                LogHelper.Info($"[Truck] Fulfilled offer {offer.OfferId}: {offer.Quantity}x {offer.ItemDisplayName} → {reward}g");
                Game1.addHUDMessage(new HUDMessage(
                    $"Delivery: {offer.ItemDisplayName} → {offer.NpcName}! +{reward}g",
                    HUDMessage.achievement_type));
            }

            // Return non-null, non-empty leftovers
            return remaining.Where(i => i != null && i.Stack > 0).ToList();
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            var data = new BargainSaveData
            {
                ActiveOffers = _activeOffers,
                NpcCooldowns = _npcCooldowns,
                NextOfferId = _nextOfferId,
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public static void Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _activeOffers = new List<BargainOffer>();
                _npcCooldowns = new Dictionary<string, int>();
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<BargainSaveData>(json);
                _activeOffers = data?.ActiveOffers ?? new List<BargainOffer>();
                _npcCooldowns = data?.NpcCooldowns ?? new Dictionary<string, int>();
                _nextOfferId = data?.NextOfferId ?? 1;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Bargain] Failed to deserialize:\n{ex}");
                _activeOffers = new List<BargainOffer>();
                _npcCooldowns = new Dictionary<string, int>();
            }
        }

        public static void Reset()
        {
            _activeOffers.Clear();
            _npcCooldowns.Clear();
            _nextOfferId = 1;
        }

        private class BargainSaveData
        {
            public List<BargainOffer> ActiveOffers { get; set; }
            public Dictionary<string, int> NpcCooldowns { get; set; }
            public int NextOfferId { get; set; }
        }
    }
}
