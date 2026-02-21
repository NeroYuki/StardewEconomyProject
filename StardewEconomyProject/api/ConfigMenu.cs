using System;
using GenericModConfigMenu;
using StardewModdingAPI;

namespace StardewEconomyProject.api
{
    /// <summary>
    /// Handles registration of all mod options with Generic Mod Config Menu (GMCM).
    /// </summary>
    public class ConfigMenu
    {
        private readonly IManifest _manifest;
        private readonly IModHelper _helper;
        private readonly IGenericModConfigMenuApi _api;

        private ConfigMenu(IManifest manifest, IModHelper helper, IGenericModConfigMenuApi api)
        {
            _manifest = manifest;
            _helper = helper;
            _api = api;
        }

        public static void Setup(IManifest manifest, IModHelper helper)
        {
            var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api == null)
            {
                LogHelper.Trace("[ConfigMenu] Generic Mod Config Menu not found — skipping integration.");
                return;
            }

            new ConfigMenu(manifest, helper, api).Register();
            LogHelper.Info("[ConfigMenu] Registered config menu with Generic Mod Config Menu.");
        }

        private void Register()
        {
            _api.Register(
                mod: _manifest,
                reset: () => ModConfig.GetInstance().SetConfig(new ModConfig()),
                save: () => _helper.WriteConfig(ModConfig.GetInstance())
            );

            AddGlobalSection();
            AddCategoryMultiplierSection();
            AddMarketBottleSection();
            AddContractSection();
            AddBargainingSection();
            AddTaxSection();
            AddBankSection();
            AddArtisanMachineSection();
            AddSeasonalPricingSection();
            AddUiSection();
            AddKeybindSection();
        }

        // ── Section: Global Deflationary Engine ──
        private void AddGlobalSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Global Deflationary Engine",
                () => "The primary lever: reduces all free sell prices.");

            _api.AddNumberOption(_manifest,
                getValue: () => (float)cfg.GlobalSellMultiplier,
                setValue: v => cfg.GlobalSellMultiplier = v,
                name: () => "Global Sell Multiplier",
                tooltip: () => "All shipping/vendor sell prices are multiplied by this. 0.10 = items sell for 10% of vanilla price.",
                min: 0.01f, max: 1.0f, interval: 0.01f);
        }

        // ── Section: Category Multipliers ──
        private void AddCategoryMultiplierSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Category Price Multipliers",
                () => "Per-category multipliers stacked on the global multiplier.");

            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.CropMultiplier, setValue: v => cfg.CropMultiplier = v,
                name: () => "Crops", tooltip: () => "Vegetables, fruits, and flowers.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.ForageMultiplier, setValue: v => cfg.ForageMultiplier = v,
                name: () => "Forage", tooltip: () => "Foraged items.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.ArtisanGoodsMultiplier, setValue: v => cfg.ArtisanGoodsMultiplier = v,
                name: () => "Artisan Goods", tooltip: () => "Wine, cheese, jam, etc.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.AnimalProductMultiplier, setValue: v => cfg.AnimalProductMultiplier = v,
                name: () => "Animal Products", tooltip: () => "Eggs, milk, wool.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FishMultiplier, setValue: v => cfg.FishMultiplier = v,
                name: () => "Fish", tooltip: () => "All fish.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.MineralsMultiplier, setValue: v => cfg.MineralsMultiplier = v,
                name: () => "Minerals & Gems", tooltip: () => "Gems and minerals from the mines.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.CookingMultiplier, setValue: v => cfg.CookingMultiplier = v,
                name: () => "Cooking", tooltip: () => "Cooked dishes.", min: 0.1f, max: 5.0f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.DefaultMultiplier, setValue: v => cfg.DefaultMultiplier = v,
                name: () => "Default", tooltip: () => "All other items.", min: 0.1f, max: 5.0f, interval: 0.05f);
        }

        // ── Section: Market Bottles ──
        private void AddMarketBottleSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Volumetric Market (Bottles)",
                () => "Controls the saturation-based pricing system.");

            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.BaseBottleCapacity, setValue: v => cfg.BaseBottleCapacity = v,
                name: () => "Base Bottle Capacity", tooltip: () => "Max capacity for normal quality bottles.", min: 100f, max: 10000f, interval: 100f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.MarketDrainRate, setValue: v => cfg.MarketDrainRate = v,
                name: () => "Daily Drain Rate", tooltip: () => "Fraction of capacity drained per day.", min: 0.01f, max: 0.2f, interval: 0.01f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.RegionalCompetition, setValue: v => cfg.RegionalCompetition = v,
                name: () => "Regional Competition", tooltip: () => "Controls pre-fill of in-season bottles at season start.", min: 0f, max: 1f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.SurgeProbability, setValue: v => cfg.SurgeProbability = v,
                name: () => "Surge Event Probability", tooltip: () => "Daily chance of a demand surge (drains bottle 5-10x faster).", min: 0f, max: 0.1f, interval: 0.005f);

            _api.AddSectionTitle(_manifest, () => "Quality Capacity Scalars");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.SilverCapacityScalar, setValue: v => cfg.SilverCapacityScalar = v,
                name: () => "Silver", tooltip: () => "Capacity of silver bottles relative to normal.", min: 0.01f, max: 1f, interval: 0.01f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.GoldCapacityScalar, setValue: v => cfg.GoldCapacityScalar = v,
                name: () => "Gold", tooltip: () => "Capacity of gold bottles relative to normal.", min: 0.01f, max: 1f, interval: 0.01f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.IridiumCapacityScalar, setValue: v => cfg.IridiumCapacityScalar = v,
                name: () => "Iridium", tooltip: () => "Capacity of iridium bottles relative to normal.", min: 0.01f, max: 1f, interval: 0.01f);

            _api.AddSectionTitle(_manifest, () => "Quality Margin Multipliers");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.SilverMarginMultiplier, setValue: v => cfg.SilverMarginMultiplier = v,
                name: () => "Silver Margin", tooltip: () => "Price multiplier for silver quality items.", min: 1f, max: 3f, interval: 0.1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.GoldMarginMultiplier, setValue: v => cfg.GoldMarginMultiplier = v,
                name: () => "Gold Margin", tooltip: () => "Price multiplier for gold quality items.", min: 1f, max: 4f, interval: 0.1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.IridiumMarginMultiplier, setValue: v => cfg.IridiumMarginMultiplier = v,
                name: () => "Iridium Margin", tooltip: () => "Price multiplier for iridium quality items.", min: 1f, max: 5f, interval: 0.1f);
        }

        // ── Section: Contracts ──
        private void AddContractSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Contract System",
                () => "NPC delivery contracts as alternative sell method.");

            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.ContractPremium, setValue: v => cfg.ContractPremium = v,
                name: () => "Contract Premium", tooltip: () => "Reward multiplier over vanilla base price. 1.35 = 135%.", min: 1f, max: 5f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.ContractPenaltyMultiplier, setValue: v => cfg.ContractPenaltyMultiplier = v,
                name: () => "Penalty Multiplier", tooltip: () => "Penalty as fraction of reward for unfulfilled contracts.", min: 0f, max: 1f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.MaxActiveContracts, setValue: v => cfg.MaxActiveContracts = (int)v,
                name: () => "Max Active Contracts", min: 1f, max: 10f, interval: 1f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.ContractsPerRefresh, setValue: v => cfg.ContractsPerRefresh = (int)v,
                name: () => "Contracts Per Refresh", min: 1f, max: 15f, interval: 1f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.ContractRefreshDays, setValue: v => cfg.ContractRefreshDays = (int)v,
                name: () => "Refresh Interval (days)", min: 1f, max: 28f, interval: 1f);
        }

        // ── Section: Bargaining ──
        private void AddBargainingSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Bargaining System",
                () => "Rubinstein alternating-offer model for NPC trades.");

            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.BargainingStubbornness, setValue: v => cfg.BargainingStubbornness = v,
                name: () => "Stubbornness (k)", tooltip: () => "Higher = harder to counter-offer successfully.", min: 0.1f, max: 1f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.BargainingCooldownHours, setValue: v => cfg.BargainingCooldownHours = (int)v,
                name: () => "Cooldown (hours)", tooltip: () => "Hours before a rejected NPC offers again.", min: 12f, max: 168f, interval: 12f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.BargainingDeliveryDays, setValue: v => cfg.BargainingDeliveryDays = (int)v,
                name: () => "Delivery Days", tooltip: () => "Days to deliver after accepting a deal.", min: 1f, max: 7f, interval: 1f);
        }

        // ── Section: Taxation ──
        private void AddTaxSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Ferngill Revenue Service",
                () => "Progressive income, utility, and property taxes.");

            _api.AddBoolOption(_manifest, getValue: () => cfg.EnableTaxation, setValue: v => cfg.EnableTaxation = v,
                name: () => "Enable Taxation");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.IncomeTaxTier4Rate, setValue: v => cfg.IncomeTaxTier4Rate = v,
                name: () => "Top Income Tax Rate", tooltip: () => "Highest marginal tax rate.", min: 0f, max: 0.5f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.WaterUsageFee, setValue: v => cfg.WaterUsageFee = v,
                name: () => "Water Usage Fee", tooltip: () => "Cost per sprinkler tile per day.", min: 0f, max: 2f, interval: 0.05f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.MachineUsageFee, setValue: v => cfg.MachineUsageFee = v,
                name: () => "Machine Usage Fee", tooltip: () => "Cost per machine activation.", min: 0f, max: 50f, interval: 1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.PropertyTaxRate, setValue: v => cfg.PropertyTaxRate = v,
                name: () => "Property Tax Rate", tooltip: () => "Seasonal rate on building assessed value.", min: 0f, max: 0.1f, interval: 0.005f);
        }

        // ── Section: Banking ──
        private void AddBankSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Banking System",
                () => "Savings, fixed-term deposits, and loans.");

            _api.AddBoolOption(_manifest, getValue: () => cfg.EnableBanking, setValue: v => cfg.EnableBanking = v,
                name: () => "Enable Banking");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.BaseInterestRate, setValue: v => cfg.BaseInterestRate = v,
                name: () => "Daily Interest Rate", tooltip: () => "Daily savings interest.", min: 0f, max: 0.01f, interval: 0.0001f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.LoanInterestRate, setValue: v => cfg.LoanInterestRate = v,
                name: () => "Loan Interest Rate", tooltip: () => "Seasonal loan interest rate.", min: 0.01f, max: 0.2f, interval: 0.01f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.MaxLoanAmount, setValue: v => cfg.MaxLoanAmount = (int)v,
                name: () => "Max Loan Amount", min: 10000f, max: 1000000f, interval: 10000f);

            _api.AddSectionTitle(_manifest, () => "Fixed-Term Interest Rate Multipliers",
                () => "Daily interest rate multiplier per lock-in period (stacked on Base Rate).");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FixedTermRate1Season, setValue: v => cfg.FixedTermRate1Season = v,
                name: () => "1 Season", tooltip: () => "x multiplier on daily base rate for 1-season (28-day) lock-in.", min: 1.0f, max: 10.0f, interval: 0.1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FixedTermRate2Seasons, setValue: v => cfg.FixedTermRate2Seasons = v,
                name: () => "2 Seasons", tooltip: () => "x multiplier on daily base rate for 2-season (56-day) lock-in.", min: 1.0f, max: 10.0f, interval: 0.1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FixedTermRate4Seasons, setValue: v => cfg.FixedTermRate4Seasons = v,
                name: () => "4 Seasons", tooltip: () => "x multiplier on daily base rate for 4-season (112-day) lock-in.", min: 1.0f, max: 10.0f, interval: 0.1f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FixedTermRate8Seasons, setValue: v => cfg.FixedTermRate8Seasons = v,
                name: () => "8 Seasons", tooltip: () => "x multiplier on daily base rate for 8-season (224-day) lock-in.", min: 1.0f, max: 15.0f, interval: 0.5f);
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.FixedTermRate12Seasons, setValue: v => cfg.FixedTermRate12Seasons = v,
                name: () => "12 Seasons", tooltip: () => "x multiplier on daily base rate for 12-season (336-day) lock-in.", min: 1.0f, max: 20.0f, interval: 0.5f);
        }

        // ── Section: Artisan Machine Costs ──
        private void AddArtisanMachineSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Artisan Machine Costs",
                () => "Gold deducted per machine activation.");

            _api.AddNumberOption(_manifest, getValue: () => cfg.KegOperationCost, setValue: v => cfg.KegOperationCost = (int)v,
                name: () => "Keg Cost", min: 0f, max: 500f, interval: 10f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.PreservesJarOperationCost, setValue: v => cfg.PreservesJarOperationCost = (int)v,
                name: () => "Preserves Jar Cost", min: 0f, max: 500f, interval: 10f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.DehydratorOperationCost, setValue: v => cfg.DehydratorOperationCost = (int)v,
                name: () => "Dehydrator Cost", min: 0f, max: 500f, interval: 5f);
            _api.AddNumberOption(_manifest, getValue: () => cfg.FishSmokerOperationCost, setValue: v => cfg.FishSmokerOperationCost = (int)v,
                name: () => "Fish Smoker Cost", min: 0f, max: 500f, interval: 10f);
        }

        // ── Section: Seasonal Pricing ──
        private void AddSeasonalPricingSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Seasonal Pricing",
                () => "In-season crops sell for less, out-of-season get a premium.");

            _api.AddBoolOption(_manifest, getValue: () => cfg.EnableSeasonalPricing, setValue: v => cfg.EnableSeasonalPricing = v,
                name: () => "Enable Seasonal Pricing");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.SeasonalPriceVariation, setValue: v => cfg.SeasonalPriceVariation = v,
                name: () => "Seasonal Variation", tooltip: () => "0.25 = ±25% swing.", min: 0.05f, max: 0.75f, interval: 0.05f);

            _api.AddBoolOption(_manifest, getValue: () => cfg.EnableDayOfWeekPricing, setValue: v => cfg.EnableDayOfWeekPricing = v,
                name: () => "Enable Day-of-Week Pricing");
            _api.AddNumberOption(_manifest, getValue: () => (float)cfg.WeekendPriceBonus, setValue: v => cfg.WeekendPriceBonus = v,
                name: () => "Weekend Bonus", tooltip: () => "Extra price on weekends.", min: 0f, max: 0.5f, interval: 0.05f);
        }

        // ── Section: UI ──
        private void AddUiSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Menu UI",
                () => "Visual spacing for all economy menus.");

            _api.AddNumberOption(_manifest,
                getValue: () => cfg.UiSpacingScale,
                setValue: v => cfg.UiSpacingScale = v,
                name: () => "Row Spacing Scale",
                tooltip: () => "Multiplier for row heights and vertical padding in all menus. 1.0 = compact, 1.5 = default, 2.0 = spacious.",
                min: 0.5f, max: 3.0f, interval: 0.1f);
        }

        // ── Section: Keybinds ──
        private void AddKeybindSection()
        {
            var cfg = ModConfig.GetInstance();

            _api.AddSectionTitle(_manifest, () => "Keybinds",
                () => "Keyboard shortcuts for opening economy menus.");

            _api.AddKeybind(_manifest, getValue: () => cfg.ContractBoardKey, setValue: v => cfg.ContractBoardKey = v,
                name: () => "Contract Board", tooltip: () => "Open the contract board menu.");
            _api.AddKeybind(_manifest, getValue: () => cfg.BankKey, setValue: v => cfg.BankKey = v,
                name: () => "Bank", tooltip: () => "Open the banking menu.");
            _api.AddKeybind(_manifest, getValue: () => cfg.TaxBillKey, setValue: v => cfg.TaxBillKey = v,
                name: () => "Tax Bill", tooltip: () => "Open the tax assessment menu.");
            _api.AddKeybind(_manifest, getValue: () => cfg.BargainKey, setValue: v => cfg.BargainKey = v,
                name: () => "Bargain Offers", tooltip: () => "Open the NPC trade offers menu.");
            _api.AddKeybind(_manifest, getValue: () => cfg.ForecastKey, setValue: v => cfg.ForecastKey = v,
                name: () => "Forecast", tooltip: () => "Open the 14-day market forecast (Supercomputer).");
        }
    }
}
