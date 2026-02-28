using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewEconomyProject.source.economy;
using StardewEconomyProject.source.menus;
using StardewEconomyProject.source.harmony_patches;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace StardewEconomyProject
{
    /// <summary>The mod entry point — orchestrates all economy subsystems.</summary>
    public class ModEntry : Mod
    {
        private source.commands.Commands _commandManager;

        // Save data keys stored in player modData
        private const string SaveKey_Market     = "neroyuki.stardeweconomy/MarketData";
        private const string SaveKey_Contracts   = "neroyuki.stardeweconomy/ContractData";
        private const string SaveKey_Tax         = "neroyuki.stardeweconomy/TaxData";
        private const string SaveKey_Bank        = "neroyuki.stardeweconomy/BankData";
        private const string SaveKey_Bargain     = "neroyuki.stardeweconomy/BargainData";
        private const string SaveKey_Reputation  = "neroyuki.stardeweconomy/ReputationData";
        private const string SaveKey_Truck       = "neroyuki.stardeweconomy/TruckData";

        // CP-pack item IDs (CP ModId = neroyuki.stardeweconomyitems)
        /// <summary>PDA inventory item — must be in the player's bag to use keyboard shortcuts.</summary>
        private const string PdaItemId = "neroyuki.stardeweconomyitems_PDA";

        // Tile coordinates for the permanently-spawned Town ATM (adjust if needed in-game).
        // Position is in the town plaza, south of Pierre's shop, near the fountain path.
        private static readonly Vector2 TownATMTilePos = new Vector2(40, 68);

        /*********
        ** Public methods
        *********/
        public override void Entry(IModHelper helper)
        {
            // ── Load config ──
            try
            {
                ModConfig.GetInstance().SetConfig(helper.ReadConfig<ModConfig>());
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error loading config.json, using defaults:\n{ex}", LogLevel.Error);
            }

            // ── Initialize logging ──
            LogHelper.Monitor = this.Monitor;

            // ── Register console commands ──
            _commandManager = new source.commands.Commands(this.Monitor);
            _commandManager.RegisterCommands(helper.ConsoleCommands);

            // ── Register Reputation skill EARLY (SpaceCore requires this before GameLaunched) ──
            try
            {
                ReputationSkill.SetHelper(this.Helper);
                ReputationSkill.RegisterEarly();
                Monitor.Log("Reputation skill registered with SpaceCore (early).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to register Reputation skill early:\n{ex}", LogLevel.Error);
            }

            // ── Event handlers ──
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            // ── Menu-opening console commands ──
            helper.ConsoleCommands.Add("sep_menu_contracts", "Open the Contract Board menu.", (_, __) => OpenMenuIfReady(() => new ContractBoardMenu()));
            helper.ConsoleCommands.Add("sep_menu_bargains", "Open the Bargain / Trade Offers menu.", (_, __) => OpenMenuIfReady(() => new BargainMenu()));
            helper.ConsoleCommands.Add("sep_menu_bank", "Open the Bank menu.", (_, __) => OpenMenuIfReady(() => new BankMenu()));
            helper.ConsoleCommands.Add("sep_menu_tax", "Open the Tax Bill menu.", (_, __) => OpenMenuIfReady(() => new TaxBillMenu()));
            helper.ConsoleCommands.Add("sep_tv_report", "Print a TV-style market report to the console.", (_, __) =>
            {
                string report = TvPatches.GenerateMarketReport();
                Monitor.Log(report.Replace("^", "\n"), LogLevel.Info);
            });

            Monitor.Log("Stardew Economy Project initialized.", LogLevel.Info);
        }

        /*********
        ** Private methods
        *********/

        /// <summary>Apply Harmony patches, load skill icons, and set up SpaceCore/GMCM integrations.</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // ── Harmony patches (includes TV channel) ──
            source.harmony_patches.HarmonyPatches.InitPatches(
                this.ModManifest.UniqueID,
                this.Monitor
            );

            // ── SpaceCore API for XP/level queries ──
            try
            {
                var spaceCoreApi = this.Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
                if (spaceCoreApi != null)
                {
                    ReputationSkill.SetApi(spaceCoreApi);
                    Monitor.Log("SpaceCore API acquired for Reputation skill.", LogLevel.Info);
                }
                else
                {
                    Monitor.Log("SpaceCore API not found — XP queries will use direct SpaceCore calls.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to acquire SpaceCore API:\n{ex}", LogLevel.Warn);
            }

            // ── Load skill icons now that Content Patcher has applied patches ──
            ReputationSkill.LoadIcons();

            // ── Generic Mod Config Menu integration (optional) ──
            api.ConfigMenu.Setup(this.ModManifest, this.Helper);
        }

        /// <summary>Initialize/restore all economy state when a save is loaded.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            var player = Game1.player;
            int reputationLevel = ReputationSkill.GetLevel(player);

            // ── Market Manager ──
            if (player.modData.TryGetValue(SaveKey_Market, out string marketJson))
                MarketManager.Deserialize(marketJson);
            else
                MarketManager.Initialize(reputationLevel);

            // ── Contract Manager ──
            if (player.modData.TryGetValue(SaveKey_Contracts, out string contractJson))
                ContractManager.Deserialize(contractJson);
            else
                ContractManager.Initialize();

            // ── Tax Manager ──
            if (player.modData.TryGetValue(SaveKey_Tax, out string taxJson))
                TaxManager.Deserialize(taxJson);

            // ── Bank Manager ──
            if (player.modData.TryGetValue(SaveKey_Bank, out string bankJson))
                BankManager.Deserialize(bankJson);

            // ── Bargain Manager ──
            if (player.modData.TryGetValue(SaveKey_Bargain, out string bargainJson))
                BargainManager.Deserialize(bargainJson);
            else
                BargainManager.Initialize();

            // ── Delivery Truck ──
            if (player.modData.TryGetValue(SaveKey_Truck, out string truckJson))
                source.economy.DeliveryTruckManager.Deserialize(truckJson);
            else
                source.economy.DeliveryTruckManager.Initialize();

            Monitor.Log($"Economy systems loaded. Season: {Game1.currentSeason}, Day: {Game1.dayOfMonth}, Reputation: Lv{reputationLevel}", LogLevel.Info);

            // ── Send welcome mail on first load ──
            MailManager.SendWelcome();

            // ── Spawn permanent Town ATM fixture ──
            SpawnTownATMIfNeeded();
        }

        /// <summary>Serialize all economy state before save.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            var player = Game1.player;

            player.modData[SaveKey_Market]   = MarketManager.Serialize();
            player.modData[SaveKey_Contracts] = ContractManager.Serialize();
            player.modData[SaveKey_Tax]       = TaxManager.Serialize();
            player.modData[SaveKey_Bank]      = BankManager.Serialize();
            player.modData[SaveKey_Bargain]   = BargainManager.Serialize();
            player.modData[SaveKey_Truck]     = source.economy.DeliveryTruckManager.Serialize();

            Monitor.Log("Economy state saved.", LogLevel.Trace);
        }

        /// <summary>Per-day economy updates — the daily tick for all subsystems.</summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            var config = ModConfig.GetInstance();
            var player = Game1.player;

            // ── Delivery Truck: process yesterday's cargo before anything else ──
            source.economy.DeliveryTruckManager.ProcessDayEnd();

            // ── Market Manager daily update ──
            MarketManager.OnDayStarted();

            // ── Contract Manager daily update ──
            ContractManager.OnDayStarted();

            // ── Bargain Manager daily update ──
            BargainManager.OnDayStarted();

            // ── Banking: apply daily interest ──
            if (config.EnableBanking)
            {
                BankManager.OnDayStarted();

                // Send loan payment reminder near end of season (day 25)
                if (Game1.dayOfMonth == 25 && BankManager.HasOutstandingLoan())
                    MailManager.SendLoanPaymentDue();
            }

            // ── Taxation: assess on Day 1 of each season ──
            if (config.EnableTaxation && Game1.dayOfMonth == 1)
            {
                // AssessSeasonalTaxes handles calculation, deduction, HUD message, and tracking reset internally
                TaxManager.AssessSeasonalTaxes();
                MailManager.SendTaxBill();
            }

            // ── Sprinkler tracking for utility tax ──
            if (config.EnableTaxation)
            {
                CountSprinklerTiles();
            }

            LogHelper.Debug($"[Economy] Day {Game1.dayOfMonth}, {Game1.currentSeason} — all systems updated.");

            // ── Check for reputation milestones ──
            CheckReputationMilestones();

            // ── Check for market surges ──
            CheckMarketSurges();
        }

        /// <summary>Send mail when the player reaches National or International tier.</summary>
        private void CheckReputationMilestones()
        {
            int level = ReputationSkill.GetLevel(Game1.player);
            if (level >= 10)
                MailManager.SendReputationInternational();
            else if (level >= 5)
                MailManager.SendReputationNational();
        }

        /// <summary>Send a mail alert if any market category is in a surge state.</summary>
        private void CheckMarketSurges()
        {
            var bottles = MarketManager.GetAllBottles();
            foreach (var kvp in bottles)
            {
                if (kvp.Value.IsSurgeActive)
                {
                    MailManager.SendMarketSurge();
                    break; // one alert covers all surges
                }
            }
        }

        /// <summary>Count tiles watered by sprinklers for utility tax.</summary>
        private void CountSprinklerTiles()
        {
            int sprinklerTiles = 0;
            var farm = Game1.getFarm();
            if (farm == null) return;

            foreach (var pair in farm.Objects.Pairs)
            {
                var obj = pair.Value;
                if (obj == null) continue;

                // Sprinkler types by qualified ID
                string id = obj.QualifiedItemId;
                if (id == "(O)599")       // Sprinkler → 4 tiles
                    sprinklerTiles += 4;
                else if (id == "(O)621")  // Quality Sprinkler → 8 tiles
                    sprinklerTiles += 8;
                else if (id == "(O)645")  // Iridium Sprinkler → 24 tiles
                    sprinklerTiles += 24;
            }

            TaxManager.RecordSprinklerTile(sprinklerTiles);
        }

        /// <summary>Clean up when returning to title screen.</summary>
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            MarketManager.Reset();
            ContractManager.Reset();
            TaxManager.Reset();
            BankManager.Reset();
            BargainManager.Reset();
            source.economy.DeliveryTruckManager.Reset();
        }

        /// <summary>Handle key presses to open economy menus.</summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Only handle when a save is loaded and no menu is already open
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            var config = ModConfig.GetInstance();

            // ── Contract Board ─────────────────────────────────────────
            if (e.Button == config.ContractBoardKey)
            {
                if (!PlayerHasPDA()) { ShowNoPdaError(); return; }
                Game1.activeClickableMenu = new ContractBoardMenu();
            }

            // ── Bank / ATM ─────────────────────────────────────────────
            else if (e.Button == config.BankKey)
            {
                if (!PlayerHasPDA()) { ShowNoPdaError(); return; }
                if (!HasMachineAnywhere(BigCraftablePatches.ATMachineId))
                {
                    Game1.addHUDMessage(new HUDMessage(
                        "You need an ATM Machine placed on your farm or in your house to access banking remotely.",
                        HUDMessage.error_type));
                    return;
                }
                Game1.activeClickableMenu = new BankMenu();
            }

            // ── Tax Bill ───────────────────────────────────────────────
            else if (e.Button == config.TaxBillKey)
            {
                if (!PlayerHasPDA()) { ShowNoPdaError(); return; }
                Game1.activeClickableMenu = new TaxBillMenu();
            }

            // ── Bargain / Trade Offers ─────────────────────────────────
            else if (e.Button == config.BargainKey)
            {
                if (!PlayerHasPDA()) { ShowNoPdaError(); return; }
                Game1.activeClickableMenu = new BargainMenu();
            }

            // ── Supercomputer 14-day Forecast ──────────────────────────
            else if (e.Button == config.ForecastKey)
            {
                if (!PlayerHasPDA()) { ShowNoPdaError(); return; }
                if (!HasMachineAnywhere(BigCraftablePatches.SupercomputerId))
                {
                    Game1.addHUDMessage(new HUDMessage(
                        "You need a Supercomputer placed on your farm or in your house to access the forecast remotely.",
                        HUDMessage.error_type));
                    return;
                }
                Game1.activeClickableMenu = new ForecastMenu();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PDA / MACHINE HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Returns true if the local player has a PDA anywhere in their inventory.</summary>
        private static bool PlayerHasPDA()
        {
            return Game1.player.Items.Any(item => item?.ItemId == PdaItemId);
        }

        /// <summary>Shows the standard "you need a PDA" HUD error.</summary>
        private static void ShowNoPdaError()
        {
            Game1.addHUDMessage(new HUDMessage(
                "You need a PDA in your inventory to access the economy network remotely.",
                HUDMessage.error_type));
        }

        /// <summary>
        /// Returns true if the local player has at least one of the specified
        /// BigCraftable placed anywhere on their farm or inside their farmhouse.
        /// </summary>
        private static bool HasMachineAnywhere(string machineId)
        {
            var farm = Game1.getFarm();
            var farmhouse = Game1.getLocationFromName("FarmHouse");

            bool onFarm = farm?.objects.Values
                .Any(o => o.bigCraftable.Value && o.ItemId == machineId) == true;
            bool inHouse = farmhouse?.objects.Values
                .Any(o => o.bigCraftable.Value && o.ItemId == machineId) == true;

            return onFarm || inHouse;
        }

        /// <summary>
        /// Spawns the permanent indestructible Town ATM at <see cref="TownATMTilePos"/>
        /// if it is not already there.  Called each time a save is loaded so the fixture
        /// is restored even if something removed it while the game was running.
        /// </summary>
        private void SpawnTownATMIfNeeded()
        {
            try
            {
                var town = Game1.getLocationFromName("Town");
                if (town == null)
                {
                    Monitor.Log("[SEP] Town location not found — cannot spawn Town ATM.", LogLevel.Warn);
                    return;
                }

                // Already present and correct item?
                if (town.objects.TryGetValue(TownATMTilePos, out var existing)
                    && existing.ItemId == BigCraftablePatches.TownATMachineId)
                {
                    Monitor.Log("[SEP] Town ATM Machine already present — skipping spawn.", LogLevel.Trace);
                    return;
                }

                // Remove anything blocking the tile (shouldn't happen in vanilla Town)
                town.objects.Remove(TownATMTilePos);

                // Create and place the Town ATM
                var atm = (SObject)ItemRegistry.Create($"(BC){BigCraftablePatches.TownATMachineId}");
                atm.IsSpawnedObject = false;
                town.objects[TownATMTilePos] = atm;

                Monitor.Log($"[SEP] Spawned Town ATM Machine at tile ({TownATMTilePos.X}, {TownATMTilePos.Y}) in Town.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"[SEP] Failed to spawn Town ATM Machine:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>Helper: open a menu from a console command if a save is loaded.</summary>
        private void OpenMenuIfReady(Func<IClickableMenu> createMenu)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }
            Game1.activeClickableMenu = createMenu();
        }
    }
}
