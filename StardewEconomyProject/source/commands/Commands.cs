using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewEconomyProject.source.economy;

namespace StardewEconomyProject.source.commands
{
    /// <summary>
    /// SMAPI console commands for all economy subsystems.
    /// </summary>
    public class Commands
    {
        private readonly IMonitor _monitor;

        public Commands(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void RegisterCommands(ICommandHelper cmd)
        {
            // ── General ──
            cmd.Add("sep_status", "Show economy engine status and active multipliers.", OnStatus);
            cmd.Add("sep_check", "Check price multiplier for the held item.", OnCheckHeldItem);

            // ── Market ──
            cmd.Add("sep_market", "Show all market bottle states.", OnMarket);
            cmd.Add("sep_forecast", "Forecast market for N days. Usage: sep_forecast [days]", OnForecast);

            // ── Contracts ──
            cmd.Add("sep_contracts", "List available and active contracts.", OnContracts);
            cmd.Add("sep_accept", "Accept a contract. Usage: sep_accept <id>", OnAcceptContract);

            // ── Bargaining ──
            cmd.Add("sep_offers", "Show current NPC bargaining offers.", OnOffers);
            cmd.Add("sep_offer_accept", "Accept a bargaining offer. Usage: sep_offer_accept <id>", OnOfferAccept);
            cmd.Add("sep_offer_reject", "Reject a bargaining offer. Usage: sep_offer_reject <id>", OnOfferReject);
            cmd.Add("sep_offer_counter", "Counter-offer a price. Usage: sep_offer_counter <id> <price>", OnOfferCounter);

            // ── Tax ──
            cmd.Add("sep_tax", "Show current tax tracking and estimate.", OnTax);

            // ── Bank ──
            cmd.Add("sep_bank", "Show bank account status.", OnBank);
            cmd.Add("sep_deposit", "Deposit gold into savings. Usage: sep_deposit <amount>", OnDeposit);
            cmd.Add("sep_withdraw", "Withdraw from savings. Usage: sep_withdraw <amount>", OnWithdraw);
            cmd.Add("sep_loan", "Take a loan. Usage: sep_loan <amount>", OnLoan);
            cmd.Add("sep_repay", "Manually repay loan. Usage: sep_repay <amount>", OnRepay);

            // ── Reputation ──
            cmd.Add("sep_reputation", "Show Reputation skill level and XP.", OnReputation);
            cmd.Add("sep_addxp", "Debug: add reputation XP. Usage: sep_addxp <amount>", OnAddXp);
        }

        // ══════════════════════════════════════════════════════════════
        //  GENERAL
        // ══════════════════════════════════════════════════════════════

        private void OnStatus(string command, string[] args)
        {
            var config = ModConfig.GetInstance();
            _monitor.Log("=== Stardew Economy Project Status ===", LogLevel.Info);
            _monitor.Log($"  Global Sell Multiplier:    {config.GlobalSellMultiplier:F2} ({config.GlobalSellMultiplier * 100:F0}% of vanilla)", LogLevel.Info);
            _monitor.Log($"  Crop Multiplier:          {config.CropMultiplier:F2}", LogLevel.Info);
            _monitor.Log($"  Artisan Goods Multiplier: {config.ArtisanGoodsMultiplier:F2}", LogLevel.Info);
            _monitor.Log($"  Animal Product Multiplier:{config.AnimalProductMultiplier:F2}", LogLevel.Info);
            _monitor.Log($"  Fish Multiplier:          {config.FishMultiplier:F2}", LogLevel.Info);
            _monitor.Log($"  Contract Premium:         {config.ContractPremium:F2}", LogLevel.Info);
            _monitor.Log($"  Taxation:                 {(config.EnableTaxation ? "ON" : "OFF")}", LogLevel.Info);
            _monitor.Log($"  Banking:                  {(config.EnableBanking ? "ON" : "OFF")}", LogLevel.Info);
            _monitor.Log($"  Seasonal Pricing:         {(config.EnableSeasonalPricing ? "ON" : "OFF")} (±{config.SeasonalPriceVariation:P0})", LogLevel.Info);

            int repLevel = ReputationSkill.GetLevel(Game1.player);
            string tier = repLevel >= 10 ? "International" : repLevel >= 5 ? "National" : "Regional";
            _monitor.Log($"  Reputation:               Lv{repLevel} ({tier})", LogLevel.Info);
        }

        private void OnCheckHeldItem(string command, string[] args)
        {
            var player = Game1.player;
            if (player?.CurrentItem == null)
            {
                _monitor.Log("You are not holding any item.", LogLevel.Warn);
                return;
            }

            var item = player.CurrentItem;
            float multiplier = EconomyEngine.GetPriceMultiplier(item);
            float saturation = MarketManager.GetSaturationMultiplier(item);
            int sellPrice = 0;
            if (item is StardewValley.Object obj)
                sellPrice = obj.sellToStorePrice(-1L);

            _monitor.Log($"=== Price Check: {item.Name} ===", LogLevel.Info);
            _monitor.Log($"  Qualified ID:    {item.QualifiedItemId}", LogLevel.Info);
            _monitor.Log($"  Category:        {item.Category}", LogLevel.Info);
            _monitor.Log($"  Quality:         {item.Quality}", LogLevel.Info);
            _monitor.Log($"  Economy Mult:    x{multiplier:F4}", LogLevel.Info);
            _monitor.Log($"  Saturation Mult: x{saturation:F4}", LogLevel.Info);
            _monitor.Log($"  Sell Price:      {sellPrice}g (per unit, post-economy)", LogLevel.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  MARKET
        // ══════════════════════════════════════════════════════════════

        private void OnMarket(string command, string[] args)
        {
            _monitor.Log("=== Market Bottle Status ===", LogLevel.Info);
            var bottles = MarketManager.GetAllBottles();
            if (bottles == null || bottles.Count == 0)
            {
                _monitor.Log("  No bottles initialized.", LogLevel.Info);
                return;
            }

            foreach (var kvp in bottles)
            {
                var bottle = kvp.Value;
                _monitor.Log($"  [{bottle.BottleId}] {bottle.CurrentVolume:F0}/{bottle.MaxCapacity:F0} " +
                             $"({bottle.Saturation:P0}) State={bottle.MarketState} DynMult={bottle.DynamicPriceMultiplier:F3}",
                    LogLevel.Info);
            }
        }

        private void OnForecast(string command, string[] args)
        {
            int days = 7;
            if (args.Length > 0 && int.TryParse(args[0], out int d))
                days = d;

            var forecast = MarketManager.ForecastMarket(days);
            _monitor.Log($"=== Market Forecast ({days} days) ===", LogLevel.Info);
            foreach (var entry in forecast)
            {
                _monitor.Log($"  {entry.Key}: {entry.Value:P0} avg saturation", LogLevel.Info);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CONTRACTS
        // ══════════════════════════════════════════════════════════════

        private void OnContracts(string command, string[] args)
        {
            _monitor.Log("=== Contract Board ===", LogLevel.Info);
            var available = ContractManager.GetAvailableContracts();
            var active = ContractManager.GetActiveContracts();

            _monitor.Log("  Available:", LogLevel.Info);
            if (available.Count == 0)
                _monitor.Log("    (none)", LogLevel.Info);
            foreach (var c in available)
            {
                string items = string.Join(", ", c.RequiredItems.Select(ri => $"{ri.Value}x {ri.Key}"));
                _monitor.Log($"    [{c.ContractId}] {c.RequesterNpc}: {items} " +
                             $"(min Q{c.MinimumQuality}) \u2192 {c.TotalReward}g in {c.DurationDays}d", LogLevel.Info);
            }

            _monitor.Log("  Active:", LogLevel.Info);
            if (active.Count == 0)
                _monitor.Log("    (none)", LogLevel.Info);
            foreach (var c in active)
            {
                string items = string.Join(", ", c.RequiredItems.Select(ri =>
                {
                    c.DeliveredItems.TryGetValue(ri.Key, out int delivered_);
                    int delivered = delivered_;
                    return $"{delivered}/{ri.Value}x {ri.Key}";
                }));
                _monitor.Log($"    [{c.ContractId}] {c.RequesterNpc}: {items} " +
                             $"({c.CompletionPercentage:P0}) Days left: {c.DaysRemaining}", LogLevel.Info);
            }
        }

        private void OnAcceptContract(string command, string[] args)
        {
            if (args.Length < 1)
            {
                _monitor.Log("Usage: sep_accept <contractId>", LogLevel.Warn);
                return;
            }

            bool result = ContractManager.AcceptContract(args[0]);
            _monitor.Log(result ? $"Contract {args[0]} accepted!" : $"Could not accept contract {args[0]}.", LogLevel.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  BARGAINING
        // ══════════════════════════════════════════════════════════════

        private void OnOffers(string command, string[] args)
        {
            _monitor.Log("=== Bargaining Offers ===", LogLevel.Info);
            var pending = BargainManager.GetPendingOffers();
            var accepted = BargainManager.GetAcceptedOffers();

            _monitor.Log("  Pending:", LogLevel.Info);
            if (pending.Count == 0) _monitor.Log("    (none)", LogLevel.Info);
            foreach (var o in pending)
            {
                _monitor.Log($"    [{o.OfferId}] {o.NpcName} wants {o.Quantity}x {o.ItemDisplayName} for {o.OfferPrice}g (round {o.RoundNumber}/{o.MaxRounds})", LogLevel.Info);
            }

            _monitor.Log("  Accepted (awaiting delivery):", LogLevel.Info);
            if (accepted.Count == 0) _monitor.Log("    (none)", LogLevel.Info);
            foreach (var o in accepted)
            {
                _monitor.Log($"    [{o.OfferId}] {o.NpcName}: {o.Quantity}x {o.ItemDisplayName} → {o.OfferPrice}g (deliver in {o.DeliveryDaysRemaining}d)", LogLevel.Info);
            }
        }

        private void OnOfferAccept(string command, string[] args)
        {
            if (args.Length < 1) { _monitor.Log("Usage: sep_offer_accept <offerId>", LogLevel.Warn); return; }
            bool ok = BargainManager.AcceptOffer(args[0]);
            _monitor.Log(ok ? $"Offer {args[0]} accepted!" : $"Could not accept offer {args[0]}.", LogLevel.Info);
        }

        private void OnOfferReject(string command, string[] args)
        {
            if (args.Length < 1) { _monitor.Log("Usage: sep_offer_reject <offerId>", LogLevel.Warn); return; }
            BargainManager.RejectOffer(args[0]);
            _monitor.Log($"Offer {args[0]} rejected.", LogLevel.Info);
        }

        private void OnOfferCounter(string command, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int price))
            {
                _monitor.Log("Usage: sep_offer_counter <offerId> <price>", LogLevel.Warn);
                return;
            }
            int result = BargainManager.CounterOffer(args[0], price);
            string outcome = result switch
            {
                1 => "NPC ACCEPTED your counter-offer!",
                0 => "NPC WALKED AWAY.",
                -1 => "NPC made a counter-offer. Check sep_offers for new price.",
                _ => "Unknown result."
            };
            _monitor.Log(outcome, LogLevel.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  TAX
        // ══════════════════════════════════════════════════════════════

        private void OnTax(string command, string[] args)
        {
            _monitor.Log("=== Tax Tracking (Current Season) ===", LogLevel.Info);
            var data = TaxManager.GetCurrentTracking();
            _monitor.Log($"  Gross Income:        {data.SeasonGrossIncome}g", LogLevel.Info);
            _monitor.Log($"  Sprinkler Tiles:     {data.SprinklerTilesWatered}", LogLevel.Info);
            _monitor.Log($"  Machine Activations: {data.MachineActivations}", LogLevel.Info);

            // Estimate current tax
            var estimate = TaxManager.EstimateCurrentTax();
            _monitor.Log($"  --- Estimated Bill ---", LogLevel.Info);
            _monitor.Log($"  Income Tax:          {estimate.IncomeTax}g", LogLevel.Info);
            _monitor.Log($"  Utility Tax:         {estimate.UtilityTax}g", LogLevel.Info);
            _monitor.Log($"  Property Tax:        {estimate.PropertyTax}g", LogLevel.Info);
            _monitor.Log($"  TOTAL:               {estimate.IncomeTax + estimate.UtilityTax + estimate.PropertyTax}g", LogLevel.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  BANK
        // ══════════════════════════════════════════════════════════════

        private void OnBank(string command, string[] args)
        {
            _monitor.Log("=== Bank Account ===", LogLevel.Info);
            _monitor.Log($"  Savings Balance:     {BankManager.GetSavingsBalance()}g", LogLevel.Info);
            _monitor.Log($"  Fixed-Term:          {(BankManager.IsFixedTerm() ? $"Active ({BankManager.GetFixedTermDaysRemaining()} days remaining)" : "None")}", LogLevel.Info);
            _monitor.Log($"  Loan Outstanding:    {BankManager.GetLoanBalance()}g", LogLevel.Info);
            _monitor.Log($"  Seasonal Payment:    {BankManager.GetSeasonalPayment()}g", LogLevel.Info);
            _monitor.Log($"  Total Interest Earned: {BankManager.GetTotalInterestEarned()}g", LogLevel.Info);
            _monitor.Log($"  Daily Interest Rate: {ModConfig.GetInstance().BaseInterestRate:P3}", LogLevel.Info);
        }

        private void OnDeposit(string command, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int amount) || amount <= 0)
            {
                _monitor.Log("Usage: sep_deposit <amount>", LogLevel.Warn);
                return;
            }
            bool ok = BankManager.Deposit(amount);
            _monitor.Log(ok ? $"Deposited {amount}g." : "Deposit failed (insufficient funds or fixed-term active).", LogLevel.Info);
        }

        private void OnWithdraw(string command, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int amount) || amount <= 0)
            {
                _monitor.Log("Usage: sep_withdraw <amount>", LogLevel.Warn);
                return;
            }
            bool ok = BankManager.Withdraw(amount);
            _monitor.Log(ok ? $"Withdrew {amount}g." : "Withdraw failed (insufficient savings or fixed-term locked).", LogLevel.Info);
        }

        private void OnLoan(string command, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int amount) || amount <= 0)
            {
                _monitor.Log("Usage: sep_loan <amount>", LogLevel.Warn);
                return;
            }
            bool ok = BankManager.TakeLoan(amount);
            _monitor.Log(ok ? $"Loan of {amount}g approved!" : $"Loan denied (max: {ModConfig.GetInstance().MaxLoanAmount}g, existing loan, or too small).", LogLevel.Info);
        }

        private void OnRepay(string command, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int amount) || amount <= 0)
            {
                _monitor.Log("Usage: sep_repay <amount>", LogLevel.Warn);
                return;
            }
            bool ok = BankManager.RepayLoan(amount);
            _monitor.Log(ok ? $"Repaid {amount}g on loan." : "Repayment failed.", LogLevel.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  REPUTATION
        // ══════════════════════════════════════════════════════════════

        private void OnReputation(string command, string[] args)
        {
            var player = Game1.player;
            int level = ReputationSkill.GetLevel(player);
            string tier = level >= 10 ? "International (25x capacity)" : level >= 5 ? "National (5x capacity)" : "Regional (1x capacity)";
            _monitor.Log($"=== Reputation Skill ===", LogLevel.Info);
            _monitor.Log($"  Level:    {level}", LogLevel.Info);
            _monitor.Log($"  Tier:     {tier}", LogLevel.Info);
        }

        private void OnAddXp(string command, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int xp))
            {
                _monitor.Log("Usage: sep_addxp <amount>", LogLevel.Warn);
                return;
            }
            ReputationSkill.AddReputationXP(Game1.player, xp);
            _monitor.Log($"Added {xp} reputation XP.", LogLevel.Info);
        }
    }
}
