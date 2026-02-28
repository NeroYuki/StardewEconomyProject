using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Represents a single bulk order contract.
    /// Contracts bypass the GlobalSellMultiplier and offer premium pricing
    /// in exchange for specific logistical demands.
    /// </summary>
    public class Contract
    {
        /// <summary>Unique identifier for this contract.</summary>
        public string ContractId { get; set; }

        /// <summary>Display name for the contract.</summary>
        public string Name { get; set; }

        /// <summary>Description of what the requester needs.</summary>
        public string Description { get; set; }

        /// <summary>NPC who requested this contract.</summary>
        public string RequesterNpc { get; set; }

        /// <summary>Items required for fulfillment. Key = qualified item ID, Value = quantity needed.</summary>
        public Dictionary<string, int> RequiredItems { get; set; } = new();

        /// <summary>Items delivered so far. Key = qualified item ID, Value = quantity delivered.</summary>
        public Dictionary<string, int> DeliveredItems { get; set; } = new();

        /// <summary>Minimum quality tier required (0=any, 1=silver, 2=gold, 4=iridium).</summary>
        public int MinimumQuality { get; set; } = 0;

        /// <summary>Total days from acceptance until deadline.</summary>
        public int DurationDays { get; set; } = 7;

        /// <summary>Day the contract was accepted (Game1.Date.TotalDays).</summary>
        public int AcceptedDay { get; set; } = -1;

        /// <summary>The gold reward for full completion.</summary>
        public int BaseReward { get; set; }

        /// <summary>Contract premium multiplier applied to reward.</summary>
        public double PremiumMultiplier { get; set; } = 1.35;

        /// <summary>Friendship points awarded on completion.</summary>
        public int FriendshipReward { get; set; } = 150;

        /// <summary>Friendship points lost on failure.</summary>
        public int FriendshipPenalty { get; set; } = -30;

        /// <summary>The market category this contract targets.</summary>
        public string MarketCategory { get; set; }

        /// <summary>Whether this contract has been accepted by the player.</summary>
        public bool IsAccepted { get; set; } = false;

        /// <summary>Whether this contract has been completed.</summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>Whether this contract has expired/failed.</summary>
        public bool IsFailed { get; set; } = false;

        // ══════════════════════════════════════════════════════════════
        //  COMPUTED PROPERTIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Days remaining until deadline. -1 if not accepted.</summary>
        [JsonIgnore]
        public int DaysRemaining
        {
            get
            {
                if (AcceptedDay < 0) return DurationDays;
                int elapsed = Game1.Date.TotalDays - AcceptedDay;
                return Math.Max(0, DurationDays - elapsed);
            }
        }

        /// <summary>Is the contract past its deadline?</summary>
        [JsonIgnore]
        public bool IsExpired => AcceptedDay >= 0 && DaysRemaining <= 0;

        /// <summary>
        /// Total reward with premium: R = (Σ Qi * Pvanilla_i) * M_contract
        /// </summary>
        [JsonIgnore]
        public int TotalReward => (int)(BaseReward * PremiumMultiplier);

        /// <summary>
        /// Completion percentage (0.0 to 1.0).
        /// </summary>
        [JsonIgnore]
        public float CompletionPercentage
        {
            get
            {
                if (RequiredItems.Count == 0) return 1.0f;
                int totalRequired = RequiredItems.Values.Sum();
                int totalDelivered = 0;
                foreach (var kvp in RequiredItems)
                {
                    totalDelivered += DeliveredItems.TryGetValue(kvp.Key, out int delivered)
                        ? Math.Min(delivered, kvp.Value) : 0;
                }
                return totalRequired > 0 ? (float)totalDelivered / totalRequired : 0f;
            }
        }

        /// <summary>
        /// Penalty for breach: L = R_base * M_penalty * (Q_required - Q_delivered) / Q_required
        /// </summary>
        [JsonIgnore]
        public int BreachPenalty
        {
            get
            {
                float unfulfilled = 1.0f - CompletionPercentage;
                var config = ModConfig.GetInstance();
                return (int)(BaseReward * config.ContractPenaltyMultiplier * unfulfilled);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  METHODS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempt to deliver items toward this contract.
        /// Returns the number of items actually accepted.
        /// </summary>
        public int DeliverItems(string qualifiedItemId, int quantity, int itemQuality)
        {
            if (!IsAccepted || IsCompleted || IsFailed) return 0;

            // Quality gate
            if (itemQuality < MinimumQuality) return 0;

            if (!RequiredItems.TryGetValue(qualifiedItemId, out int required)) return 0;

            if (!DeliveredItems.ContainsKey(qualifiedItemId))
                DeliveredItems[qualifiedItemId] = 0;

            int remaining = required - DeliveredItems[qualifiedItemId];
            int accepted = Math.Min(quantity, remaining);
            DeliveredItems[qualifiedItemId] += accepted;

            // Check if fully completed
            if (CompletionPercentage >= 1.0f)
            {
                IsCompleted = true;
            }

            return accepted;
        }

        /// <summary>
        /// Check deadline and apply penalties if expired.
        /// Returns the penalty amount (0 if not expired or already processed).
        /// </summary>
        public int CheckDeadline()
        {
            if (!IsAccepted || IsCompleted || IsFailed) return 0;

            if (IsExpired)
            {
                IsFailed = true;
                return BreachPenalty;
            }

            return 0;
        }

        public override string ToString()
        {
            string status = IsCompleted ? "COMPLETED" : IsFailed ? "FAILED" : IsAccepted ? $"{DaysRemaining}d left" : "Available";
            return $"[{ContractId}] {Name} — {status} ({CompletionPercentage:P0})";
        }
    }

    /// <summary>
    /// Manages the contract board: generation, tracking, and resolution.
    /// </summary>
    public class ContractManager
    {
        /// <summary>Available contracts on the board (not yet accepted).</summary>
        private static List<Contract> _availableContracts = new();

        /// <summary>Currently active (accepted) contracts.</summary>
        private static List<Contract> _activeContracts = new();

        /// <summary>Completed/failed contracts history.</summary>
        private static List<Contract> _completedContracts = new();

        /// <summary>Day the board was last refreshed.</summary>
        private static int _lastRefreshDay = -1;

        /// <summary>Counter for generating unique contract IDs.</summary>
        private static int _nextContractId = 1;

        /// <summary>Random instance for contract generation.</summary>
        private static Random _contractRng;

        // ── Pelican Town NPC list (for LocalMerchant profession check) ──
        private static readonly HashSet<string> PelicanTownNpcs = new()
        {
            "Pierre", "Gus", "Willy", "Robin", "Clint", "Caroline",
            "Evelyn", "Harvey", "Marnie", "Emily", "Lewis", "Demetrius"
        };

        /// <summary>
        /// Returns true if the contract's requester is a Pelican Town (regional) NPC.
        /// Used by the Delivery Motorbike which can only serve local customers.
        /// </summary>
        public static bool IsRegionalContract(Contract contract)
        {
            return contract?.RequesterNpc != null
                && PelicanTownNpcs.Contains(contract.RequesterNpc);
        }

        // ── Zuzu City NPC list (for RegionalMogul profession, National tier Lv5+) ──
        private static readonly HashSet<string> ZuzuCityNpcs = new()
        {
            "Director Chen", "Commissioner Hayes",
            "Zuzu Imports Co.", "Metropolitan Catering", "GreenLeaf Distributors"
        };

        // ── International NPC list (for GlobalExporter profession, International tier Lv10) ──
        private static readonly HashSet<string> InternationalNpcs = new()
        {
            "Gotoro Merchant Guild", "Desert Trade Consortium",
            "Ferngill Export Authority", "Overseas Trading Company"
        };

        // ── NPC contract requesters with personality categories ──
        // Pelican Town (Regional — always available)
        private static readonly Dictionary<string, string[]> NpcContractors = new()
        {
            ["Pierre"] = new[] { "Vegetable", "Fruit" },
            ["Gus"] = new[] { "Vegetable", "Fish", "AnimalProduct" },
            ["Willy"] = new[] { "Fish" },
            ["Robin"] = new[] { "Default" },
            ["Clint"] = new[] { "Mineral" },
            ["Caroline"] = new[] { "Flower" },
            ["Evelyn"] = new[] { "Cooking", "Flower" },
            ["Harvey"] = new[] { "Vegetable", "Fruit" },
            ["Marnie"] = new[] { "AnimalProduct" },
            ["Emily"] = new[] { "Flower", "Forage" },
            ["Lewis"] = new[] { "Vegetable", "Fruit", "ArtisanGoods" },
            ["Demetrius"] = new[] { "Fruit", "Forage" },
        };

        // Zuzu City (National — requires Lv5+ and RegionalMogul profession)
        private static readonly Dictionary<string, string[]> ZuzuCityContractors = new()
        {
            ["Director Chen"] = new[] { "ArtisanGoods", "Cooking" },
            ["Commissioner Hayes"] = new[] { "Vegetable", "Fruit", "Flower" },
            ["Zuzu Imports Co."] = new[] { "Fish", "Mineral" },
            ["Metropolitan Catering"] = new[] { "Cooking", "AnimalProduct" },
            ["GreenLeaf Distributors"] = new[] { "Vegetable", "Forage", "Fruit" },
        };

        // International (International — requires Lv10 and GlobalExporter profession)
        private static readonly Dictionary<string, string[]> InternationalContractors = new()
        {
            ["Gotoro Merchant Guild"] = new[] { "Mineral", "ArtisanGoods" },
            ["Desert Trade Consortium"] = new[] { "Flower", "Fruit" },
            ["Ferngill Export Authority"] = new[] { "Vegetable", "AnimalProduct", "Fish" },
            ["Overseas Trading Company"] = new[] { "ArtisanGoods", "Cooking", "Default" },
        };

        // ── Item pools are now built dynamically by SeasonalItemRegistry ──
        // Crops + fruit trees: seasonal data from Data/Crops and Data/FruitTrees
        // Fish: seasonal data from Data/Locations fish entries
        // Non-seasonal categories (AnimalProduct, ArtisanGoods, Mineral, etc.): static pools

        // ══════════════════════════════════════════════════════════════
        //  SEASON / PROGRESSION HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns a 0-based progression index: Spring Y1 = 0, Winter Y3 = 11.
        /// After Y3 it keeps climbing but the scaling formula is clamped elsewhere.
        /// </summary>
        private static int GetProgressionIndex()
        {
            int seasonIdx = Game1.currentSeason switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall"   => 2,
                _        => 3, // winter
            };
            return (Game1.year - 1) * 4 + seasonIdx;
        }

        /// <summary>
        /// Returns the quantity multiplier for contracts / bargains.
        /// Spring Y1 (idx 0) → 1x .. Winter Y3 (idx 11) → ~100x, exponential curve.
        /// Formula: multiplier = baseMin + (baseMax - baseMin) * (idx / maxIdx)^exponent
        /// Then multiplied by the config scalar.
        ///
        /// At progression 0  → ~1–3 (random low end)
        /// At progression 11 → ~50–120 (random high end)
        /// </summary>
        private static int GetProgressionQuantity(Random rng, double configScale)
        {
            int idx = GetProgressionIndex();
            // Clamp to 0-11 for the core curve, allow slight growth beyond Y3
            float t = Math.Min(idx, 15) / 11f;

            // Exponential curve:  low end for early game, explodes toward late game
            // At t=0: range = [1, 5]   |  At t=1.0: range = [60, 120]
            double lowEnd  = 1.0 + 59.0 * Math.Pow(t, 2.0);
            double highEnd = 5.0 + 115.0 * Math.Pow(t, 2.0);

            double raw = lowEnd + rng.NextDouble() * (highEnd - lowEnd);
            int quantity = Math.Max(1, (int)(raw * configScale));
            return quantity;
        }

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static void Initialize()
        {
            _contractRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays * 31);
            LogHelper.Info("[Contracts] Contract manager initialized.");
        }

        // ══════════════════════════════════════════════════════════════
        //  DAILY UPDATE
        // ══════════════════════════════════════════════════════════════

        public static void OnDayStarted()
        {
            var config = ModConfig.GetInstance();
            _contractRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays * 31);

            // Check for contract board refresh
            int today = Game1.Date.TotalDays;
            if (_lastRefreshDay < 0 || today - _lastRefreshDay >= config.ContractRefreshDays)
            {
                RefreshBoard();
                _lastRefreshDay = today;
            }

            // Check deadlines on active contracts
            var toRemove = new List<Contract>();
            foreach (var contract in _activeContracts)
            {
                int penalty = contract.CheckDeadline();
                if (penalty > 0)
                {
                    // Apply breach penalty
                    Game1.player.Money = Math.Max(0, Game1.player.Money - penalty);
                    LogHelper.Warn($"[Contracts] BREACH: {contract.Name} — penalty of {penalty}g applied!");

                    // Apply friendship penalty
                    if (!string.IsNullOrEmpty(contract.RequesterNpc))
                    {
                        var npc = Game1.getCharacterFromName(contract.RequesterNpc);
                        if (npc != null)
                        {
                            Game1.player.changeFriendship(contract.FriendshipPenalty, npc);
                        }
                    }

                    toRemove.Add(contract);
                    _completedContracts.Add(contract);

                    // Notification
                    Game1.addHUDMessage(new HUDMessage(
                        $"Contract failed: {contract.Name}! Penalty: {penalty}g",
                        HUDMessage.error_type));

                    // Send contract breach mail
                    MailManager.SendContractBreach();
                }
            }
            foreach (var c in toRemove) _activeContracts.Remove(c);
        }

        // ══════════════════════════════════════════════════════════════
        //  CONTRACT GENERATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>Generate a fresh set of contracts for the board.</summary>
        private static void RefreshBoard()
        {
            _availableContracts.Clear();
            var config = ModConfig.GetInstance();

            for (int i = 0; i < config.ContractsPerRefresh; i++)
            {
                var contract = GenerateContract();
                if (contract != null)
                    _availableContracts.Add(contract);
            }

            LogHelper.Info($"[Contracts] Board refreshed with {_availableContracts.Count} new contracts.");
        }

        /// <summary>Generate a single random contract with seasonal bias and progression scaling.</summary>
        private static Contract GenerateContract()
        {
            var config = ModConfig.GetInstance();

            // Build the eligible NPC pool based on reputation level & professions
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            var eligibleContractors = new Dictionary<string, string[]>(NpcContractors);

            // Zuzu City NPCs unlock at National tier (Lv5+) with RegionalMogul
            if (repLevel >= 5 && ReputationSkill.HasProfession("RegionalMogul"))
            {
                foreach (var kvp in ZuzuCityContractors)
                    eligibleContractors[kvp.Key] = kvp.Value;
            }

            // International NPCs unlock at International tier (Lv10) with GlobalExporter
            if (repLevel >= 10 && ReputationSkill.HasProfession("GlobalExporter"))
            {
                foreach (var kvp in InternationalContractors)
                    eligibleContractors[kvp.Key] = kvp.Value;
            }

            // Pick a random NPC requester from the eligible pool
            var npcList = eligibleContractors.Keys.ToList();
            string npc = npcList[_contractRng.Next(npcList.Count)];
            var categories = eligibleContractors[npc];

            // ── Split NPC's categories into seasonal vs non-seasonal pools ──
            var seasonalCats   = categories.Where(c => SeasonalItemRegistry.SeasonalCategories.Contains(c)).ToArray();
            var nonSeasonalCats = categories.Where(c => !SeasonalItemRegistry.SeasonalCategories.Contains(c)).ToArray();

            string category;
            if (seasonalCats.Length > 0 && nonSeasonalCats.Length > 0)
            {
                // Roll: SeasonalContractBias chance to pick a seasonal category
                if (_contractRng.NextDouble() < config.SeasonalContractBias)
                    category = seasonalCats[_contractRng.Next(seasonalCats.Length)];
                else
                    category = nonSeasonalCats[_contractRng.Next(nonSeasonalCats.Length)];
            }
            else if (seasonalCats.Length > 0)
                category = seasonalCats[_contractRng.Next(seasonalCats.Length)];
            else if (nonSeasonalCats.Length > 0)
                category = nonSeasonalCats[_contractRng.Next(nonSeasonalCats.Length)];
            else
                category = categories[_contractRng.Next(categories.Length)];

            // Get the dynamic item pool for this category (built from game data)
            var itemPool = SeasonalItemRegistry.GetItemPool(category);
            if (itemPool == null || itemPool.Count == 0)
                return null;

            // ── Seasonal bias: split pool into in-season and off-season ──
            var inSeason  = itemPool.Where(e => SeasonalItemRegistry.IsInSeason(e.Seasons)).ToList();
            var offSeason = itemPool.Where(e => !SeasonalItemRegistry.IsInSeason(e.Seasons)).ToList();

            bool pickedOffSeason = false;
            SeasonalItemRegistry.PoolEntry picked;

            if (inSeason.Count > 0 && offSeason.Count > 0)
            {
                // Roll: InSeasonBias chance to pick from in-season pool
                if (_contractRng.NextDouble() < config.InSeasonBias)
                    picked = inSeason[_contractRng.Next(inSeason.Count)];
                else
                {
                    picked = offSeason[_contractRng.Next(offSeason.Count)];
                    pickedOffSeason = true;
                }
            }
            else if (inSeason.Count > 0)
                picked = inSeason[_contractRng.Next(inSeason.Count)];
            else if (offSeason.Count > 0)
            {
                picked = offSeason[_contractRng.Next(offSeason.Count)];
                pickedOffSeason = true;
            }
            else
                picked = itemPool[_contractRng.Next(itemPool.Count)]; // fallback

            string itemId = picked.QualifiedItemId;

            // ── Quantity: progression-based scaling ──
            int quantity = GetProgressionQuantity(_contractRng, config.ContractQuantityScale);

            // Determine quality requirement (higher chance in later years)
            int minQuality = 0;
            if (Game1.year >= 2 && _contractRng.NextDouble() < 0.3)
                minQuality = 1;
            if (Game1.year >= 3 && _contractRng.NextDouble() < 0.15)
                minQuality = 2;

            // Calculate base reward: vanilla price * quantity
            int vanillaPrice = GetVanillaItemPrice(itemId);
            int baseReward = vanillaPrice * quantity;

            // Off-season bonus: items that aren't naturally available pay more
            double offSeasonMultiplier = pickedOffSeason ? config.OffSeasonRewardBonus : 1.0;
            baseReward = (int)(baseReward * offSeasonMultiplier);

            // BulkTrader profession: -20% quantity requirements
            if (ReputationSkill.HasProfession("BulkTrader"))
            {
                quantity = Math.Max(1, (int)(quantity * 0.80));
            }

            // ── Duration: longer to compensate for crop growing time ──
            // Base duration + extra days proportional to quantity, clamped
            int duration = Math.Clamp(
                config.ContractDurationBase + quantity / 5,
                config.ContractDurationBase,
                config.ContractDurationMax);

            // Off-season contracts get extra time (hard to source)
            if (pickedOffSeason)
                duration = Math.Min(config.ContractDurationMax, (int)(duration * 1.5));

            // Friendship reward scales with contract value
            int friendshipReward = Math.Clamp(150 + baseReward / 500, 150, 500);

            string qualityName = minQuality switch
            {
                1 => "Silver+ ",
                2 => "Gold+ ",
                4 => "Iridium ",
                _ => ""
            };

            string seasonTag = pickedOffSeason ? " [Off-Season]" : "";

            // Get item display name
            string itemName = GetItemDisplayName(itemId);

            var contract = new Contract
            {
                ContractId = $"SEP_{_nextContractId++}",
                Name = $"{npc}'s Order: {qualityName}{quantity}x {itemName}{seasonTag}",
                Description = $"{npc} needs {quantity} {qualityName}{itemName} delivered within {duration} days.{(pickedOffSeason ? " (Off-season premium!)" : "")}",
                RequesterNpc = npc,
                RequiredItems = new Dictionary<string, int> { { itemId, quantity } },
                MinimumQuality = minQuality,
                DurationDays = duration,
                BaseReward = baseReward,
                PremiumMultiplier = config.ContractPremium,
                FriendshipReward = friendshipReward,
                FriendshipPenalty = -30,
                MarketCategory = category,
            };

            return contract;
        }

        /// <summary>Get vanilla base price for an item by qualified ID.</summary>
        private static int GetVanillaItemPrice(string qualifiedItemId)
        {
            try
            {
                var item = ItemRegistry.Create(qualifiedItemId);
                if (item is StardewValley.Object obj)
                {
                    return obj.Price;
                }
                return 50; // fallback
            }
            catch
            {
                return 50;
            }
        }

        /// <summary>Get display name for an item.</summary>
        private static string GetItemDisplayName(string qualifiedItemId)
        {
            try
            {
                var item = ItemRegistry.Create(qualifiedItemId);
                return item?.DisplayName ?? qualifiedItemId;
            }
            catch
            {
                return qualifiedItemId;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PLAYER ACTIONS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Accept a contract from the board.</summary>
        public static bool AcceptContract(string contractId)
        {
            var config = ModConfig.GetInstance();

            // Dynamic contract slots: base + bonus from Reputation level (Lv1/4/9 = +1 each)
            int maxContracts = config.MaxActiveContracts;
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            if (repLevel >= 9) maxContracts += 3;
            else if (repLevel >= 4) maxContracts += 2;
            else if (repLevel >= 1) maxContracts += 1;

            if (_activeContracts.Count >= maxContracts)
            {
                Game1.addHUDMessage(new HUDMessage("Cannot accept more contracts!", HUDMessage.error_type));
                return false;
            }

            var contract = _availableContracts.FirstOrDefault(c => c.ContractId == contractId);
            if (contract == null) return false;

            contract.IsAccepted = true;
            contract.AcceptedDay = Game1.Date.TotalDays;

            _availableContracts.Remove(contract);
            _activeContracts.Add(contract);

            LogHelper.Info($"[Contracts] Accepted: {contract.Name}");
            Game1.addHUDMessage(new HUDMessage($"Contract accepted: {contract.Name}", HUDMessage.achievement_type));
            return true;
        }

        /// <summary>
        /// Deliver items to an active contract.
        /// Returns the number of items accepted.
        /// </summary>
        public static int DeliverToContract(string contractId, Item item, int quantity)
        {
            var contract = _activeContracts.FirstOrDefault(c => c.ContractId == contractId);
            if (contract == null) return 0;

            int accepted = contract.DeliverItems(item.QualifiedItemId, quantity, item.Quality);

            if (contract.IsCompleted)
            {
                CompleteContract(contract);
            }

            return accepted;
        }

        /// <summary>Complete a contract and award rewards.</summary>
        private static void CompleteContract(Contract contract)
        {
            // Award gold reward
            int reward = contract.TotalReward;

            // Level-based contract reward bonuses (Lv2: +5%, Lv6: +10%)
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            if (repLevel >= 6)
            {
                int lvlBonus = (int)(reward * 0.10);
                reward += lvlBonus;
                LogHelper.Debug($"[Contracts] Lv6 perk: +{lvlBonus}g (+10% reward)");
            }
            else if (repLevel >= 2)
            {
                int lvlBonus = (int)(reward * 0.05);
                reward += lvlBonus;
                LogHelper.Debug($"[Contracts] Lv2 perk: +{lvlBonus}g (+5% reward)");
            }

            // LocalMerchant profession: +15% reward from Pelican Town NPCs
            if (ReputationSkill.HasProfession("LocalMerchant") &&
                PelicanTownNpcs.Contains(contract.RequesterNpc ?? ""))
            {
                int bonus = (int)(reward * 0.15);
                reward += bonus;
                LogHelper.Debug($"[Contracts] LocalMerchant bonus: +{bonus}g from {contract.RequesterNpc}");
            }

            // RegionalMogul profession: +50% reward from Zuzu City NPCs
            if (ReputationSkill.HasProfession("RegionalMogul") &&
                ZuzuCityNpcs.Contains(contract.RequesterNpc ?? ""))
            {
                int bonus = (int)(reward * 0.50);
                reward += bonus;
                LogHelper.Debug($"[Contracts] RegionalMogul bonus: +{bonus}g from {contract.RequesterNpc}");
            }

            // GlobalExporter profession: 2x reward from International NPCs
            if (ReputationSkill.HasProfession("GlobalExporter") &&
                InternationalNpcs.Contains(contract.RequesterNpc ?? ""))
            {
                int bonus = reward; // double it
                reward += bonus;
                LogHelper.Debug($"[Contracts] GlobalExporter bonus: +{bonus}g (2x) from {contract.RequesterNpc}");
            }

            Game1.player.Money += reward;

            // Award friendship
            if (!string.IsNullOrEmpty(contract.RequesterNpc))
            {
                var npc = Game1.getCharacterFromName(contract.RequesterNpc);
                if (npc != null)
                {
                    Game1.player.changeFriendship(contract.FriendshipReward, npc);
                }
            }

            // Award Reputation XP
            ReputationSkill.AddReputationXP(Game1.player, Math.Max(10, reward / 100));

            _activeContracts.Remove(contract);
            _completedContracts.Add(contract);

            LogHelper.Info($"[Contracts] COMPLETED: {contract.Name} — Reward: {reward}g");
            Game1.addHUDMessage(new HUDMessage(
                $"Contract complete: {contract.Name}! +{reward}g",
                HUDMessage.achievement_type));

            // Send contract completion mail
            MailManager.SendContractComplete();
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════════

        public static IReadOnlyList<Contract> GetAvailableContracts() => _availableContracts.AsReadOnly();
        public static IReadOnlyList<Contract> GetActiveContracts() => _activeContracts.AsReadOnly();
        public static IReadOnlyList<Contract> GetCompletedContracts() => _completedContracts.AsReadOnly();

        /// <summary>Find an active contract that accepts a given item.</summary>
        public static Contract FindContractForItem(Item item)
        {
            if (item == null) return null;
            return _activeContracts.FirstOrDefault(c =>
                !c.IsCompleted && !c.IsFailed &&
                c.RequiredItems.ContainsKey(item.QualifiedItemId) &&
                item.Quality >= c.MinimumQuality &&
                c.DeliveredItems.GetValueOrDefault(item.QualifiedItemId, 0) < c.RequiredItems[item.QualifiedItemId]);
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            var data = new ContractSaveData
            {
                AvailableContracts = _availableContracts,
                ActiveContracts = _activeContracts,
                CompletedContracts = _completedContracts,
                LastRefreshDay = _lastRefreshDay,
                NextContractId = _nextContractId,
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public static void Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _availableContracts = new List<Contract>();
                _activeContracts = new List<Contract>();
                _completedContracts = new List<Contract>();
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<ContractSaveData>(json);
                _availableContracts = data?.AvailableContracts ?? new List<Contract>();
                _activeContracts = data?.ActiveContracts ?? new List<Contract>();
                _completedContracts = data?.CompletedContracts ?? new List<Contract>();
                _lastRefreshDay = data?.LastRefreshDay ?? -1;
                _nextContractId = data?.NextContractId ?? 1;
                LogHelper.Info($"[Contracts] Loaded {_activeContracts.Count} active, {_availableContracts.Count} available contracts.");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Contracts] Failed to deserialize:\n{ex}");
                _availableContracts = new List<Contract>();
                _activeContracts = new List<Contract>();
                _completedContracts = new List<Contract>();
            }
        }

        public static void Reset()
        {
            _availableContracts.Clear();
            _activeContracts.Clear();
            _completedContracts.Clear();
            _lastRefreshDay = -1;
            _nextContractId = 1;
        }

        private class ContractSaveData
        {
            public List<Contract> AvailableContracts { get; set; }
            public List<Contract> ActiveContracts { get; set; }
            public List<Contract> CompletedContracts { get; set; }
            public int LastRefreshDay { get; set; }
            public int NextContractId { get; set; }
        }
    }
}
