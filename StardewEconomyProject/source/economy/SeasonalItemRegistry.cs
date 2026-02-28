using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.Locations;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// Dynamically builds seasonal item pools from game content at runtime.
    /// Automatically includes modded crops, fruit trees, and fish.
    ///
    /// Data sources:
    ///   Crops:       Data/Crops      → CropData.Seasons + HarvestItemId   → category from item
    ///   Fruit trees: Data/FruitTrees → FruitTreeData.Seasons + Fruit[].ItemId
    ///   Fish:        Data/Locations  → SpawnFishData entries  → season from Condition field
    ///   Other:       Non-seasonal categories use static pools (always available)
    /// </summary>
    public static class SeasonalItemRegistry
    {
        // ── Data structures ──

        /// <summary>An item entry in a category pool with its seasonal availability.</summary>
        public class PoolEntry
        {
            public string QualifiedItemId;
            /// <summary>Lowercase season names this item is available in. 4 entries = all-season.</summary>
            public HashSet<string> Seasons;
        }

        /// <summary>Category name → list of pool entries.</summary>
        private static readonly Dictionary<string, List<PoolEntry>> _categoryPools = new();

        /// <summary>Qualified item ID → seasons (for quick lookup by BargainManager etc.).</summary>
        private static readonly Dictionary<string, HashSet<string>> _itemSeasons = new();

        private static readonly HashSet<string> AllSeasonsSet = new() { "spring", "summer", "fall", "winter" };

        /// <summary>SDV object category int → contract category name (seasonal items only).</summary>
        private static readonly Dictionary<int, string> CropCategoryMap = new()
        {
            [-75] = "Vegetable",
            [-79] = "Fruit",
            [-80] = "Flower",
        };

        /// <summary>Categories whose items have real seasonal availability (crops, fruit trees, fish).</summary>
        public static readonly HashSet<string> SeasonalCategories = new()
        {
            "Vegetable", "Fruit", "Flower", "Fish"
        };

        /// <summary>
        /// Map SDV object category int → contract pool name for non-seasonal items.
        /// Crops (-75), Fruit (-79), Flowers (-80), and Fish (-4) are handled
        /// by the seasonal builders so they're excluded here.
        /// </summary>
        private static string MapNonSeasonalCategory(int category)
        {
            return category switch
            {
                -5 or -6 or -18 => "AnimalProduct",  // Egg, Milk, Animal-related
                -26 => "ArtisanGoods",                // Cheese, Wine, Honey, etc.
                -2 or -12 => "Mineral",               // Gems, Minerals
                -81 => "Forage",                       // Greens / Forage
                -7 => "Cooking",                       // Cooked dishes
                -16 or -28 => "Default",               // Building Resources, Monster Loot
                _ => null,
            };
        }

        public static bool IsInitialized { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  REBUILD (call once per save load)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuild all seasonal item data from game content.
        /// Must be called after game data is loaded (e.g. on SaveLoaded).
        /// </summary>
        public static void Rebuild()
        {
            _categoryPools.Clear();
            _itemSeasons.Clear();

            try
            {
                BuildCropData();
                BuildFruitTreeData();
                BuildFishData();
                BuildNonSeasonalPools();

                IsInitialized = true;

                int totalItems = _categoryPools.Sum(p => p.Value.Count);
                int seasonalItems = _itemSeasons.Count;
                string poolSummary = string.Join(", ",
                    _categoryPools.Select(p => $"{p.Key}={p.Value.Count}"));

                LogHelper.Info($"[SeasonalItems] Registry built: {seasonalItems} seasonal items, " +
                    $"{totalItems} total pool entries. Pools: {poolSummary}");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[SeasonalItems] Failed to build registry: {ex}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  BUILD METHODS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Build seasonal data for all crops from Data/Crops.</summary>
        private static void BuildCropData()
        {
            var cropData = Game1.cropData;
            if (cropData == null) return;

            int count = 0;
            foreach (var kvp in cropData)
            {
                var crop = kvp.Value;
                if (crop?.HarvestItemId == null || crop.Seasons == null || crop.Seasons.Count == 0)
                    continue;

                string qid = QualifyId(crop.HarvestItemId);

                // Extract season names
                var seasons = new HashSet<string>();
                foreach (var s in crop.Seasons)
                    seasons.Add(s.ToString().ToLower());

                // Store / merge season data
                if (!_itemSeasons.ContainsKey(qid))
                    _itemSeasons[qid] = seasons;
                else
                    _itemSeasons[qid].UnionWith(seasons);

                // Determine contract category by creating the item and checking its SDV category
                try
                {
                    var item = ItemRegistry.Create(qid);
                    if (item is SObject obj && obj.Price > 0 && obj.canBeShipped())
                    {
                        if (CropCategoryMap.TryGetValue(obj.Category, out string catName))
                        {
                            AddToPool(catName, qid, _itemSeasons[qid]);
                            count++;
                        }
                    }
                }
                catch { /* skip invalid items */ }
            }

            LogHelper.Debug($"[SeasonalItems] Loaded {count} crop items from {cropData.Count} crop entries.");
        }

        /// <summary>Build seasonal data for all fruit trees from Data/FruitTrees.</summary>
        private static void BuildFruitTreeData()
        {
            Dictionary<string, FruitTreeData> fruitTrees;
            try
            {
                fruitTrees = Game1.content.Load<Dictionary<string, FruitTreeData>>("Data/FruitTrees");
            }
            catch
            {
                LogHelper.Warn("[SeasonalItems] Could not load Data/FruitTrees.");
                return;
            }

            if (fruitTrees == null) return;

            int count = 0;
            foreach (var kvp in fruitTrees)
            {
                var tree = kvp.Value;
                if (tree?.Fruit == null || tree.Seasons == null || tree.Seasons.Count == 0)
                    continue;

                // Extract tree seasons
                var treeSeasons = new HashSet<string>();
                foreach (var s in tree.Seasons)
                    treeSeasons.Add(s.ToString().ToLower());

                foreach (var fruit in tree.Fruit)
                {
                    if (string.IsNullOrEmpty(fruit?.ItemId)) continue;

                    string qid = QualifyId(fruit.ItemId);

                    // Merge seasons (a fruit might also appear as a crop harvest)
                    if (!_itemSeasons.ContainsKey(qid))
                        _itemSeasons[qid] = new HashSet<string>(treeSeasons);
                    else
                        _itemSeasons[qid].UnionWith(treeSeasons);

                    // Add to category pool — fruit tree products are typically in the Fruit category
                    try
                    {
                        var item = ItemRegistry.Create(qid);
                        if (item is SObject obj && obj.Price > 0 && obj.canBeShipped())
                        {
                            string catName = CropCategoryMap.GetValueOrDefault(obj.Category, "Fruit");
                            AddToPool(catName, qid, _itemSeasons[qid]);
                            count++;
                        }
                    }
                    catch { /* skip invalid items */ }
                }
            }

            LogHelper.Debug($"[SeasonalItems] Loaded {count} fruit tree items from {fruitTrees.Count} tree entries.");
        }

        /// <summary>
        /// Build seasonal data for fish from Data/Locations fish spawn entries.
        /// Fish seasonality comes from the Condition field on SpawnFishData entries.
        /// Seasons are unioned across all locations (if a fish is available in any
        /// location during a season, it counts as available that season).
        /// </summary>
        private static void BuildFishData()
        {
            Dictionary<string, LocationData> locations;
            try
            {
                locations = Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations");
            }
            catch
            {
                LogHelper.Warn("[SeasonalItems] Could not load Data/Locations.");
                return;
            }

            if (locations == null) return;

            // Accumulate seasons for each fish across all locations
            var fishSeasons = new Dictionary<string, HashSet<string>>();

            foreach (var locKvp in locations)
            {
                var loc = locKvp.Value;
                if (loc?.Fish == null) continue;

                foreach (var fish in loc.Fish)
                {
                    if (string.IsNullOrEmpty(fish?.ItemId)) continue;

                    // Skip special item query syntax
                    if (fish.ItemId.Contains("RANDOM") || fish.ItemId.Contains("FLAVORED")
                        || fish.ItemId.Contains("SECRET_NOTE") || fish.ItemId.Contains("LOST_BOOK"))
                        continue;

                    string qid = QualifyId(fish.ItemId);

                    if (!fishSeasons.TryGetValue(qid, out var seasons))
                    {
                        seasons = new HashSet<string>();
                        fishSeasons[qid] = seasons;
                    }

                    // Extract season data from the Condition string
                    var entrySeasons = ExtractFishEntrySeasons(fish);
                    seasons.UnionWith(entrySeasons);
                }
            }

            // Add valid fish to the Fish pool
            int count = 0;
            foreach (var kvp in fishSeasons)
            {
                string qid = kvp.Key;
                var seasons = kvp.Value;

                // Default to all-season if no specific season found
                if (seasons.Count == 0)
                    seasons.UnionWith(AllSeasonsSet);

                _itemSeasons[qid] = seasons;

                try
                {
                    var item = ItemRegistry.Create(qid);
                    if (item is SObject obj && obj.Price > 0 && obj.Category == -4) // -4 = Fish
                    {
                        AddToPool("Fish", qid, seasons);
                        count++;
                    }
                }
                catch { /* skip invalid items */ }
            }

            LogHelper.Debug($"[SeasonalItems] Loaded {count} fish from location data.");
        }

        /// <summary>
        /// Extract season information from a fish spawn entry's Condition string.
        /// Handles patterns like "LOCATION_SEASON Here spring" and negations
        /// like "!LOCATION_SEASON Here winter".
        /// </summary>
        private static HashSet<string> ExtractFishEntrySeasons(SpawnFishData fish)
        {
            var result = new HashSet<string>();

            if (!string.IsNullOrEmpty(fish.Condition))
            {
                string cond = fish.Condition;

                // Match "LOCATION_SEASON <target> <seasons...>" or "!LOCATION_SEASON <target> <seasons...>"
                var match = Regex.Match(cond,
                    @"(!?)LOCATION_SEASON\s+\w+\s+([^,]+)",
                    RegexOptions.IgnoreCase);

                // Also try plain "SEASON <seasons...>" game state query
                if (!match.Success)
                    match = Regex.Match(cond, @"(!?)SEASON\s+([^,]+)", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    bool negated = match.Groups[1].Value == "!";
                    string seasonPart = match.Groups[2].Value.ToLower().Trim();

                    var mentioned = new HashSet<string>();
                    foreach (string s in AllSeasonsSet)
                    {
                        if (seasonPart.Contains(s))
                            mentioned.Add(s);
                    }

                    if (mentioned.Count > 0)
                    {
                        if (negated)
                        {
                            // !LOCATION_SEASON Here winter → spring, summer, fall
                            foreach (string s in AllSeasonsSet)
                            {
                                if (!mentioned.Contains(s))
                                    result.Add(s);
                            }
                        }
                        else
                        {
                            result.UnionWith(mentioned);
                        }
                    }
                }
            }

            // No season restriction found → all seasons
            if (result.Count == 0)
                result.UnionWith(AllSeasonsSet);

            return result;
        }

        /// <summary>
        /// Build non-seasonal item pools by scanning all objects in the game.
        /// Every item whose SDV category matches a non-seasonal pool is included,
        /// automatically picking up modded items.
        /// </summary>
        private static void BuildNonSeasonalPools()
        {
            var objectData = Game1.objectData;
            if (objectData == null) return;

            int count = 0;
            foreach (var key in objectData.Keys)
            {
                string qid = $"(O){key}";

                // Skip items already registered by seasonal builders (crops, fruit trees, fish)
                if (_itemSeasons.ContainsKey(qid)) continue;

                try
                {
                    var item = ItemRegistry.Create(qid);
                    if (item is not SObject obj) continue;
                    if (obj.Price <= 0) continue;
                    if (!obj.canBeShipped()) continue;

                    string catName = MapNonSeasonalCategory(obj.Category);
                    if (catName == null) continue;

                    var seasons = new HashSet<string>(AllSeasonsSet);
                    AddToPool(catName, qid, seasons);
                    _itemSeasons[qid] = seasons;
                    count++;
                }
                catch { /* skip invalid items */ }
            }

            LogHelper.Debug($"[SeasonalItems] Loaded {count} non-seasonal items from object data.");
        }

        // ══════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════

        /// <summary>Get the item pool for a contract category. Returns null if category unknown.</summary>
        public static List<PoolEntry> GetItemPool(string category)
        {
            if (!IsInitialized) Rebuild();
            return _categoryPools.TryGetValue(category, out var pool) ? pool : null;
        }

        /// <summary>Check if a set of seasons includes the current game season.</summary>
        public static bool IsInSeason(HashSet<string> seasons)
        {
            if (seasons == null || seasons.Count == 0) return true;
            if (seasons.Count >= 4) return true; // all seasons = always in season
            return seasons.Contains(Game1.currentSeason);
        }

        /// <summary>Check if an item (by qualified ID) is available in the current season.</summary>
        public static bool IsItemInSeason(string qualifiedItemId)
        {
            if (!IsInitialized) Rebuild();
            if (_itemSeasons.TryGetValue(qualifiedItemId, out var seasons))
                return IsInSeason(seasons);
            return true; // unknown items → assume always available
        }

        /// <summary>Check if an item is available in the current season.</summary>
        public static bool IsItemInSeason(Item item)
        {
            if (item == null) return false;
            return IsItemInSeason(item.QualifiedItemId);
        }

        /// <summary>Get the seasons an item is available in, or null if unknown.</summary>
        public static HashSet<string> GetItemSeasons(string qualifiedItemId)
        {
            if (!IsInitialized) Rebuild();
            return _itemSeasons.TryGetValue(qualifiedItemId, out var s) ? s : null;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Ensure an item ID is qualified with the (O) prefix for objects.</summary>
        private static string QualifyId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;
            if (itemId.StartsWith("(")) return itemId; // already qualified
            return $"(O){itemId}";
        }

        /// <summary>Add an item to a category pool, avoiding duplicates.</summary>
        private static void AddToPool(string category, string qualifiedId, HashSet<string> seasons)
        {
            if (!_categoryPools.TryGetValue(category, out var pool))
            {
                pool = new List<PoolEntry>();
                _categoryPools[category] = pool;
            }

            // Skip if already present (e.g. a crop and fruit tree might share an item)
            if (pool.Any(e => e.QualifiedItemId == qualifiedId))
                return;

            pool.Add(new PoolEntry { QualifiedItemId = qualifiedId, Seasons = seasons });
        }
    }
}
