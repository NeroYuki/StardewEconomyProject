using StardewModdingAPI;
using StardewValley;
using StardewEconomyProject.source.menus;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches for custom Big Craftable interactions.
    /// Handles right-click on placed Contract Board, Market Terminal, and ATM.
    ///
    /// Patch target:
    /// ─────────────────────────────────────────────────────────────────────
    /// Object.checkForAction(Farmer, bool)
    ///   → Postfix: open economy menus for SEP big craftables
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class BigCraftablePatches
    {
        private static IMonitor _monitor;

        // Our custom Big Craftable item IDs (must match Data/BigCraftables keys in the CP pack)
        public const string ContractBoardId   = "sep.ContractBoard";
        public const string MarketTerminalId  = "sep.MarketTerminal";
        public const string ATMachineId       = "sep.ATMachine";
        public const string SupercomputerId   = "sep.Supercomputer";

        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Postfix for Object.checkForAction — intercepts interaction with
        /// our custom big craftables and opens the corresponding menu.
        /// </summary>
        public static void CheckForAction_Postfix(
            SObject __instance,
            Farmer who,
            bool justCheckingForActivity,
            ref bool __result)
        {
            // If just probing, report that we ARE actionable for our items
            if (justCheckingForActivity)
            {
                if (__instance.bigCraftable.Value && IsEconomyCraftable(__instance.ItemId))
                    __result = true;
                return;
            }

            // Only handle our big craftables
            if (!__instance.bigCraftable.Value) return;

            switch (__instance.ItemId)
            {
                case ContractBoardId:
                    Game1.activeClickableMenu = new ContractBoardMenu();
                    __result = true;
                    _monitor?.Log("[SEP] Opened Contract Board menu via big craftable.", LogLevel.Trace);
                    break;

                case MarketTerminalId:
                    Game1.activeClickableMenu = new ForecastMenu();
                    __result = true;
                    _monitor?.Log("[SEP] Opened Forecast menu via big craftable.", LogLevel.Trace);
                    break;

                case ATMachineId:
                    Game1.activeClickableMenu = new BankMenu();
                    __result = true;
                    _monitor?.Log("[SEP] Opened Bank menu via big craftable.", LogLevel.Trace);
                    break;

                case SupercomputerId:
                    Game1.activeClickableMenu = new ForecastMenu();
                    __result = true;
                    _monitor?.Log("[SEP] Opened Forecast menu via Supercomputer.", LogLevel.Trace);
                    break;
            }
        }

        private static bool IsEconomyCraftable(string itemId)
        {
            return itemId == ContractBoardId
                || itemId == MarketTerminalId
                || itemId == ATMachineId
                || itemId == SupercomputerId;
        }
    }
}
