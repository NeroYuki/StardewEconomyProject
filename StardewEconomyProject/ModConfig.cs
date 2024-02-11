using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewEconomyProject
{
    public class ModConfig
    {
        private static ModConfig _instance;

        // This is the static method that controls the access to the singleton
        // instance. On the first run, it creates a singleton object and places
        // it into the static field. On subsequent runs, it returns the client
        // existing object stored in the static field.
        public static ModConfig GetInstance()
        {
            if (_instance == null)
            {
                _instance = new ModConfig();
            }
            return _instance;
        }

        public void SetConfig(ModConfig input, bool CopyUIConfig = false)
        {
            if (_instance == null)
            {
                _instance = new ModConfig();
            }
            if (input != null)
            {
                _instance = input;
            }
        }

        // config general mod properties

        public bool enableItemSpoil = true;
        public bool enableSupplyDemandSimulation = true;
        public bool enableProductOrder = false;

        // config mod UI properties

        public bool enableSpoilTooltip = true;
        public bool enableCustomSpoiledItemSprite = true;

        // config item aging properties

        public int defaultAge = -1;
        public int defaultAgeCooking = 2;
        public int defaultAgeGreens = 7;
        public int defaultAgeFruit = 5;

        // supply demand simulation configurable parameters


        // product order configurable parameters


    }
}
