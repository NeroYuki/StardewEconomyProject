using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconomyProject.source.model
{
    public class MarketPriceEntry
    {
        public int price;
        // add extra paramters here
    }
    public class MarketPrice
    {
        public static Dictionary<string, MarketPriceEntry> PricingList = new Dictionary<string, MarketPriceEntry>();

        public static void UpdatePriceOfAllItem()
        {
            // iterate through the list and update the price
            foreach (KeyValuePair<string, MarketPriceEntry> entry in PricingList)
            {
                // modify the price here
                entry.Value.price = 100;
            }
        }

        public static int GetPrice(string itemName)
        {
            if (PricingList.ContainsKey(itemName))
                return PricingList[itemName].price;

            return 0;
        }
    }
}
