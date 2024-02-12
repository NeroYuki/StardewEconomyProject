using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewEconomyProject.source.model;
using StardewModdingAPI;
using StardewValley;
using System.Globalization;
using Object = StardewValley.Object;

namespace StardewEconomyProject.source.patch
{
    public class HarmonyPatches
    {

        public static void InitPatches(string modId, IMonitor monitor)
        {
            utils.LogHelper.Monitor = monitor;
            var harmony = new Harmony(modId);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(Object), nameof(Object.drawInMenu))]
        public class Object_drawInMenu_Patch
        {
            public static void Prefix(Object __instance, ref Color color, ref int __state)
            {
                // modify item color
                return;
            }
        }
        [HarmonyPatch(typeof(Object), nameof(Object.sellToStorePrice))]
        public class Object_sellToStorePrice_Patch
        {
            public static bool Prefix(Object __instance, ref int __result)
            {
                // modify selling price
                // if the item is spoiled, return 1
                if (false)
                {
                    __result = 1;
                    return false;
                }

                // if the item is not spoiled, search for the item in the pricing list in MarketPrice model
                // if the item is found, return the price
                // if the item is not found, return the default price

                int cPrice = MarketPrice.GetPrice(__instance.ItemId);
                if (cPrice > 0)
                {
                    __result = cPrice;
                    return false;
                }

                return true;
            }
        }
        [HarmonyPatch(typeof(Object), nameof(Object.DisplayName))]
        [HarmonyPatch(MethodType.Getter)]
        public class Object_DisplayName_Patch
        {
            public static void Postfix(Object __instance, ref string __result)
            {
                // modify item display name
                return;
            }
        }
        [HarmonyPatch(typeof(Object), nameof(Object.getDescription))]
        public class Object_getDescription_Patch
        {
            public static void Postfix(Object __instance, ref string __result)
            {
                // modify item description
                return;
            }
        }
    }
}