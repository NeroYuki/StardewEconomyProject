using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using xTile.Dimensions;
using StardewEconomyProject.source.menus;

namespace StardewEconomyProject.source.harmony_patches
{
    /// <summary>
    /// Harmony patches for custom building interactions.
    ///
    /// When a player clicks on a building tile that has a custom Action property,
    /// the game calls GameLocation.performAction(string[], Farmer, Location).
    /// We intercept custom SEP action tokens here and open the appropriate menu.
    ///
    /// Action tokens handled:
    ///   sep.OpenDeliveryTruck  → opens the Delivery Truck cargo hold (ItemGrabMenu)
    /// </summary>
    public class BuildingPatches
    {
        private static IMonitor _monitor;

        // Building ID used in Data/Buildings (must match content.json key)
        public const string DeliveryTruckBuildingId = "neroyuki.stardeweconomyitems_DeliveryTruck";

        // Action token fired by the building's DefaultAction field
        public const string ActionOpenDeliveryTruck = "sep.OpenDeliveryTruck";

        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Apply patches for building tile actions.
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            try
            {
                harmony.Patch(
                    original: AccessTools.Method(
                        typeof(GameLocation),
                        nameof(GameLocation.performAction),
                        new[] { typeof(string[]), typeof(Farmer), typeof(Location) }),
                    prefix: new HarmonyMethod(
                        typeof(BuildingPatches),
                        nameof(PerformAction_Prefix))
                );
                _monitor?.Log("[SEP] Building action patch applied (sep.OpenDeliveryTruck).", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[SEP] Could not patch GameLocation.performAction: {ex.Message}", LogLevel.Warn);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  PREFIX: GameLocation.performAction(string[], Farmer, Location)
        //  Intercepts custom SEP building action tokens before the game
        //  processes them (which would do nothing since they're unknown).
        // ════════════════════════════════════════════════════════════════
        public static bool PerformAction_Prefix(
            string[] action,
            Farmer who,
            Location tileLocation,
            ref bool __result)
        {
            try
            {
                if (action == null || action.Length == 0) return true; // run original

                string token = action[0];

                if (token == ActionOpenDeliveryTruck)
                {
                    if (who.IsLocalPlayer)
                    {
                        Game1.activeClickableMenu = DeliveryTruckMenu.Open();
                        _monitor?.Log("[SEP] Opened Delivery Truck cargo hold via building action.", LogLevel.Trace);
                    }
                    __result = true;
                    return false; // skip original
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[SEP] BuildingPatches.PerformAction_Prefix error:\n{ex}", LogLevel.Error);
            }

            return true; // run original for all other actions
        }
    }
}
