using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Market category definitions used by the bottle system.
    /// Each category maps to one or more Stardew item categories.
    /// </summary>
    public static class MarketCategories
    {
        public const string Vegetable = "Vegetable";
        public const string Fruit = "Fruit";
        public const string Flower = "Flower";
        public const string Forage = "Forage";
        public const string ArtisanGoods = "ArtisanGoods";
        public const string AnimalProduct = "AnimalProduct";
        public const string Fish = "Fish";
        public const string Mineral = "Mineral";
        public const string Cooking = "Cooking";
        public const string Default = "Default";

        /// <summary>All known market categories.</summary>
        public static readonly string[] All = new[]
        {
            Vegetable, Fruit, Flower, Forage, ArtisanGoods,
            AnimalProduct, Fish, Mineral, Cooking, Default
        };

        /// <summary>Quality tiers used for sub-market stratification.</summary>
        public static readonly int[] QualityTiers = new[] { 0, 1, 2, 4 };

        /// <summary>Map a Stardew item category ID to our market category.</summary>
        public static string FromItemCategory(int category)
        {
            return category switch
            {
                -75 => Vegetable,
                -79 => Fruit,
                -80 => Flower,
                -81 => Forage,
                -26 => ArtisanGoods,
                -5 => AnimalProduct,
                -6 => AnimalProduct,
                -18 => AnimalProduct,
                -4 => Fish,
                -2 => Mineral,
                -12 => Mineral,
                -7 => Cooking,
                _ => Default
            };
        }

        /// <summary>Get the category-specific drain rate from config.</summary>
        public static float GetDrainRate(string category, ModConfig config)
        {
            return category switch
            {
                Vegetable => (float)config.VegetableDrainRate,
                Fruit => (float)config.FruitDrainRate,
                Flower => (float)config.FlowerDrainRate,
                Forage => (float)config.VegetableDrainRate,
                ArtisanGoods => (float)config.ArtisanDrainRate,
                AnimalProduct => (float)config.AnimalProductDrainRate,
                Fish => (float)config.FishDrainRate,
                Mineral => (float)config.MineralDrainRate,
                Cooking => (float)config.VegetableDrainRate,
                _ => (float)config.MarketDrainRate,
            };
        }

        /// <summary>Get the category-specific base capacity multiplier.</summary>
        public static float GetCategoryCapacityMultiplier(string category)
        {
            return category switch
            {
                Vegetable => 1.0f,
                Fruit => 0.8f,
                Flower => 0.5f,
                Forage => 0.7f,
                ArtisanGoods => 0.6f,
                AnimalProduct => 0.8f,
                Fish => 0.9f,
                Mineral => 0.4f,
                Cooking => 0.5f,
                _ => 1.0f,
            };
        }

        /// <summary>Get the quality-tier capacity scalar.</summary>
        public static float GetQualityCapacityScalar(int quality, ModConfig config)
        {
            return quality switch
            {
                0 => 1.0f,
                1 => (float)config.SilverCapacityScalar,
                2 => (float)config.GoldCapacityScalar,
                4 => (float)config.IridiumCapacityScalar,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Central manager for all market bottles. Handles initialization, daily updates,
    /// seasonal prefilling, volume tracking, and serialization.
    ///
    /// Each bottle represents one (item × quality-tier) pairing.
    /// Bottles are lazily created when an item is first encountered.
    /// Key format: "{qualifiedItemId}_Q{quality}"  e.g. "(O)254_Q0" for normal-quality Melon.
    /// </summary>
    public class MarketManager
    {
        /// <summary>All active market bottles. Key = BottleId.</summary>
        private static Dictionary<string, MarketBottle> _bottles = new();

        /// <summary>Whether the market has been initialized for the current save.</summary>
        private static bool _initialized = false;

        /// <summary>Random instance for market simulation.</summary>
        private static Random _marketRng;

        /// <summary>The season when bottles were last prefilled.</summary>
        private static string _lastPrefillSeason = "";

        /// <summary>Snapshot of yesterday's saturation for change-detection (TV report).</summary>
        private static Dictionary<string, float> _previousSaturation = new();

        /// <summary>Flag used by Harmony patches to skip GlobalSellMultiplier for contracts.</summary>
        public static bool IsCalculatingContractPrice { get; set; } = false;

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize or reset the market system.
        /// Bottles are created lazily on demand — this just clears state.
        /// </summary>
        public static void Initialize(int reputationLevel = 0)
        {
            _bottles.Clear();
            _previousSaturation.Clear();
            _marketRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays);
            _initialized = true;
            LogHelper.Info($"[Market] Initialized (per-item bottles, created on demand).");
        }

        /// <summary>
        /// Build the bottle ID for a given item and quality tier.
        /// </summary>
        public static string GetItemBottleId(string qualifiedItemId, int quality)
            => $"{qualifiedItemId}_Q{quality}";

        /// <summary>
        /// Build the bottle ID for an Item instance.
        /// </summary>
        public static string GetItemBottleId(Item item)
            => item == null ? null : GetItemBottleId(item.QualifiedItemId, item.Quality);

        /// <summary>
        /// Get an existing bottle or lazily create one for the specified item + quality.
        /// </summary>
        public static MarketBottle GetOrCreateBottle(string qualifiedItemId, int quality, int reputationLevel = -1)
        {
            string bottleId = GetItemBottleId(qualifiedItemId, quality);
            if (_bottles.TryGetValue(bottleId, out var existing))
                return existing;

            // Lazily create — derive category from item registry
            string category = MarketCategories.Default;
            try
            {
                var tmp = ItemRegistry.Create(qualifiedItemId);
                if (tmp != null)
                    category = MarketCategories.FromItemCategory(tmp.Category);
            }
            catch { /* default category */ }

            if (reputationLevel < 0)
                reputationLevel = ReputationSkill.GetLevel(Game1.player);

            var config = ModConfig.GetInstance();
            float baseCap = (float)config.BaseBottleCapacity;
            float categoryCap = MarketCategories.GetCategoryCapacityMultiplier(category);
            float qualityCap = MarketCategories.GetQualityCapacityScalar(quality, config);
            float repMult = GetReputationCapacityMultiplier(reputationLevel);
            float drainRate = MarketCategories.GetDrainRate(category, config);

            var bottle = new MarketBottle
            {
                BottleId = bottleId,
                ItemId = qualifiedItemId,
                CategoryId = category,
                QualityTier = quality,
                CurrentVolume = 0,
                MaxCapacity = baseCap * categoryCap * qualityCap * repMult,
                DailyDrainRate = drainRate,
            };

            _bottles[bottleId] = bottle;
            return bottle;
        }

        /// <summary>
        /// Get the reputation-based capacity multiplier.
        /// Levels 0-4: 1.0x (Regional), 5-9: 5.0x (National), 10: 25.0x (International)
        /// GlobalExporter profession grants an additional 2x on top.
        /// </summary>
        public static float GetReputationCapacityMultiplier(int reputationLevel)
        {
            float multiplier;
            if (reputationLevel >= 10) multiplier = 25.0f;
            else if (reputationLevel >= 5) multiplier = 5.0f;
            else multiplier = 1.0f;

            // GlobalExporter profession: 2x capacity
            if (ReputationSkill.HasProfession("GlobalExporter"))
            {
                multiplier *= 2.0f;
                LogHelper.Debug($"[Market] GlobalExporter 2x capacity active (total: {multiplier:F0}x)");
            }

            return multiplier;
        }

        // ══════════════════════════════════════════════════════════════
        //  DAILY UPDATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called at DayStarted. Applies daily drainage, surge events, and seasonal prefilling.
        /// </summary>
        public static void OnDayStarted()
        {
            if (!_initialized) Initialize();

            var config = ModConfig.GetInstance();
            _marketRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays);

            // Snapshot previous saturation for change-detection (TV report)
            _previousSaturation.Clear();
            foreach (var kvp in _bottles)
                _previousSaturation[kvp.Key] = kvp.Value.Saturation;

            // Daily luck factor influences drainage variance
            float luckFactor = 1.0f + (float)(Game1.player.DailyLuck * 2.0);

            // Reputation-based drain inertia (higher rep = calmer, more stable market)
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            float drainInertFactor;
            if (repLevel >= 10)      drainInertFactor = 0.70f;  // International: very stable
            else if (repLevel >= 5)  drainInertFactor = 0.85f;  // National: moderately stable
            else                     drainInertFactor = 1.00f;  // Regional: full amplitude

            // MarketManipulator perk adds extra drain on top (by reducing inertia)
            if (ReputationSkill.HasProfession("MarketManipulator"))
                drainInertFactor = Math.Min(drainInertFactor * 1.25f, 1.25f);

            // Level 3 perk: +10% effective drain
            if (repLevel >= 3)
                drainInertFactor = Math.Min(drainInertFactor * 1.10f, 1.35f);

            // Check for surge events & apply drainage
            foreach (var bottle in _bottles.Values)
            {
                if (_marketRng.NextDouble() < config.SurgeProbability)
                {
                    bottle.IsSurgeActive = true;
                    LogHelper.Info($"[Market] SURGE EVENT: {bottle.BottleId} — massive demand spike!");
                }

                bottle.ApplyDailyDrainage(_marketRng, luckFactor, drainInertFactor);
            }

            // Seasonal prefill on season change
            string currentSeason = Game1.currentSeason;
            if (currentSeason != _lastPrefillSeason)
            {
                ApplySeasonalPrefill(currentSeason);
                _lastPrefillSeason = currentSeason;
            }

            LogHelper.Debug($"[Market] Day {Game1.dayOfMonth} update complete. Active bottles: {_bottles.Count}");
        }

        /// <summary>
        /// Pre-fill bottles for categories that are in-season.
        /// V_season_start = Cmax * (0.4 + Random(0.0, 0.3)) * RegionalCompetition
        /// </summary>
        private static void ApplySeasonalPrefill(string season)
        {
            var config = ModConfig.GetInstance();

            // Determine which categories are "in season"
            var inSeasonCategories = GetInSeasonCategories(season);

            foreach (var bottle in _bottles.Values)
            {
                if (inSeasonCategories.Contains(bottle.CategoryId))
                {
                    bottle.PrefillForSeason(_marketRng, config.RegionalCompetition);
                    LogHelper.Debug($"[Market] Seasonal prefill: {bottle.BottleId} → {bottle.CurrentVolume:F0}/{bottle.MaxCapacity:F0}");
                }
            }
        }

        /// <summary>Get categories that are considered "in season" for prefilling.</summary>
        private static HashSet<string> GetInSeasonCategories(string season)
        {
            // Vegetables, Fruits, and Flowers are seasonal; other categories are year-round
            var result = new HashSet<string> { MarketCategories.Vegetable, MarketCategories.Fruit, MarketCategories.Flower };
            // Forage is always somewhat in-season
            result.Add(MarketCategories.Forage);
            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  PRICE QUERIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get the saturation-based dynamic price multiplier for an item.
        /// This is the M_dynamic = (1 - V/Cmax)^2 from the spec.
        /// </summary>
        public static float GetSaturationMultiplier(Item item)
        {
            if (item == null || !_initialized) return 1.0f;

            var bottle = GetOrCreateBottle(item.QualifiedItemId, item.Quality);
            return bottle.DynamicPriceMultiplier * bottle.QualityMarginMultiplier;
        }

        /// <summary>
        /// Get the raw saturation multiplier (without quality margin) for a specific item.
        /// Used by the Harmony patch for the global deflationary engine.
        /// </summary>
        public static float GetRawSaturationMultiplier(Item item)
        {
            if (item == null || !_initialized) return 1.0f;

            var bottle = GetOrCreateBottle(item.QualifiedItemId, item.Quality);
            return bottle.DynamicPriceMultiplier;
        }

        /// <summary>Get a specific bottle by ID.</summary>
        public static MarketBottle GetBottle(string bottleId)
        {
            return _bottles.TryGetValue(bottleId, out var bottle) ? bottle : null;
        }

        /// <summary>Get the bottle for a specific item (creates lazily if needed).</summary>
        public static MarketBottle GetBottleForItem(Item item)
        {
            if (item == null) return null;
            return GetOrCreateBottle(item.QualifiedItemId, item.Quality);
        }

        /// <summary>Get all bottles.</summary>
        public static IReadOnlyDictionary<string, MarketBottle> GetAllBottles()
        {
            return _bottles;
        }

        // ══════════════════════════════════════════════════════════════
        //  VOLUME TRACKING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Record that items were sold/shipped, adding volume to the appropriate bottle.
        /// </summary>
        public static void RecordSale(Item item, int quantity)
        {
            if (item == null || !_initialized) return;

            var bottle = GetOrCreateBottle(item.QualifiedItemId, item.Quality);
            bottle.AddVolume(quantity);
            LogHelper.Trace($"[Market] Sold {quantity}x {item.Name} → {bottle.BottleId} ({bottle.Saturation:P0} saturated)");
        }

        /// <summary>
        /// Transfer volume from raw ingredient bottle to artisan output bottle when processing.
        /// The artisan output item's own qualified ID is used as the artisan bottle key.
        /// </summary>
        public static void TransferToArtisan(Item rawItem, int quantity)
        {
            if (rawItem == null || !_initialized) return;

            // Drain the raw material's bottle
            var rawBottle = GetOrCreateBottle(rawItem.QualifiedItemId, rawItem.Quality);
            rawBottle.RemoveVolume(quantity);

            // NOTE: the artisan *output* bottle is filled when the output is shipped,
            // so no need to double-fill here.
        }

        // ══════════════════════════════════════════════════════════════
        //  FORECASTING (for Supercomputer)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Simulate future market state for forecasting. Returns predicted
        /// saturation levels for each category over the specified number of days.
        /// </summary>
        public static Dictionary<string, float[]> ForecastMarket(int daysAhead)
        {
            var result = new Dictionary<string, float[]>();
            var config = ModConfig.GetInstance();

            foreach (var kvp in _bottles)
            {
                float[] forecast = new float[daysAhead];
                float currentVol = kvp.Value.CurrentVolume;
                float maxCap = kvp.Value.MaxCapacity;
                float drainRate = kvp.Value.DailyDrainRate;

                // Use deterministic simulation based on game seed
                var simRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays);

                // Competition spectrum at current season (matches ApplyDailyDrainage logic)
                int simSeasonIdx = Game1.currentSeason switch
                {
                    "spring" => 0, "summer" => 1, "fall" => 2, "winter" => 3, _ => 0
                };
                int totalSeasonsSim = (Game1.year - 1) * 4 + simSeasonIdx;
                float simT = Math.Clamp(totalSeasonsSim / 8f, 0f, 1f);
                float simDrainW    = 0.70f - 0.20f * simT;
                float simSaturateW = 0.30f + 0.20f * simT;
                // Use the same ref-capacity approach (baseline, not MaxCapacity)
                float simRefCap = (float)config.BaseBottleCapacity
                    * MarketCategories.GetCategoryCapacityMultiplier(kvp.Value.CategoryId);
                float simBaseDrain = simRefCap * drainRate;

                for (int day = 0; day < daysAhead; day++)
                {
                    float consumptionRoll = 0.5f + (float)simRng.NextDouble();
                    float competitionRoll = 0.5f + (float)simRng.NextDouble();
                    float consumption = simBaseDrain * simDrainW * consumptionRoll;
                    float competition = simBaseDrain * simSaturateW * competitionRoll;
                    float netChange = consumption - competition;

                    // Check for surge
                    if (simRng.NextDouble() < config.SurgeProbability)
                    {
                        netChange = simBaseDrain * (5f + (float)(simRng.NextDouble() * 5.0));
                    }

                    if (netChange > 0)
                        currentVol = Math.Max(0, currentVol - netChange);
                    else
                        currentVol = Math.Min(maxCap, currentVol - netChange);

                    forecast[day] = maxCap > 0 ? currentVol / maxCap : 0;
                }

                result[kvp.Key] = forecast;
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>Serialize all market bottle state to JSON for save data.</summary>
        public static string Serialize()
        {
            var data = new MarketSaveData
            {
                Bottles = _bottles,
                LastPrefillSeason = _lastPrefillSeason
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary>Deserialize market state from saved JSON data.</summary>
        public static void Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Initialize();
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<MarketSaveData>(json);
                if (data?.Bottles != null)
                {
                    _bottles = data.Bottles;
                    _lastPrefillSeason = data.LastPrefillSeason ?? "";
                    _initialized = true;
                    LogHelper.Info($"[Market] Loaded {_bottles.Count} bottles from save.");
                }
                else
                {
                    Initialize();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Market] Failed to deserialize save data:\n{ex}");
                Initialize();
            }
        }

        /// <summary>Reset all market state (e.g. when returning to title).</summary>
        public static void Reset()
        {
            _bottles.Clear();
            _previousSaturation.Clear();
            _initialized = false;
            _lastPrefillSeason = "";
            LogHelper.Debug("[Market] State reset.");
        }

        // ══════════════════════════════════════════════════════════════
        //  CHANGE DETECTION (for TV report)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the top N bottles with the largest day-over-day saturation changes.
        /// Positive delta = saturation rose (bad for price); negative = saturation fell (good for price).
        /// Returns tuples of (bottle, deltaPercent).
        /// </summary>
        public static List<(MarketBottle bottle, float delta)> GetTopSaturationChanges(int topN = 10)
        {
            var changes = new List<(MarketBottle bottle, float delta)>();

            foreach (var kvp in _bottles)
            {
                float prev = _previousSaturation.TryGetValue(kvp.Key, out float p) ? p : 0f;
                float curr = kvp.Value.Saturation;
                float delta = curr - prev;
                if (Math.Abs(delta) > 0.01f) // ignore negligible changes
                    changes.Add((kvp.Value, delta));
            }

            return changes
                .OrderByDescending(c => Math.Abs(c.delta))
                .Take(topN)
                .ToList();
        }

        /// <summary>Internal save data structure.</summary>
        private class MarketSaveData
        {
            public Dictionary<string, MarketBottle> Bottles { get; set; }
            public string LastPrefillSeason { get; set; }
        }
    }
}
