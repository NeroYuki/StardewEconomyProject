using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StardewEconomyProject
{
    /// <summary>
    /// Mod configuration loaded from config.json via SMAPI's config API.
    /// Implements all configurable parameters from the SEP design specification.
    /// </summary>
    public class ModConfig
    {
        // ══════════════════════════════════════════════════════════════
        //  GLOBAL DEFLATIONARY ENGINE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// The primary deflation lever. Multiplies all "free sell" prices.
        /// Default 0.10 means items sell for 10% of vanilla price via shipping bin / vendors.
        /// Domain: [0.01 - 1.0]
        /// </summary>
        public double GlobalSellMultiplier { get; set; } = 0.10;

        // ══════════════════════════════════════════════════════════════
        //  CATEGORY BASE MULTIPLIERS (stacked on top of global)
        // ══════════════════════════════════════════════════════════════

        public double CropMultiplier { get; set; } = 1.0;
        public double ForageMultiplier { get; set; } = 1.0;
        public double ArtisanGoodsMultiplier { get; set; } = 1.0;
        public double AnimalProductMultiplier { get; set; } = 1.0;
        public double FishMultiplier { get; set; } = 1.0;
        public double MineralsMultiplier { get; set; } = 1.0;
        public double CookingMultiplier { get; set; } = 1.0;
        public double DefaultMultiplier { get; set; } = 1.0;

        // ══════════════════════════════════════════════════════════════
        //  CONTRACT SYSTEM
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Multiplier applied to contract rewards. 1.35 = 135% of vanilla base price.
        /// Domain: [1.0 - 5.0]
        /// </summary>
        public double ContractPremium { get; set; } = 1.35;

        /// <summary>
        /// Penalty multiplier for unfulfilled portion of contracts.
        /// Domain: [0.0 - 1.0]
        /// </summary>
        public double ContractPenaltyMultiplier { get; set; } = 0.25;

        /// <summary>Maximum number of active contracts the player may hold.</summary>
        public int MaxActiveContracts { get; set; } = 3;

        /// <summary>Number of contracts available on the board each refresh.</summary>
        public int ContractsPerRefresh { get; set; } = 5;

        /// <summary>Days between contract board refreshes.</summary>
        public int ContractRefreshDays { get; set; } = 7;

        // ══════════════════════════════════════════════════════════════
        //  VOLUMETRIC SATURATION ("BOTTLE") MODEL
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Regional competition factor. Controls pre-fill of in-season bottles.
        /// 0.50 means 40-70% pre-fill during growing season. Domain: [0.0 - 1.0]
        /// </summary>
        public double RegionalCompetition { get; set; } = 0.50;

        /// <summary>
        /// Base daily drain rate as fraction of max capacity. Domain: [0.01 - 0.2]
        /// </summary>
        public double MarketDrainRate { get; set; } = 0.05;

        /// <summary>
        /// Probability per day that a "Surge" event occurs (drain * 5-10x). Domain: [0.0 - 0.1]
        /// </summary>
        public double SurgeProbability { get; set; } = 0.02;

        /// <summary>Base market bottle capacity for normal-quality crops.</summary>
        public double BaseBottleCapacity { get; set; } = 1000.0;

        // ══════════════════════════════════════════════════════════════
        //  QUALITY STRATIFICATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>Capacity scalar for Silver quality sub-market.</summary>
        public double SilverCapacityScalar { get; set; } = 0.20;
        /// <summary>Capacity scalar for Gold quality sub-market.</summary>
        public double GoldCapacityScalar { get; set; } = 0.10;
        /// <summary>Capacity scalar for Iridium quality sub-market.</summary>
        public double IridiumCapacityScalar { get; set; } = 0.05;

        /// <summary>Base margin multiplier for Silver quality items.</summary>
        public double SilverMarginMultiplier { get; set; } = 1.2;
        /// <summary>Base margin multiplier for Gold quality items.</summary>
        public double GoldMarginMultiplier { get; set; } = 1.5;
        /// <summary>Base margin multiplier for Iridium quality items.</summary>
        public double IridiumMarginMultiplier { get; set; } = 2.0;

        // ══════════════════════════════════════════════════════════════
        //  BARGAINING SYSTEM (Rubinstein Model)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Stubbornness coefficient (k) for counter-offer acceptance probability.
        /// Higher = more risk of rejection. Domain: [0.1 - 1.0]
        /// </summary>
        public double BargainingStubbornness { get; set; } = 0.35;

        /// <summary>Cooldown in hours before an NPC will make another offer after rejection.</summary>
        public int BargainingCooldownHours { get; set; } = 48;

        /// <summary>Days given for delivery after accepting a bargaining deal.</summary>
        public int BargainingDeliveryDays { get; set; } = 2;

        // ══════════════════════════════════════════════════════════════
        //  TAXATION (Ferngill Revenue Service)
        // ══════════════════════════════════════════════════════════════

        /// <summary>Enable/disable the taxation system.</summary>
        public bool EnableTaxation { get; set; } = true;

        /// <summary>Progressive income tax bracket thresholds and rates.</summary>
        public double IncomeTaxTier1Threshold { get; set; } = 5000;
        public double IncomeTaxTier1Rate { get; set; } = 0.0;
        public double IncomeTaxTier2Threshold { get; set; } = 25000;
        public double IncomeTaxTier2Rate { get; set; } = 0.10;
        public double IncomeTaxTier3Threshold { get; set; } = 100000;
        public double IncomeTaxTier3Rate { get; set; } = 0.20;
        public double IncomeTaxTier4Rate { get; set; } = 0.35;

        /// <summary>Cost per tile watered per day by sprinklers.</summary>
        public double WaterUsageFee { get; set; } = 0.25;

        /// <summary>Cost per machine activation (non-fuel machines).</summary>
        public double MachineUsageFee { get; set; } = 5.0;

        /// <summary>Percentage of total building assessed value taxed per season.</summary>
        public double PropertyTaxRate { get; set; } = 0.02;

        // ══════════════════════════════════════════════════════════════
        //  BANKING SYSTEM
        // ══════════════════════════════════════════════════════════════

        /// <summary>Enable/disable the banking system.</summary>
        public bool EnableBanking { get; set; } = true;

        /// <summary>Daily interest rate on savings. Domain: [0.0 - 0.01]</summary>
        public double BaseInterestRate { get; set; } = 0.001;

        /// <summary>Daily interest rate multipliers for fixed-term deposits, keyed by lock-in period in seasons.</summary>
        public double FixedTermRate1Season   { get; set; } = 1.5;
        public double FixedTermRate2Seasons  { get; set; } = 2.0;
        public double FixedTermRate4Seasons  { get; set; } = 3.0;
        public double FixedTermRate8Seasons  { get; set; } = 4.5;
        public double FixedTermRate12Seasons { get; set; } = 6.5;

        /// <summary>Loan interest rate per season. Domain: [0.01 - 0.2]</summary>
        public double LoanInterestRate { get; set; } = 0.05;

        /// <summary>Maximum loan amount available.</summary>
        public int MaxLoanAmount { get; set; } = 100000;

        // ══════════════════════════════════════════════════════════════
        //  ARTISAN MACHINE OPERATION COSTS
        // ══════════════════════════════════════════════════════════════

        public int KegOperationCost { get; set; } = 150;
        public int PreservesJarOperationCost { get; set; } = 50;
        public int DehydratorOperationCost { get; set; } = 25;
        public int FishSmokerOperationCost { get; set; } = 80;

        // ══════════════════════════════════════════════════════════════
        //  SEASONAL & LEGACY FEATURES
        // ══════════════════════════════════════════════════════════════

        public bool EnableSeasonalPricing { get; set; } = true;
        public double SeasonalPriceVariation { get; set; } = 0.25;
        public bool EnableDayOfWeekPricing { get; set; } = false;
        public double WeekendPriceBonus { get; set; } = 0.1;

        // ══════════════════════════════════════════════════════════════
        //  CATEGORY-SPECIFIC DRAIN RATES
        // ══════════════════════════════════════════════════════════════

        public double VegetableDrainRate { get; set; } = 0.05;
        public double FruitDrainRate { get; set; } = 0.03;
        public double LuxuryFruitDrainRate { get; set; } = 0.02;
        public double FlowerDrainRate { get; set; } = 0.04;
        public double ArtisanDrainRate { get; set; } = 0.03;
        public double AnimalProductDrainRate { get; set; } = 0.04;
        public double FishDrainRate { get; set; } = 0.05;
        public double MineralDrainRate { get; set; } = 0.02;

        // ══════════════════════════════════════════════════════════════
        //  PER-ITEM OVERRIDES
        // ══════════════════════════════════════════════════════════════

        public Dictionary<string, double> ItemSpecificOverrides { get; set; } = new();

        // ══════════════════════════════════════════════════════════════
        //  KEYBINDS — Open Economy Menus
        // ══════════════════════════════════════════════════════════════

        /// <summary>Key to open the Contract Board menu.</summary>
        public SButton ContractBoardKey { get; set; } = SButton.F5;

        /// <summary>Key to open the Bank menu.</summary>
        public SButton BankKey { get; set; } = SButton.F6;

        /// <summary>Key to open the Tax Bill menu.</summary>
        public SButton TaxBillKey { get; set; } = SButton.F7;

        /// <summary>Key to open the Bargain / Trade Offers menu.</summary>
        public SButton BargainKey { get; set; } = SButton.F8;

        /// <summary>Key to open the Supercomputer 14-day Forecast menu.</summary>
        public SButton ForecastKey { get; set; } = SButton.F9;

        // ══════════════════════════════════════════════════════════════
        //  UI
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Scale factor applied to row heights and vertical spacing in all economy menus.
        /// 1.0 = compact (original), 1.5 = recommended default, 2.0 = very spacious.
        /// Domain: [0.5 – 3.0]
        /// </summary>
        public float UiSpacingScale { get; set; } = 1.5f;

        // ══════════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════════

        private static ModConfig _instance;
        public static ModConfig GetInstance()
        {
            _instance ??= new ModConfig();
            return _instance;
        }

        public void SetConfig(ModConfig config)
        {
            _instance = config;
        }
    }
}
