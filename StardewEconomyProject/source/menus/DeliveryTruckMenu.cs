using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Opens the Delivery Truck cargo hold as an ItemGrabMenu.
    ///
    /// Items placed here are processed at the start of the next day:
    ///   • Accepted trade contracts are fulfilled first (urgency order).
    ///   • Remaining items are shipped to the market (bottle fill + income).
    ///
    /// The menu is a thin wrapper around ItemGrabMenu backed by
    /// DeliveryTruckManager.TruckContents.
    /// </summary>
    public static class DeliveryTruckMenu
    {
        /// <summary>
        /// Build and return an ItemGrabMenu backed by the truck's live item list.
        /// </summary>
        public static IClickableMenu Open()
        {
            var contents = economy.DeliveryTruckManager.TruckContents;

            // Pad the list to TruckSlots so the menu shows a full grid
            while (contents.Count < economy.DeliveryTruckManager.TruckSlots)
                contents.Add(null);

            var menu = new ItemGrabMenu(
                inventory: contents,
                reverseGrab: false,
                showReceivingMenu: true,
                highlightFunction: HighlightShippable,
                behaviorOnItemSelectFunction: null,
                message: "Delivery Truck — items are shipped at start of next day",
                behaviorOnItemGrab: null,
                snapToBottom: false,
                canBeExitedWithKey: true,
                playRightClickSound: true,
                allowRightClick: true,
                showOrganizeButton: true,
                source: ItemGrabMenu.source_none,
                sourceItem: null
            );

            return menu;
        }

        /// <summary>Highlight shippable items (non-tools, non-weapons).</summary>
        private static bool HighlightShippable(Item item)
        {
            if (item == null) return false;
            return item is StardewValley.Object;
        }
    }
}
