using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using System.Globalization;
using Object = StardewValley.Object;

namespace StardewEconomyProject
{
    public partial class ModEntry
    {
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