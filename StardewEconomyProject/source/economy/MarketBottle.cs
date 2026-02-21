using System;
using Newtonsoft.Json;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Represents a single market "bottle" — a volumetric container for a
    /// specific produce category and quality tier combination.
    /// The bottle tracks current supply volume against maximum capacity,
    /// and the saturation level drives dynamic pricing.
    /// </summary>
    public class MarketBottle
    {
        /// <summary>
        /// Unique identifier for this bottle, e.g. "Vegetable_Normal", "Fruit_Gold".
        /// Format: {CategoryID}_{QualityTier}
        /// </summary>
        public string BottleId { get; set; }

        /// <summary>The market category this bottle belongs to.</summary>
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
        /// Apply daily drainage to this bottle.
        /// D = (Cmax * M_base_demand) + Random(-R, R)
        /// If surge: D *= Random(5, 10)
        /// </summary>
        public void ApplyDailyDrainage(Random rng, float luckFactor)
        {
            float baseDrain = MaxCapacity * DailyDrainRate;
            float variance = baseDrain * 0.3f * luckFactor;
            float randomVariance = (float)(rng.NextDouble() * 2 - 1) * variance;
            float totalDrain = baseDrain + randomVariance;

            if (IsSurgeActive)
            {
                float surgeMultiplier = 5f + (float)(rng.NextDouble() * 5.0);
                totalDrain *= surgeMultiplier;
            }

            RemoveVolume(Math.Max(0, totalDrain));
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
