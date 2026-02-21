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

        /// <summary>Flag used by Harmony patches to skip GlobalSellMultiplier for contracts.</summary>
        public static bool IsCalculatingContractPrice { get; set; } = false;

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize or reset all market bottles based on config and reputation level.
        /// </summary>
        public static void Initialize(int reputationLevel = 0)
        {
            _bottles.Clear();
            var config = ModConfig.GetInstance();
            float reputationCapacityMultiplier = GetReputationCapacityMultiplier(reputationLevel);

            foreach (string category in MarketCategories.All)
            {
                foreach (int quality in MarketCategories.QualityTiers)
                {
                    string bottleId = $"{category}_Q{quality}";
                    float baseCap = (float)config.BaseBottleCapacity;
                    float categoryCap = MarketCategories.GetCategoryCapacityMultiplier(category);
                    float qualityCap = MarketCategories.GetQualityCapacityScalar(quality, config);
                    float drainRate = MarketCategories.GetDrainRate(category, config);

                    var bottle = new MarketBottle
                    {
                        BottleId = bottleId,
                        CategoryId = category,
                        QualityTier = quality,
                        CurrentVolume = 0,
                        MaxCapacity = baseCap * categoryCap * qualityCap * reputationCapacityMultiplier,
                        DailyDrainRate = drainRate,
                    };

                    _bottles[bottleId] = bottle;
                }
            }

            _marketRng = new Random((int)Game1.uniqueIDForThisGame + Game1.Date.TotalDays);
            _initialized = true;
            LogHelper.Info($"[Market] Initialized {_bottles.Count} bottles (reputation multiplier: {reputationCapacityMultiplier:F1}x)");
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

            // Daily luck factor influences drainage variance
            float luckFactor = 1.0f + (float)(Game1.player.DailyLuck * 2.0);

            // Reputation-based drain bonuses
            float drainBonus = 1.0f;
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            if (repLevel >= 3)
                drainBonus *= 1.10f; // Level 3 perk: +10% drain rate
            if (ReputationSkill.HasProfession("MarketManipulator"))
                drainBonus *= 1.25f; // MarketManipulator profession: +25% drain rate

            // Check for surge events
            foreach (var bottle in _bottles.Values)
            {
                // Surge probability check
                if (_marketRng.NextDouble() < config.SurgeProbability)
                {
                    bottle.IsSurgeActive = true;
                    LogHelper.Info($"[Market] SURGE EVENT: {bottle.BottleId} — massive demand spike!");
                }

                // Apply daily drainage
                bottle.ApplyDailyDrainage(_marketRng, luckFactor);

                // Apply additional drainage from reputation perks
                if (drainBonus > 1.0f)
                {
                    float extraDrain = bottle.MaxCapacity * bottle.DailyDrainRate * (drainBonus - 1.0f);
                    bottle.RemoveVolume(extraDrain);
                }
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

            string category = MarketCategories.FromItemCategory(item.Category);
            int quality = item.Quality;
            string bottleId = $"{category}_Q{quality}";

            if (_bottles.TryGetValue(bottleId, out var bottle))
            {
                return bottle.DynamicPriceMultiplier * bottle.QualityMarginMultiplier;
            }

            return 1.0f;
        }

        /// <summary>
        /// Get the raw saturation multiplier (without quality margin) for a specific item.
        /// Used by the Harmony patch for the global deflationary engine.
        /// </summary>
        public static float GetRawSaturationMultiplier(Item item)
        {
            if (item == null || !_initialized) return 1.0f;

            string category = MarketCategories.FromItemCategory(item.Category);
            int quality = item.Quality;
            string bottleId = $"{category}_Q{quality}";

            if (_bottles.TryGetValue(bottleId, out var bottle))
            {
                return bottle.DynamicPriceMultiplier;
            }

            return 1.0f;
        }

        /// <summary>Get a specific bottle by ID.</summary>
        public static MarketBottle GetBottle(string bottleId)
        {
            return _bottles.TryGetValue(bottleId, out var bottle) ? bottle : null;
        }

        /// <summary>Get the bottle for a specific item.</summary>
        public static MarketBottle GetBottleForItem(Item item)
        {
            if (item == null) return null;
            string category = MarketCategories.FromItemCategory(item.Category);
            string bottleId = $"{category}_Q{item.Quality}";
            return _bottles.TryGetValue(bottleId, out var bottle) ? bottle : null;
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

            var bottle = GetBottleForItem(item);
            if (bottle != null)
            {
                bottle.AddVolume(quantity);
                LogHelper.Trace($"[Market] Sold {quantity}x {item.Name} → {bottle.BottleId} ({bottle.Saturation:P0} saturated)");
            }
        }

        /// <summary>
        /// Transfer volume from raw ingredient bottle to artisan bottle when processing.
        /// </summary>
        public static void TransferToArtisan(Item rawItem, int quantity)
        {
            if (rawItem == null || !_initialized) return;

            // Remove from raw category
            var rawBottle = GetBottleForItem(rawItem);
            if (rawBottle != null)
            {
                rawBottle.RemoveVolume(quantity);
            }

            // Add to artisan bottle (quality 0 for artisan goods)
            string artisanBottleId = $"{MarketCategories.ArtisanGoods}_Q0";
            if (_bottles.TryGetValue(artisanBottleId, out var artisanBottle))
            {
                artisanBottle.AddVolume(quantity);
            }
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

                for (int day = 0; day < daysAhead; day++)
                {
                    float baseDrain = maxCap * drainRate;
                    float variance = baseDrain * 0.3f;
                    float randomVar = (float)(simRng.NextDouble() * 2 - 1) * variance;
                    float totalDrain = baseDrain + randomVar;

                    // Check for surge
                    if (simRng.NextDouble() < config.SurgeProbability)
                    {
                        totalDrain *= 5f + (float)(simRng.NextDouble() * 5.0);
                    }

                    currentVol = Math.Max(0, currentVol - totalDrain);
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
            _initialized = false;
            _lastPrefillSeason = "";
            LogHelper.Debug("[Market] State reset.");
        }

        /// <summary>Internal save data structure.</summary>
        private class MarketSaveData
        {
            public Dictionary<string, MarketBottle> Bottles { get; set; }
            public string LastPrefillSeason { get; set; }
        }
    }
}
