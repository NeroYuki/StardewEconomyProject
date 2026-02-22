using System;
using Newtonsoft.Json;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Represents a single market "bottle" — a volumetric container for a
    /// specific item type and quality tier combination.
    /// The bottle tracks current supply volume against maximum capacity,
    /// and the saturation level drives dynamic pricing.
    /// </summary>
    public class MarketBottle
    {
        /// <summary>
        /// Unique identifier for this bottle.
        /// Format: {QualifiedItemId}_Q{QualityTier}  e.g. "(O)254_Q0"
        /// </summary>
        public string BottleId { get; set; }

        /// <summary>The qualified item ID this bottle tracks, e.g. "(O)254".</summary>
        public string ItemId { get; set; }

        /// <summary>The market category this item belongs to (used for drain rates).</summary>
        public string CategoryId { get; set; }

        /// <summary>Quality tier: 0=Normal, 1=Silver, 2=Gold, 4=Iridium.</summary>
        public int QualityTier { get; set; }

        /// <summary>Current volume in the bottle (units of produce in the market).</summary>
        public float CurrentVolume { get; set; }

        /// <summary>Maximum capacity of this bottle.</summary>
        public float MaxCapacity { get; set; }

        /// <summary>
        /// Daily drain rate as fraction of MaxCapacity.
        /// Represents regional consumption and exports.
        /// </summary>
        public float DailyDrainRate { get; set; }

        /// <summary>Whether a surge event is active today (5-10x drainage).</summary>
        [JsonIgnore]
        public bool IsSurgeActive { get; set; }

        /// <summary>
        /// Current saturation S = V / Cmax. Range [0, 1].
        /// </summary>
        [JsonIgnore]
        public float Saturation => MaxCapacity > 0 ? Math.Clamp(CurrentVolume / MaxCapacity, 0f, 1f) : 1f;

        /// <summary>
        /// Dynamic price multiplier based on quadratic decay of saturation.
        /// M_dynamic = (1 - S)^2
        /// At 0% saturation → 1.00 (empty/surge)
        /// At 25% → 0.56 (healthy)
        /// At 50% → 0.25 (saturated)
        /// At 75% → 0.06 (flooded)
        /// At 100% → 0.00 (crash)
        /// </summary>
        [JsonIgnore]
        public float DynamicPriceMultiplier
        {
            get
            {
                float s = Saturation;
                return (1f - s) * (1f - s);
            }
        }

        /// <summary>
        /// Get the quality-tier margin multiplier bonus.
        /// Normal=1.0, Silver=1.2, Gold=1.5, Iridium=2.0
        /// </summary>
        [JsonIgnore]
        public float QualityMarginMultiplier
        {
            get
            {
                var config = ModConfig.GetInstance();
                return QualityTier switch
                {
                    0 => 1.0f,
                    1 => (float)config.SilverMarginMultiplier,
                    2 => (float)config.GoldMarginMultiplier,
                    4 => (float)config.IridiumMarginMultiplier,
                    _ => 1.0f
                };
            }
        }

        /// <summary>
        /// Get the market state description based on saturation level.
        /// </summary>
        [JsonIgnore]
        public string MarketState
        {
            get
            {
                float s = Saturation;
                if (s <= 0.05f) return "Empty / Surge";
                if (s <= 0.30f) return "Healthy";
                if (s <= 0.55f) return "Saturated";
                if (s <= 0.80f) return "Flooded";
                return "Crash";
            }
        }

        /// <summary>
        /// Add volume to this bottle (player selling into the market).
        /// </summary>
        public void AddVolume(float amount)
        {
            CurrentVolume = Math.Min(CurrentVolume + amount, MaxCapacity);
        }

        /// <summary>
        /// Remove volume from this bottle (player buying or consuming).
        /// </summary>
        public void RemoveVolume(float amount)
        {
            CurrentVolume = Math.Max(CurrentVolume - amount, 0f);
        }

        /// <summary>
        /// Apply daily competition-driven drainage / saturation.
        ///
        /// The game world has many other farms; on any given day competitors
        /// may flood the market (raising volume/saturation) or consumer demand
        /// may pull it down (lowering volume/saturation).
        ///
        /// SPECTRUM — advances every season, not every year:
        ///   Spring Y1 (season 0)  : 70% drain / 30% saturate  (favorable start)
        ///   Winter Y2 (season 7)  : ~54% / 46%
        ///   Spring Y3 (season 8+) : 50% drain / 50% saturate  (full equilibrium)
        ///
        /// BASE DRAIN — uses a reference capacity (BaseBottleCapacity × category scalar)
        /// so that expanding the bottle via reputation does NOT proportionally scale
        /// competition. A higher-rep bottle drains less relative to its size.
        ///
        /// INERT FACTOR — optional reputation-based dampener passed by MarketManager.
        ///   Rep 0-4 Regional  : 1.00  (full amplitude)
        ///   Rep 5-9 National  : 0.85  (slightly calmer market)
        ///   Rep 10 Intl       : 0.70  (mature, stable market)
        /// </summary>
        public void ApplyDailyDrainage(Random rng, float luckFactor, float drainInertFactor = 1.0f)
        {
            // ── Reference capacity: category-scaled baseline, NOT expanded rep capacity ──
            var config = ModConfig.GetInstance();
            float catMult = MarketCategories.GetCategoryCapacityMultiplier(CategoryId);
            float refCap  = (float)config.BaseBottleCapacity * catMult;
            float baseDrain = refCap * DailyDrainRate * drainInertFactor;

            // ── Competition spectrum: advances by season ──
            int seasonIndex = Game1.currentSeason switch
            {
                "spring" => 0, "summer" => 1, "fall" => 2, "winter" => 3, _ => 0
            };
            int totalSeasons = (Game1.year - 1) * 4 + seasonIndex;
            float t = Math.Clamp(totalSeasons / 8f, 0f, 1f);  // 0.0 @ spring Y1 → 1.0 @ spring Y3
            float drainWeight    = 0.70f - 0.20f * t;          // 0.70 → 0.50
            float saturateWeight = 0.30f + 0.20f * t;          // 0.30 → 0.50

            // Each component gets independent random variance [0.5 … 1.5]
            float consumptionRoll = 0.5f + (float)rng.NextDouble();
            float competitionRoll = 0.5f + (float)rng.NextDouble();

            float consumption = baseDrain * drainWeight * consumptionRoll;
            float competition = baseDrain * saturateWeight * competitionRoll;

            // Daily luck widens the consumption swing
            float luckSwing = baseDrain * 0.15f * luckFactor
                              * (float)(rng.NextDouble() * 2 - 1);
            consumption = Math.Max(0, consumption + luckSwing);

            float netChange = consumption - competition;

            // Surge overrides: massive demand spike always drains
            if (IsSurgeActive)
            {
                float surgeMultiplier = 5f + (float)(rng.NextDouble() * 5.0);
                netChange = baseDrain * surgeMultiplier;
            }

            // Apply: positive → drain (vol ↓, price ↑); negative → fill (vol ↑, price ↓)
            if (netChange > 0)
                RemoveVolume(netChange);
            else
                AddVolume(Math.Abs(netChange));

            IsSurgeActive = false;
        }

        /// <summary>
        /// Pre-fill this bottle for in-season items.
        /// V_season_start = Cmax * (0.4 + Random(0.0, 0.3))
        /// </summary>
        public void PrefillForSeason(Random rng, double regionalCompetition)
        {
            float prefillBase = 0.4f + (float)(rng.NextDouble() * 0.3);
            CurrentVolume = MaxCapacity * prefillBase * (float)regionalCompetition;
        }

        public override string ToString()
        {
            return $"[{BottleId}] {CurrentVolume:F0}/{MaxCapacity:F0} ({Saturation:P0}) → x{DynamicPriceMultiplier:F3} ({MarketState})";
        }
    }
}
