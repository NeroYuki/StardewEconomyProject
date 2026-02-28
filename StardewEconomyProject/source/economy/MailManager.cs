using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Manages mail delivery for the economy system.
    /// Mail entries are defined in the Content Patcher pack (Data/Mail).
    /// This class provides convenience methods for sending them from C#.
    /// </summary>
    public static class MailManager
    {
        // ── Mail IDs (must match keys in the CP pack's Data/Mail entries) ──
        // The CP pack uses {{ModId}} = "neroyuki.stardeweconomyitems" as prefix.
        public const string Mail_Welcome = "neroyuki.stardeweconomyitems_welcome";
        public const string Mail_TaxBill = "neroyuki.stardeweconomyitems_taxBill";
        public const string Mail_ContractComplete = "neroyuki.stardeweconomyitems_contractComplete";
        public const string Mail_ContractBreach = "neroyuki.stardeweconomyitems_contractBreach";
        public const string Mail_LoanApproved = "neroyuki.stardeweconomyitems_loanApproved";
        public const string Mail_LoanPaymentDue = "neroyuki.stardeweconomyitems_loanPaymentDue";
        public const string Mail_DebtWarning = "neroyuki.stardeweconomyitems_debtWarning";
        public const string Mail_ReputationNational = "neroyuki.stardeweconomyitems_reputationNational";
        public const string Mail_ReputationInternational = "neroyuki.stardeweconomyitems_reputationInternational";
        public const string Mail_MarketSurge = "neroyuki.stardeweconomyitems_marketSurge";

        // ══════════════════════════════════════════════════════════════
        //  SEND METHODS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Send the welcome letter on first load with the mod.</summary>
        public static void SendWelcome()
        {
            SendMailOnce(Mail_Welcome);
        }

        /// <summary>Send a tax bill notification (allows re-sending each season).</summary>
        public static void SendTaxBill()
        {
            // Use a season+year specific flag to allow seasonal re-sends
            string flagId = $"{Mail_TaxBill}_{Game1.currentSeason}_{Game1.year}";
            if (!Game1.player.hasOrWillReceiveMail(flagId))
            {
                // Add to today's mailbox (Day 1 = taxes assessed today)
                Game1.player.mailbox.Add(Mail_TaxBill);
                // Flag the season+year combo so we don't re-send this season
                Game1.player.mailReceived.Add(flagId);
                LogHelper.Debug($"[Mail] Tax bill sent for {Game1.currentSeason} Y{Game1.year}.");
            }
        }

        /// <summary>Send a contract completion congratulation.</summary>
        public static void SendContractComplete()
        {
            // Allow multiple contract completions — send as tomorrow's mail
            Game1.player.mailForTomorrow.Add(Mail_ContractComplete);
            LogHelper.Debug("[Mail] Contract completion notice queued.");
        }

        /// <summary>Send a contract breach warning.</summary>
        public static void SendContractBreach()
        {
            Game1.player.mailForTomorrow.Add(Mail_ContractBreach);
            LogHelper.Debug("[Mail] Contract breach notice queued.");
        }

        /// <summary>Send a loan approval notice.</summary>
        public static void SendLoanApproved()
        {
            Game1.player.mailForTomorrow.Add(Mail_LoanApproved);
            LogHelper.Debug("[Mail] Loan approval notice queued.");
        }

        /// <summary>Send a loan payment reminder.</summary>
        public static void SendLoanPaymentDue()
        {
            // Only send once per season
            string flagId = $"{Mail_LoanPaymentDue}_{Game1.currentSeason}_{Game1.year}";
            if (!Game1.player.hasOrWillReceiveMail(flagId))
            {
                Game1.player.mailForTomorrow.Add(Mail_LoanPaymentDue);
                Game1.player.mailReceived.Add(flagId);
                LogHelper.Debug("[Mail] Loan payment due notice queued.");
            }
        }

        /// <summary>Send a debt warning notice.</summary>
        public static void SendDebtWarning()
        {
            string flagId = $"{Mail_DebtWarning}_{Game1.currentSeason}_{Game1.year}";
            if (!Game1.player.hasOrWillReceiveMail(flagId))
            {
                Game1.player.mailForTomorrow.Add(Mail_DebtWarning);
                Game1.player.mailReceived.Add(flagId);
                LogHelper.Debug("[Mail] Debt warning notice queued.");
            }
        }

        /// <summary>Send the National tier milestone letter (one-time).</summary>
        public static void SendReputationNational()
        {
            SendMailOnce(Mail_ReputationNational);
        }

        /// <summary>Send the International tier milestone letter (one-time).</summary>
        public static void SendReputationInternational()
        {
            SendMailOnce(Mail_ReputationInternational);
        }

        /// <summary>Send a market surge alert.</summary>
        public static void SendMarketSurge()
        {
            // Allow periodic surge alerts — use day-specific flag
            string flagId = $"{Mail_MarketSurge}_D{Game1.Date.TotalDays}";
            if (!Game1.player.hasOrWillReceiveMail(flagId))
            {
                Game1.player.mailForTomorrow.Add(Mail_MarketSurge);
                Game1.player.mailReceived.Add(flagId);
                LogHelper.Debug("[Mail] Market surge alert queued.");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Send a mail only once ever (uses the mail ID as the flag).</summary>
        private static void SendMailOnce(string mailId)
        {
            if (!Game1.player.hasOrWillReceiveMail(mailId))
            {
                Game1.player.mailForTomorrow.Add(mailId);
                LogHelper.Debug($"[Mail] One-time mail '{mailId}' queued for tomorrow.");
            }
        }
    }
}
