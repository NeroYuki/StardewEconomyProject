using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewEconomyProject.source.utils;

namespace StardewEconomyProject.source.data
{
    public class CustomItemAgeEntry
    {
        public string name;
        public int age;
    }
    public class CustomItemAge
    {
        public static Dictionary<string, CustomItemAgeEntry> itemAges = new Dictionary<string, CustomItemAgeEntry>();

        public CustomItemAge()
        {
            
        }

        public static void loadList(Mod context)
        {
            String RelativePath = Path.Combine("customAgingData.json");
            CustomItemAgeEntry[] tempArray = context.Helper.Data.ReadJsonFile<CustomItemAgeEntry[]>(RelativePath);
            if (tempArray == null)
            {
                LogHelper.Warn("No aging item definition is found");
                return;
            }

            for (int i = 0; i < tempArray.Length; i++)
                itemAges.TryAdd(tempArray[i].name, tempArray[i]);

            LogHelper.Debug("Aging Item Data loaded");
        }

        public static int getAge(StardewValley.Object gameObj)
        {
            string name = gameObj.Name;
            if (itemAges.ContainsKey(name))
                return itemAges[name].age;

            // placeholder for category specific entries
            if (gameObj.Category == -21)
                return ModConfig.GetInstance().defaultAgeGreens;

            return ModConfig.GetInstance().defaultAge;
        }
    }
}
