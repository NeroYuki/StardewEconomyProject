using StardewEconomyProject.source.data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconomyProject.source.model
{
    public class ItemSpoilEntry
    {
        public string ItemName;
        public int remainingAge;
    }

    public class ItemSpoilTracker
    {
        public static Dictionary<string, ItemSpoilEntry> spoilTracker = new Dictionary<string, ItemSpoilEntry>();

        public static void UpdateSpoilage()
        {
            // iterate through the list and update the spoilage
            foreach (KeyValuePair<string, ItemSpoilEntry> entry in spoilTracker)
            {
                // modify the spoilage here
                entry.Value.remainingAge = 2;
            }
        }

        public void AddItemSpoilEntry(StardewValley.Item item)
        {
            // generate unique key
            string key = item.Name + item.GetHashCode();

            // TODO: check if the item is already in the list
            if (!spoilTracker.ContainsKey(key))
            {
                // add the item to the list
                ItemSpoilEntry entry = new ItemSpoilEntry();
                entry.ItemName = item.Name;
                // TODO: set the remaining age based on the item category or custom aging data
                entry.remainingAge = CustomItemAge.getAge(item as StardewValley.Object);
                spoilTracker.Add(key, entry);
            }
        }
    }
}
