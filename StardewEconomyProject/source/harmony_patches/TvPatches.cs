using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewEconomyProject.source.economy;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Patches the TV to add a "Market &amp; Trade Report" channel.
    /// 
    /// Harmony approach:
    /// 1. Postfix on TV.checkForAction — injects our channel into the dialogue response list.
    /// 2. Intercepts the afterQuestion delegate to handle our channel selection.
    /// </summary>
    public static class TvPatches
    {
        private static IMonitor Monitor;
        private const string ChannelName = "Market & Trade Report";
        private const string ChannelKey = "sep_market";

        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        /// <summary>
        /// Apply TV Harmony patches to inject the Market Report channel.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                var checkForAction = AccessTools.Method(typeof(TV), nameof(TV.checkForAction),
                    new[] { typeof(Farmer), typeof(bool) });

                if (checkForAction != null)
                {
                    harmony.Patch(
                        original: checkForAction,
                        postfix: new HarmonyMethod(typeof(TvPatches), nameof(TV_CheckForAction_Postfix))
                    );
                    Monitor?.Log("TV Market Report channel patched successfully.", LogLevel.Info);
                }
                else
                {
                    Monitor?.Log("Could not find TV.checkForAction — TV channel will not be available.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to apply TV patches: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// After the TV creates its channel selection dialogue, inject the Market Report channel
        /// and intercept the afterQuestion delegate to handle selection.
        /// </summary>
        private static void TV_CheckForAction_Postfix(TV __instance, Farmer who, bool justCheckingForActivity)
        {
            if (justCheckingForActivity) return;

            try
            {
                var location = Game1.currentLocation;
                if (location == null) return;

                // Save the original afterQuestion delegate set by TV.checkForAction
                var originalHandler = location.afterQuestion;

                // Replace with our wrapper that intercepts our channel key
                location.afterQuestion = (respondent, answer) =>
                {
                    if (answer == ChannelKey)
                    {
                        ShowMarketReportOnTV();
                    }
                    else
                    {
                        originalHandler?.Invoke(respondent, answer);
                    }
                };

                // Add our channel response to the active dialogue box
                if (Game1.activeClickableMenu is DialogueBox box && box.responses != null)
                {
                    // Guard against duplicate injection
                    if (box.responses.Any(r => r.responseKey == ChannelKey))
                        return;

                    // Convert to list, insert our channel, convert back to array
                    var responseList = new List<Response>(box.responses);

                    // Insert before the "(Leave)" / close option
                    int leaveIdx = responseList.FindIndex(r =>
                        r.responseKey == "(Leave)" || r.responseKey.Contains("Leave"));
                    var marketResponse = new Response(ChannelKey, ChannelName);

                    if (leaveIdx >= 0)
                        responseList.Insert(leaveIdx, marketResponse);
                    else
                        responseList.Add(marketResponse);

                    box.responses = responseList.ToArray();

                    // Rebuild the clickable components so the new response is interactive
                    var setUpQuestions = AccessTools.Method(typeof(DialogueBox), "setUpQuestions")
                                     ?? AccessTools.Method(typeof(DialogueBox), "SetUpQuestions");
                    setUpQuestions?.Invoke(box, null);
                }
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Error injecting TV channel: {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Show the market report as a multi-page TV overlay dialogue.
        /// </summary>
        private static void ShowMarketReportOnTV()
        {
            try
            {
                string report = GenerateMarketReport();
                string[] pages = report.Split(new[] { '^' }, StringSplitOptions.RemoveEmptyEntries);

                if (pages.Length > 0)
                    Game1.multipleDialogues(pages);
                else
                    Game1.drawObjectDialogue("No market data available yet.");
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Error showing market report: {ex.Message}", LogLevel.Warn);
                Game1.drawObjectDialogue("*static* ...technical difficulties...");
            }
        }

        /// <summary>
        /// Generates a market report string for the TV channel display.
        /// Called from a console command or from ModEntry's TV integration.
        /// </summary>
        public static string GenerateMarketReport()
        {
            var bottles = MarketManager.GetAllBottles();
            if (bottles == null || bottles.Count == 0)
                return "Welcome to the Market & Trade Report!^No market data available yet. Start selling to see trends!";

            // Group by category, pick the highest-impact quality tier
            var categories = bottles.Values
                .GroupBy(b => b.CategoryId)
                .OrderBy(g => Array.IndexOf(MarketCategories.All, g.Key))
                .ToList();

            var lines = new List<string>();
            lines.Add("Welcome to the Market & Trade Report!^Here's today's market conditions:");

            foreach (var group in categories)
            {
                // Use the base quality (0) bottle as the representative
                var representative = group.FirstOrDefault(b => b.QualityTier == 0) ?? group.First();
                string emoji = GetMarketEmoji(representative.MarketState);
                string trend = GetTrendText(representative);
                lines.Add($"{representative.CategoryId}: {emoji} {representative.MarketState} (x{representative.DynamicPriceMultiplier:F2}) — {trend}");
            }

            // Add general advice
            lines.Add(GetGeneralAdvice(categories));

            return string.Join("^", lines);
        }

        /// <summary>
        /// Generate a short qualitative hint for an NPC-style dialogue about markets.
        /// </summary>
        public static string GenerateShortHint()
        {
            var bottles = MarketManager.GetAllBottles();
            if (bottles == null || bottles.Count == 0)
                return "The market seems quiet today.";

            // Find the best and worst categories
            var byMultiplier = bottles.Values
                .Where(b => b.QualityTier == 0)
                .OrderByDescending(b => b.DynamicPriceMultiplier)
                .ToList();

            if (byMultiplier.Count == 0)
                return "The market seems quiet today.";

            var best = byMultiplier.First();
            var worst = byMultiplier.Last();

            if (best.DynamicPriceMultiplier > 0.6f)
                return $"I hear {best.CategoryId} goods are selling well right now! Prices are strong.";
            if (worst.DynamicPriceMultiplier < 0.15f)
                return $"The {worst.CategoryId} market is completely flooded. You might want to hold off on selling those.";

            return $"{best.CategoryId} looks promising, while {worst.CategoryId} is oversaturated.";
        }

        private static string GetMarketEmoji(string state)
        {
            return state switch
            {
                "Empty / Surge" => "★",
                "Healthy" => "●",
                "Saturated" => "▼",
                "Flooded" => "▼▼",
                "Crash" => "✖",
                _ => "·"
            };
        }

        private static string GetTrendText(MarketBottle bottle)
        {
            float sat = bottle.Saturation;
            if (sat < 0.10f) return "Prices are surging! Sell now!";
            if (sat < 0.30f) return "Good time to sell.";
            if (sat < 0.55f) return "Average conditions.";
            if (sat < 0.80f) return "Oversupplied — consider waiting.";
            return "Market crashed — avoid selling.";
        }

        private static string GetGeneralAdvice(List<IGrouping<string, MarketBottle>> categories)
        {
            // Find any surge opportunities
            var surges = categories
                .Select(g => g.FirstOrDefault(b => b.QualityTier == 0) ?? g.First())
                .Where(b => b.IsSurgeActive)
                .ToList();

            if (surges.Count > 0)
            {
                string surgeNames = string.Join(", ", surges.Select(s => s.CategoryId));
                return $"^SURGE ALERT: {surgeNames} markets have a surge event today! Prices will be exceptionally high!";
            }

            // General tip based on season
            string season = Game1.currentSeason;
            return season switch
            {
                "spring" => "^Tip: Spring crops are in high demand early season. Plan your planting!",
                "summer" => "^Tip: Summer festivals can boost certain categories. Watch for events!",
                "fall" => "^Tip: Fall brings premium prices for higher-quality goods.",
                "winter" => "^Tip: With fewer crops, Forage and Animal Products tend to hold value.",
                _ => "^Diversify your sales across categories to avoid flooding any single market!"
            };
        }
    }
}
