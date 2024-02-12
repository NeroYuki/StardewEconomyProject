using StardewEconomyProject.source.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconomyProject.source
{
    public class Manager
    {
        private model.MarketPrice marketPrice;
        private model.ItemSpoilTracker spoilTracker;

        public Manager()
        {
            marketPrice = null;
        }

        public void Init()
        {
            // load the market price from the file
            marketPrice = new model.MarketPrice();
            spoilTracker = new model.ItemSpoilTracker();
            
            LogHelper.Debug("Market Price loaded");
        }

        public void OnDayStarting()
        {

            // update the spoilage of items in the player's inventory and container's


        }
    }
}
