using System;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace StardewEconomyProject
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private source.Manager instance;
        //private source.commands.Commands commandManager;
        public static IMonitor ModMonitor;
        public static IModHelper ModHelper;

        public override void Entry(IModHelper helper)
        {

            try
            {
                ModConfig.GetInstance().SetConfig(this.Helper.ReadConfig<ModConfig>());
            }
            catch (Exception ex)
            {
                Monitor.Log($"Encountered an error while loading the config.json file. Default settings will be used instead. Full error message:\n-----\n{ex.ToString()}", LogLevel.Error);
            }

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            //Initialize Global Log
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            Helper.Events.GameLoop.DayStarted += this.OnDayStarting;

            instance = new source.Manager();

            this.Monitor.Log("Initiating Harmony patches", LogLevel.Debug);
            source.patch.HarmonyPatches.InitPatches(this.ModManifest.UniqueID, this.Monitor);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            // print button presses to the console window
            this.Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
        }

        private void OnDayStarting(object sender, DayStartedEventArgs e)
        {
            // print a message to the player when the day starts
            this.Monitor.Log("Good morning!", LogLevel.Info);

            // update the spoilage of items in the player's inventory and container's

        }
    }
}