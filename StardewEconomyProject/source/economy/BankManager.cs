using System;
using System.Linq;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Locations;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// The Pelican Town Banking System provides tools for managing seasonal cash flow.
    /// Features:
    /// 1. Savings accounts with variable interest
    /// 2. Fixed-term deposits with higher interest
    /// 3. Loans with amortization
    /// 4. Debt spiral / collateral logic
    /// </summary>
    public class BankManager
    {
        private static BankData _data = new();

        // ══════════════════════════════════════════════════════════════
        //  DAILY UPDATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>Called at DayStarted to apply daily interest and check loan payments.</summary>
        public static void OnDayStarted()
        {
            if (!ModConfig.GetInstance().EnableBanking) return;

            var config = ModConfig.GetInstance();

            // Apply daily interest to savings
            if (_data.SavingsBalance > 0)
            {
                double dailyRate = config.BaseInterestRate;

                // Fixed-term bonus
                if (_data.IsFixedTerm && _data.FixedTermDaysRemaining > 0)
                {
                    dailyRate *= _data.FixedTermRateMultiplier;
                    _data.FixedTermDaysRemaining--;

                    if (_data.FixedTermDaysRemaining <= 0)
                    {
                        _data.IsFixedTerm = false;
                        LogHelper.Info("[Bank] Fixed-term deposit matured!");
                        Game1.addHUDMessage(new HUDMessage(
                            $"Your fixed-term deposit of {_data.SavingsBalance:N0}g has matured!",
                            HUDMessage.achievement_type));
                    }
                }

                int interest = (int)(_data.SavingsBalance * dailyRate);
                if (interest > 0)
                {
                    _data.SavingsBalance += interest;
                    _data.TotalInterestEarned += interest;
                    LogHelper.Trace($"[Bank] Interest: +{interest}g (savings: {_data.SavingsBalance:N0}g)");
                }
            }

            // Check for seasonal loan payment (Day 1 of each season)
            if (_data.LoanBalance > 0 && Game1.dayOfMonth == 1)
            {
                ProcessLoanPayment();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SAVINGS OPERATIONS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Deposit money into savings.</summary>
        public static bool Deposit(int amount)
        {
            if (!ModConfig.GetInstance().EnableBanking) return false;
            if (amount <= 0 || Game1.player.Money < amount) return false;

            // Cannot deposit if in a fixed term
            if (_data.IsFixedTerm && _data.FixedTermDaysRemaining > 0)
            {
                Game1.addHUDMessage(new HUDMessage(
                    "Cannot modify savings during fixed-term deposit period.",
                    HUDMessage.error_type));
                return false;
            }

            Game1.player.Money -= amount;
            _data.SavingsBalance += amount;

            LogHelper.Info($"[Bank] Deposited {amount}g. Balance: {_data.SavingsBalance:N0}g");
            Game1.addHUDMessage(new HUDMessage(
                $"Deposited {amount:N0}g. Savings: {_data.SavingsBalance:N0}g",
                HUDMessage.achievement_type));
            return true;
        }

        /// <summary>Withdraw money from savings.</summary>
        public static bool Withdraw(int amount)
        {
            if (!ModConfig.GetInstance().EnableBanking) return false;
            if (amount <= 0 || _data.SavingsBalance < amount) return false;

            // Cannot withdraw if in a fixed term
            if (_data.IsFixedTerm && _data.FixedTermDaysRemaining > 0)
            {
                Game1.addHUDMessage(new HUDMessage(
                    $"Cannot withdraw during fixed-term period ({_data.FixedTermDaysRemaining} days remaining).",
                    HUDMessage.error_type));
                return false;
            }

            _data.SavingsBalance -= amount;
            Game1.player.Money += amount;

            LogHelper.Info($"[Bank] Withdrew {amount}g. Balance: {_data.SavingsBalance:N0}g");
            Game1.addHUDMessage(new HUDMessage(
                $"Withdrew {amount:N0}g. Savings: {_data.SavingsBalance:N0}g",
                HUDMessage.achievement_type));
            return true;
        }

        /// <summary>Lock savings into a fixed-term deposit. <paramref name="seasons"/> must be 1, 2, 4, 8, or 12; other values snap to nearest.</summary>
        public static bool StartFixedTerm(int seasons = 1)
        {
            if (!ModConfig.GetInstance().EnableBanking) return false;
            if (_data.SavingsBalance <= 0 || _data.IsFixedTerm) return false;

            seasons = SnapToValidSeasons(seasons);
            int days = seasons * 28;
            double rateMultiplier = GetFixedTermRateForSeasons(seasons);

            _data.IsFixedTerm = true;
            _data.FixedTermDaysRemaining = days;
            _data.FixedTermRateMultiplier = rateMultiplier;

            double effectiveRate = ModConfig.GetInstance().BaseInterestRate * rateMultiplier;
            LogHelper.Info($"[Bank] Fixed-term started: {_data.SavingsBalance:N0}g for {seasons} season(s) ({days} days) at {effectiveRate:P3}/day.");
            Game1.addHUDMessage(new HUDMessage(
                $"Fixed-term activated! {_data.SavingsBalance:N0}g locked for {seasons} season(s) ({days} days) at {effectiveRate:P3}/day.",
                HUDMessage.achievement_type));
            return true;
        }

        /// <summary>Returns the nearest valid fixed-term season count (1, 2, 4, 8, 12).</summary>
        public static int SnapToValidSeasons(int input)
        {
            int[] valid = { 1, 2, 4, 8, 12 };
            int best = valid[0];
            int bestDiff = int.MaxValue;
            foreach (int v in valid)
            {
                int diff = Math.Abs(input - v);
                if (diff < bestDiff) { bestDiff = diff; best = v; }
            }
            return best;
        }

        /// <summary>Returns the daily rate multiplier for the given season count from config.</summary>
        public static double GetFixedTermRateForSeasons(int seasons)
        {
            var cfg = ModConfig.GetInstance();
            return seasons switch
            {
                1  => cfg.FixedTermRate1Season,
                2  => cfg.FixedTermRate2Seasons,
                4  => cfg.FixedTermRate4Seasons,
                8  => cfg.FixedTermRate8Seasons,
                12 => cfg.FixedTermRate12Seasons,
                _  => cfg.FixedTermRate2Seasons,
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  LOAN OPERATIONS
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  LOAN LIMIT — VAULT BUNDLE GATING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the player's current max loan amount, scaled by vault bundle completion.
        /// Tiers: none=1k, 2500g bundle=10k, 5000g=20k, 10000g=40k, 25000g=100k, all/joja=250k
        /// (all proportionally scaled from <see cref="ModConfig.LoanLimitMaxTier"/>).
        /// </summary>
        public static int GetLoanLimit()
        {
            double cap = ModConfig.GetInstance().LoanLimitMaxTier;
            const double defaultMax = 250_000.0;
            int Scaled(double defaultLimit) => Math.Max(1, (int)(cap * defaultLimit / defaultMax));

            // Top tier: all vault bundles complete OR Joja route finished
            if (HasFinishedJojaRoute() || AreAllVaultBundlesComplete())
                return (int)cap;

            if (IsBundleComplete(23)) return Scaled(100_000); // 25 000g bundle
            if (IsBundleComplete(15)) return Scaled(40_000);  // 10 000g bundle
            if (IsBundleComplete(14)) return Scaled(20_000);  // 5 000g bundle
            if (IsBundleComplete(13)) return Scaled(10_000);  // 2 500g bundle
            return Scaled(1_000);                              // no vault bundle done
        }

        /// <summary>Returns true when the Joja community development route is complete.</summary>
        private static bool HasFinishedJojaRoute()
        {
            // Each Joja development project sends a specific mail on purchase.
            var mail = Game1.MasterPlayer.mailReceived;
            return mail.Contains("jojaMember") &&
                   mail.Contains("jojaBoiler") && mail.Contains("jojaFridge") &&
                   mail.Contains("jojaBus")    && mail.Contains("jojaGreenhouse") &&
                   mail.Contains("jojaVault");
        }

        /// <summary>Returns true when all four vault bundles (IDs 13, 14, 15, 23) are complete.</summary>
        private static bool AreAllVaultBundlesComplete()
            => IsBundleComplete(13) && IsBundleComplete(14)
            && IsBundleComplete(15) && IsBundleComplete(23);

        /// <summary>Returns true when every slot in a community center bundle is filled.</summary>
        private static bool IsBundleComplete(int bundleId)
        {
            try
            {
                var cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
                if (cc == null) return false;
                if (!cc.bundles.ContainsKey(bundleId)) return false;
                bool[] slots = cc.bundles[bundleId];
                return slots.Length > 0 && slots.All(b => b);
            }
            catch { return false; }
        }

        /// <summary>Take out a loan.</summary>
        public static bool TakeLoan(int amount, int seasons = 4)
        {
            if (!ModConfig.GetInstance().EnableBanking) return false;

            int limit = GetLoanLimit();
            if (amount <= 0 || amount > limit) return false;
            if (_data.LoanBalance > 0)
            {
                Game1.addHUDMessage(new HUDMessage(
                    "You already have an outstanding loan!",
                    HUDMessage.error_type));
                return false;
            }

            seasons = SnapToValidSeasons(seasons);

            _data.LoanPrincipal = amount;
            _data.LoanBalance = amount;
            _data.LoanSeasonsRemaining = seasons;
            _data.SeasonalPayment = CalculateSeasonalPayment(amount, ModConfig.GetInstance().LoanInterestRate, seasons);

            Game1.player.Money += amount;

            LogHelper.Info($"[Bank] Loan taken: {amount}g. Seasonal payment: {_data.SeasonalPayment}g over {seasons} season(s).");
            Game1.addHUDMessage(new HUDMessage(
                $"Loan approved: {amount:N0}g! Payment: {_data.SeasonalPayment:N0}g/season for {seasons} season(s).",
                HUDMessage.achievement_type));

            // Send loan approval mail
            MailManager.SendLoanApproved();

            return true;
        }

        /// <summary>Process the seasonal loan payment (called on Day 1).</summary>
        private static void ProcessLoanPayment()
        {
            if (_data.LoanBalance <= 0) return;

            int payment = Math.Min(_data.SeasonalPayment, _data.LoanBalance);

            if (Game1.player.Money >= payment)
            {
                Game1.player.Money -= payment;
                _data.LoanBalance -= payment;
                _data.LoanSeasonsRemaining--;

                LogHelper.Info($"[Bank] Loan payment: {payment}g. Remaining: {_data.LoanBalance:N0}g ({_data.LoanSeasonsRemaining} seasons)");
                Game1.addHUDMessage(new HUDMessage(
                    $"Loan payment: {payment:N0}g deducted. Remaining: {_data.LoanBalance:N0}g",
                    HUDMessage.error_type));

                if (_data.LoanBalance <= 0)
                {
                    _data.LoanBalance = 0;
                    _data.LoanSeasonsRemaining = 0;
                    LogHelper.Info("[Bank] Loan fully repaid!");
                    Game1.addHUDMessage(new HUDMessage(
                        "Loan fully repaid! Congratulations!",
                        HUDMessage.achievement_type));
                }
            }
            else
            {
                // Debt spiral — player can't afford payment
                LogHelper.Warn($"[Bank] DEBT SPIRAL: Cannot afford loan payment of {payment}g!");
                Game1.addHUDMessage(new HUDMessage(
                    $"WARNING: Cannot afford loan payment of {payment:N0}g! Assets may be repossessed.",
                    HUDMessage.error_type));

                // Apply interest penalty for missed payment
                _data.LoanBalance += (int)(_data.LoanBalance * ModConfig.GetInstance().LoanInterestRate * 0.5);

                // Take whatever money the player has
                int partialPayment = Game1.player.Money;
                Game1.player.Money = 0;
                _data.LoanBalance -= partialPayment;

                // Send debt warning mail
                MailManager.SendDebtWarning();
            }
        }

        /// <summary>Calculate seasonal payment using simple amortization.</summary>
        private static int CalculateSeasonalPayment(int principal, double interestRate, int seasons)
        {
            // Standard amortization: P * [r(1+r)^n] / [(1+r)^n - 1]
            double r = interestRate;
            double n = seasons;
            if (r <= 0) return principal / seasons;

            double numerator = r * Math.Pow(1 + r, n);
            double denominator = Math.Pow(1 + r, n) - 1;
            return (int)(principal * numerator / denominator);
        }

        /// <summary>Manually repay part or all of a loan early.</summary>
        public static bool RepayLoan(int amount)
        {
            if (!ModConfig.GetInstance().EnableBanking) return false;
            if (amount <= 0 || _data.LoanBalance <= 0) return false;
            if (Game1.player.Money < amount) return false;

            int actualPayment = Math.Min(amount, _data.LoanBalance);
            Game1.player.Money -= actualPayment;
            _data.LoanBalance -= actualPayment;

            if (_data.LoanBalance <= 0)
            {
                _data.LoanBalance = 0;
                _data.LoanSeasonsRemaining = 0;
                Game1.addHUDMessage(new HUDMessage("Loan fully repaid!", HUDMessage.achievement_type));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(
                    $"Paid {actualPayment:N0}g toward loan. Remaining: {_data.LoanBalance:N0}g",
                    HUDMessage.achievement_type));
            }

            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Returns a short human-readable description of the current loan tier.</summary>
        public static string GetLoanLimitTierName()
        {
            if (HasFinishedJojaRoute() || AreAllVaultBundlesComplete()) return "vault complete / Joja";
            if (IsBundleComplete(23)) return "25,000g bundle done";
            if (IsBundleComplete(15)) return "10,000g bundle done";
            if (IsBundleComplete(14)) return "5,000g bundle done";
            if (IsBundleComplete(13)) return "2,500g bundle done";
            return "no vault bundles";
        }

        public static int GetSavingsBalance() => _data.SavingsBalance;
        public static int GetLoanBalance() => _data.LoanBalance;
        public static int GetSeasonalPayment() => _data.SeasonalPayment;
        public static bool IsFixedTerm() => _data.IsFixedTerm;
        public static int GetFixedTermDaysRemaining() => _data.FixedTermDaysRemaining;
        public static double GetFixedTermRateMultiplier() => _data.FixedTermRateMultiplier;
        public static int GetTotalInterestEarned() => _data.TotalInterestEarned;
        public static bool HasOutstandingLoan() => _data.LoanBalance > 0;

        // ══════════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ══════════════════════════════════════════════════════════════

        public static string Serialize()
        {
            return JsonConvert.SerializeObject(_data, Formatting.Indented);
        }

        public static void Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                _data = new BankData();
                return;
            }

            try
            {
                _data = JsonConvert.DeserializeObject<BankData>(json) ?? new BankData();
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Bank] Failed to deserialize:\n{ex}");
                _data = new BankData();
            }
        }

        public static void Reset()
        {
            _data = new BankData();
        }

        // ══════════════════════════════════════════════════════════════
        //  DATA MODEL
        // ══════════════════════════════════════════════════════════════

        public class BankData
        {
            public int SavingsBalance { get; set; } = 0;
            public bool IsFixedTerm { get; set; } = false;
            public int FixedTermDaysRemaining { get; set; } = 0;
            public double FixedTermRateMultiplier { get; set; } = 2.0;
            public int TotalInterestEarned { get; set; } = 0;

            public int LoanPrincipal { get; set; } = 0;
            public int LoanBalance { get; set; } = 0;
            public int SeasonalPayment { get; set; } = 0;
            public int LoanSeasonsRemaining { get; set; } = 0;
        }
    }
}
