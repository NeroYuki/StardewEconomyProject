using System;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// The Ferngill Revenue Service (FRS) implements a tiered taxation system.
    /// Taxes are assessed on Day 1 of each season based on the previous season's activity.
    ///
    /// Tax types:
    /// 1. Progressive Income Tax — based on gross earnings
    /// 2. Utility Tax — based on sprinkler tiles + machine activations
    /// 3. Property Tax — based on building assessed values
    /// </summary>
    public class TaxManager
    {
        /// <summary>Tracking data persisted across days within a season.</summary>
        private static TaxTrackingData _tracking = new();

        /// <summary>Last assessed tax bill (for display/reference).</summary>
        private static TaxBill _lastBill;

        // ══════════════════════════════════════════════════════════════
        //  TRACKING
        // ══════════════════════════════════════════════════════════════

        /// <summary>Record income earned (called when player receives money from sales/contracts).</summary>
        public static void RecordIncome(int amount)
        {
            if (!ModConfig.GetInstance().EnableTaxation) return;
            _tracking.SeasonGrossIncome += amount;
        }

        /// <summary>Record a sprinkler-watered tile for utility tracking.</summary>
        public static void RecordSprinklerTile(int tileCount = 1)
        {
            if (!ModConfig.GetInstance().EnableTaxation) return;
            _tracking.SprinklerTilesWatered += tileCount;
        }

        /// <summary>Record a machine activation for utility tracking.</summary>
        public static void RecordMachineActivation(int count = 1)
        {
            if (!ModConfig.GetInstance().EnableTaxation) return;
            _tracking.MachineActivations += count;
        }

        // ══════════════════════════════════════════════════════════════
        //  TAX ASSESSMENT (Day 1 of each season)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called on Day 1 of each new season. Calculates and applies taxes
        /// from the previous season's activity.
        /// </summary>
        public static void AssessSeasonalTaxes()
        {
            if (!ModConfig.GetInstance().EnableTaxation) return;
            if (Game1.dayOfMonth != 1) return;

            var config = ModConfig.GetInstance();
            var bill = new TaxBill();

            // 1. Progressive Income Tax
            bill.IncomeTax = CalculateIncomeTax(_tracking.SeasonGrossIncome, config);

            // 2. Utility Tax
            bill.UtilityTax = CalculateUtilityTax(_tracking, config);

            // 3. Property Tax
            bill.PropertyTax = CalculatePropertyTax(config);

            bill.TotalTax = bill.IncomeTax + bill.UtilityTax + bill.PropertyTax;
            bill.GrossIncome = _tracking.SeasonGrossIncome;
            bill.Season = Game1.currentSeason;

            // Apply tax (deduct from player money)
            if (bill.TotalTax > 0)
            {
                Game1.player.Money = Math.Max(0, Game1.player.Money - bill.TotalTax);

                // Show tax bill notification
                string message = $"=== Ferngill Revenue Service ===\n" +
                                 $"Seasonal Tax Assessment\n" +
                                 $"Income Tax: {bill.IncomeTax:N0}g\n" +
                                 $"Utility Tax: {bill.UtilityTax:N0}g\n" +
                                 $"Property Tax: {bill.PropertyTax:N0}g\n" +
                                 $"─────────────────\n" +
                                 $"Total Due: {bill.TotalTax:N0}g";

                Game1.addHUDMessage(new HUDMessage(
                    $"FRS Tax Bill: {bill.TotalTax:N0}g deducted.",
                    HUDMessage.error_type));

                LogHelper.Info($"[FRS] Tax bill: Income={bill.IncomeTax}g, Utility={bill.UtilityTax}g, Property={bill.PropertyTax}g, Total={bill.TotalTax}g");

                // Check for debt spiral
                if (Game1.player.Money <= 0 && bill.TotalTax > 0)
                {
                    LogHelper.Warn("[FRS] Player is in debt! Consider repossession logic.");
                    Game1.addHUDMessage(new HUDMessage(
                        "Warning: You are in debt to the FRS!",
                        HUDMessage.error_type));
                }
            }

            _lastBill = bill;

            // Reset tracking for new season
            _tracking = new TaxTrackingData();
        }

        // ══════════════════════════════════════════════════════════════
        //  TAX CALCULATIONS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Progressive income tax using bracket model.
        /// Tier 1: 0g - 5,000g → 0% (Tax Holiday)
        /// Tier 2: 5,001g - 25,000g → 10%
        /// Tier 3: 25,001g - 100,000g → 20%
        /// Tier 4: 100,001g+ → 35%
        /// </summary>
        private static int CalculateIncomeTax(int grossIncome, ModConfig config)
        {
            if (grossIncome <= 0) return 0;

            double tax = 0;
            double remaining = grossIncome;

            // Tier 1: 0 to threshold1 — tax holiday
            double tier1 = Math.Min(remaining, config.IncomeTaxTier1Threshold);
            tax += tier1 * config.IncomeTaxTier1Rate;
            remaining -= tier1;

            if (remaining <= 0) return (int)tax;

            // Tier 2: threshold1 to threshold2
            double tier2 = Math.Min(remaining, config.IncomeTaxTier2Threshold - config.IncomeTaxTier1Threshold);
            tax += tier2 * config.IncomeTaxTier2Rate;
            remaining -= tier2;

            if (remaining <= 0) return (int)tax;

            // Tier 3: threshold2 to threshold3
            double tier3 = Math.Min(remaining, config.IncomeTaxTier3Threshold - config.IncomeTaxTier2Threshold);
            tax += tier3 * config.IncomeTaxTier3Rate;
            remaining -= tier3;

            if (remaining <= 0) return (int)tax;

            // Tier 4: everything above threshold3
            tax += remaining * config.IncomeTaxTier4Rate;

            // Reputation-based tax discount
            int repLevel = ReputationSkill.GetLevel(Game1.player);
            if (repLevel >= 8)
            {
                tax *= 0.95; // -5% at reputation level 8+
            }

            return (int)tax;
        }

        /// <summary>
        /// Utility tax based on sprinkler usage and machine activations.
        /// Sprinklers: tiles * WaterUsageFee * days (28)
        /// Machines: activations * MachineUsageFee
        /// </summary>
        private static int CalculateUtilityTax(TaxTrackingData tracking, ModConfig config)
        {
            double waterCost = tracking.SprinklerTilesWatered * config.WaterUsageFee;
            double machineCost = tracking.MachineActivations * config.MachineUsageFee;
            return (int)(waterCost + machineCost);
        }

        /// <summary>
        /// Property tax based on assessed building values.
        /// Total = Σ(building assessed value) * PropertyTaxRate
        /// </summary>
        private static int CalculatePropertyTax(ModConfig config)
        {
            int totalValue = 0;

            if (Game1.getFarm() != null)
            {
                foreach (var building in Game1.getFarm().buildings)
                {
                    totalValue += GetBuildingAssessedValue(building.buildingType.Value);
                }

                // Farmhouse upgrade value
                totalValue += Game1.player.HouseUpgradeLevel * 25000;
            }

            return (int)(totalValue * config.PropertyTaxRate);
        }

        /// <summary>Get the assessed value of a building type for property tax.</summary>
        private static int GetBuildingAssessedValue(string buildingType)
        {
            return buildingType switch
            {
                "Coop" => 4000,
                "Big Coop" => 10000,
                "Deluxe Coop" => 20000,
                "Barn" => 6000,
                "Big Barn" => 12000,
                "Deluxe Barn" => 25000,
                "Silo" => 1000,
                "Mill" => 2500,
                "Shed" => 15000,
                "Big Shed" => 30000,
                "Well" => 1000,
                "Stable" => 10000,
                "Slime Hutch" => 10000,
                "Fish Pond" => 5000,
                "Greenhouse" => 50000,
                "Shipping Bin" => 250,
                "Cabin" => 10000,
                "Obelisk" or "Earth Obelisk" or "Water Obelisk" or "Desert Obelisk" or "Island Obelisk" => 100000,
                "Gold Clock" => 1000000,
                "Junimo Hut" => 20000,
                _ => 5000
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Get the last tax bill for display.</summary>
        public static TaxBill GetLastBill() => _lastBill;

        /// <summary>Get current season's tracking data.</summary>
        public static TaxTrackingData GetCurrentTracking() => _tracking;

        /// <summary>Estimate the current season's tax bill (for forecasting).</summary>
        public static TaxBill EstimateCurrentTax()
        {
            var config = ModConfig.GetInstance();
            var bill = new TaxBill
            {
                IncomeTax = CalculateIncomeTax(_tracking.SeasonGrossIncome, config),
                UtilityTax = CalculateUtilityTax(_tracking, config),
                PropertyTax = CalculatePropertyTax(config),
                GrossIncome = _tracking.SeasonGrossIncome,
                Season = Game1.currentSeason
            };
            bill.TotalTax = bill.IncomeTax + bill.UtilityTax + bill.PropertyTax;
            return bill;
        }

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            var data = new TaxSaveData
            {
                Tracking = _tracking,
                LastBill = _lastBill,
            };
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public static void Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _tracking = new TaxTrackingData();
                _lastBill = null;
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<TaxSaveData>(json);
                _tracking = data?.Tracking ?? new TaxTrackingData();
                _lastBill = data?.LastBill;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[FRS] Failed to deserialize:\n{ex}");
                _tracking = new TaxTrackingData();
            }
        }

        public static void Reset()
        {
            _tracking = new TaxTrackingData();
            _lastBill = null;
        }

        // ══════════════════════════════════════════════════════════════
        //  DATA MODELS
        // ══════════════════════════════════════════════════════════════

        public class TaxTrackingData
        {
            public int SeasonGrossIncome { get; set; } = 0;
            public int SprinklerTilesWatered { get; set; } = 0;
            public int MachineActivations { get; set; } = 0;
        }

        public class TaxBill
        {
            public int IncomeTax { get; set; }
            public int UtilityTax { get; set; }
            public int PropertyTax { get; set; }
            public int TotalTax { get; set; }
            public int GrossIncome { get; set; }
            public string Season { get; set; }
        }

        private class TaxSaveData
        {
            public TaxTrackingData Tracking { get; set; }
            public TaxBill LastBill { get; set; }
        }
    }
}
