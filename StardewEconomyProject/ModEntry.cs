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
        //public static IMonitor ModMonitor;
        //public static IModHelper ModHelper;

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
            source.utils.LogHelper.Monitor = this.Monitor;

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            //Initialize Global Log
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.DayStarted += this.OnDayStarting;
            helper.Events.GameLoop.SaveLoaded += this.OnLoadedSave;
            helper.Events.GameLoop.ReturnedToTitle += this.OnExitToTitle;
            helper.Events.GameLoop.Saved += this.OnGameSaved;

            instance = new source.Manager();

            this.Monitor.Log("Initiating Harmony patches", LogLevel.Debug);
            source.patch.HarmonyPatches.InitPatches(this.ModManifest.UniqueID, this.Monitor);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            //if (!Context.IsWorldReady)
            //    return;

            //// print button presses to the console window
            //this.Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
        }

        private void OnDayStarting(object sender, DayStartedEventArgs e)
        {
            // print a message to the player when the day starts
            this.Monitor.Log("Good morning!", LogLevel.Info);

            instance.OnDayStarting();
        }

        private void OnLoadedSave(object sender, SaveLoadedEventArgs e)
        {
            instance.Init();
        }

        private void OnGameSaved(object sender, SavedEventArgs e)
        {

        }

        private void OnExitToTitle(object sender, ReturnedToTitleEventArgs e)
        {

        }
    }
}