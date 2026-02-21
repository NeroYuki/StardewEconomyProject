using System;
using System.Collections.Generic;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Core pricing engine for the Stardew Economy Project.
    ///
    /// Price formula for "free sell" (shipping bin / vendor):
    ///   P_final = floor( P_vanilla * M_global * M_category * M_saturation * M_seasonal * M_quality_margin )
    ///
    /// Where:
    ///   M_global      = GlobalSellMultiplier (default 0.10 — the deflationary lever)
    ///   M_category    = per-category tuning knob
    ///   M_saturation  = (1 - V/Cmax)^2 from volumetric bottle model
    ///   M_seasonal    = in-season penalty / out-of-season bonus
    ///   M_quality     = handled at bottle level (quality margin multiplier)
    ///
    /// Contract pricing bypasses M_global via IsCalculatingContractPrice flag.
    /// </summary>
    public class EconomyEngine
    {
        /// <summary>
        /// Compute the final price multiplier to apply to an item's sale price.
        /// This is called by Harmony postfix patches on Object.sellToStorePrice.
        /// </summary>
        public static float GetPriceMultiplier(Item item)
        {
            var config = ModConfig.GetInstance();

            // ── Global deflationary multiplier ──
            double multiplier = config.GlobalSellMultiplier;

            // When calculating contract reward pricing, bypass the global deflation
            if (MarketManager.IsCalculatingContractPrice)
            {
                multiplier = 1.0;
            }

            // ── Category base multiplier ──
            multiplier *= GetCategoryMultiplier(item, config);

            // ── Volumetric saturation multiplier (bottle model) ──
            multiplier *= MarketManager.GetSaturationMultiplier(item);

            // ── Seasonal pricing ──
            if (config.EnableSeasonalPricing)
                multiplier *= GetSeasonalMultiplier(item, config);

            // ── Day-of-week pricing ──
            if (config.EnableDayOfWeekPricing)
                multiplier *= GetDayOfWeekMultiplier(config);

            // ── Per-item override (stacks on everything) ──
            if (item != null && config.ItemSpecificOverrides.TryGetValue(item.QualifiedItemId, out double itemOverride))
                multiplier *= itemOverride;

            return (float)Math.Max(0.01, multiplier);
        }

        /// <summary>
        /// Get the quality-aware margin multiplier. Higher quality items get a
        /// bonus margin from the bottle system rather than vanilla's flat 25%.
        /// </summary>
        public static float GetQualityMarginMultiplier(int quality)
        {
            var config = ModConfig.GetInstance();
            float multiplier = quality switch
            {
                0 => 1.0f,
                1 => (float)config.SilverMarginMultiplier,
                2 => (float)config.GoldMarginMultiplier,
                4 => (float)config.IridiumMarginMultiplier,
                _ => 1.0f
            };

            // LuxuryBrand profession: Iridium quality gets 3x margin instead of configured value
            if (quality == 4 && ReputationSkill.HasProfession("LuxuryBrand"))
            {
                multiplier = 3.0f;
            }

            return multiplier;
        }

        // ── Category multiplier ──
        private static double GetCategoryMultiplier(Item item, ModConfig config)
        {
            if (item == null) return config.DefaultMultiplier;

            return item.Category switch
            {
                -75 => config.CropMultiplier,          // Vegetables
                -79 => config.CropMultiplier,          // Fruits
                -80 => config.CropMultiplier,          // Flowers
                -81 => config.ForageMultiplier,        // Forage
                -26 => config.ArtisanGoodsMultiplier,  // Artisan Goods
                -5  => config.AnimalProductMultiplier, // Animal Products (Eggs)
                -6  => config.AnimalProductMultiplier, // Animal Products (Milk)
                -18 => config.AnimalProductMultiplier, // Animal Products
                -4  => config.FishMultiplier,          // Fish
                -2  => config.MineralsMultiplier,      // Gems
                -12 => config.MineralsMultiplier,      // Minerals
                -7  => config.CookingMultiplier,       // Cooking
                _ => config.DefaultMultiplier
            };
        }

        // ── Seasonal multiplier ──
        private static double GetSeasonalMultiplier(Item item, ModConfig config)
        {
            if (item == null || !Game1.hasLoadedGame) return 1.0;

            int cat = item.Category;
            if (cat != -75 && cat != -79 && cat != -80 && cat != -81)
                return 1.0;

            string season = Game1.currentSeason;
            var tags = item.GetContextTags();
            bool isInSeason = tags != null && tags.Contains($"season_{season}");

            return isInSeason
                ? 1.0 - config.SeasonalPriceVariation
                : 1.0 + config.SeasonalPriceVariation;
        }

        // ── Day-of-week pricing ──
        private static double GetDayOfWeekMultiplier(ModConfig config)
        {
            if (!Game1.hasLoadedGame) return 1.0;

            int dayOfWeek = Game1.dayOfMonth % 7;
            if (dayOfWeek == 6 || dayOfWeek == 0)
                return 1.0 + config.WeekendPriceBonus;

            return 1.0;
        }
    }
}
