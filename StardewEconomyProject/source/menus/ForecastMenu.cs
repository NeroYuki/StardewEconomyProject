using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore.UI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.GameData.Objects;
using StardewEconomyProject.source.economy;
using SObject = StardewValley.Object;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Supercomputer 14-day forecast menu.
    /// Shows per-item supply/demand forecasts with a search bar.
    /// Displays scrollable item rows; each row shows the item name,
    /// its market category, current saturation, and a 14-day heatmap strip.
    /// </summary>
    public class ForecastMenu : IClickableMenu
    {
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width  - 64, (int)(1000 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 64, (int)(580 * S));
        private const int ForecastDays = 14;

        private RootElement Ui;
        private Table ResultsTable;
        private Textbox SearchBox;
        private string _lastSearchText = "";

        // Pre-built forecast data: categoryBottleId -> float[14]
        private readonly Dictionary<string, float[]> _forecast;

        // All shipped item data for searching
        private readonly List<ItemForecastEntry> _allItems = new();

        public ForecastMenu()
            : base(
                (Game1.uiViewport.Width - MenuWidth) / 2,
                (Game1.uiViewport.Height - MenuHeight) / 2,
                MenuWidth, MenuHeight, true)
        {
            _forecast = MarketManager.ForecastMarket(ForecastDays);
            BuildItemList();
            BuildUi();
        }

        // ==============================================================
        //  DATA
        // ==============================================================

        private void BuildItemList()
        {
            // Gather all Object-type items that have a sell price and a known category
            var objectData = Game1.objectData;
            if (objectData == null) return;

            foreach (var kvp in objectData)
            {
                try
                {
                    string itemId = kvp.Key;
                    var item = ItemRegistry.Create("(O)" + itemId) as SObject;
                    if (item == null) continue;

                    int rawPrice = item.salePrice(false);
                    if (rawPrice <= 0) continue;

                    string category = MarketCategories.FromItemCategory(item.Category);
                    string bottleId = $"{category}_Q0";
                    if (!_forecast.ContainsKey(bottleId)) continue;

                    _allItems.Add(new ItemForecastEntry
                    {
                        ItemId = itemId,
                        DisplayName = item.DisplayName,
                        Category = category,
                        BottleId = bottleId,
                    });
                }
                catch { /* skip items that fail to instantiate */ }
            }

            // Sort alphabetically
            _allItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        // ==============================================================
        //  UI
        // ==============================================================

        private void BuildUi()
        {
            Ui = new RootElement()
            {
                LocalPosition = new Vector2(xPositionOnScreen, yPositionOnScreen),
            };

            // Title
            Ui.AddChild(new Label()
            {
                String = "Supercomputer - Item Supply Forecast",
                Bold = true,
                LocalPosition = new Vector2(MenuWidth / 2 - 230, (int)(16 * S)),
                IdleTextColor = Color.DarkGoldenrod,
            });

            // Subtitle
            Ui.AddChild(new Label()
            {
                String = "Search for items to see their 14-day market forecast.",
                LocalPosition = new Vector2(24, (int)(44 * S)),
                IdleTextColor = Color.DimGray,
            });

            // Search label
            Ui.AddChild(new Label()
            {
                String = "Search:",
                LocalPosition = new Vector2(24, (int)(74 * S)),
                IdleTextColor = Color.DarkSlateGray,
            });

            // Search box
            SearchBox = new Textbox()
            {
                LocalPosition = new Vector2((int)(120 * Math.Max(1f, S)), (int)(68 * S)),
                String = "",
                Callback = _ => { /* handled in update via polling */ },
            };
            SearchBox.Selected = true;
            Ui.AddChild(SearchBox);

            // Column headers
            float headerY = (int)(108 * S);
            float heatmapLeft = HeatmapLeft();
            float cellW = CellWidth();

            Ui.AddChild(new Label()
            {
                String = "Item",
                Bold = true,
                LocalPosition = new Vector2(16, headerY),
                IdleTextColor = Color.DarkSlateGray,
            });
            Ui.AddChild(new Label()
            {
                String = "Category",
                Bold = true,
                LocalPosition = new Vector2((int)(220 * Math.Max(1f, S)), headerY),
                IdleTextColor = Color.DarkSlateGray,
            });
            Ui.AddChild(new Label()
            {
                String = "Sat%",
                Bold = true,
                LocalPosition = new Vector2((int)(340 * Math.Max(1f, S)), headerY),
                IdleTextColor = Color.DarkSlateGray,
            });

            // Day column headers (days 1-14)
            for (int d = 0; d < ForecastDays; d++)
            {
                Ui.AddChild(new Label()
                {
                    String = $"D{d + 1}",
                    LocalPosition = new Vector2(heatmapLeft + d * cellW + 2, headerY),
                    IdleTextColor = Color.DarkSlateGray,
                    NonBoldScale = 0.7f,
                });
            }

            // Results table
            float tableTop = (int)(140 * S);
            float tableHeight = MenuHeight - tableTop - (int)(60 * S);
            ResultsTable = new Table()
            {
                LocalPosition = new Vector2(16, tableTop),
                RowHeight = (int)(32 * S),
                Size = new Vector2(MenuWidth - 32, tableHeight),
            };
            Ui.AddChild(ResultsTable);

            // Legend at the bottom
            float legendY = tableTop + tableHeight + (int)(10 * S);
            float legendSpacing = (float)(MenuWidth - 32) / 4f;
            AddLegendEntry("0-30% (High)", Color.ForestGreen, 16, legendY);
            AddLegendEntry("30-55% (OK)", Color.Gold, legendSpacing, legendY);
            AddLegendEntry("55-80% (Low)", Color.OrangeRed, legendSpacing * 2, legendY);
            AddLegendEntry("80-100% (Crash)", Color.DarkRed, legendSpacing * 3, legendY);

            // Initial populate (show first items)
            PopulateResults("");
        }

        private float HeatmapLeft() => (int)(400 * Math.Max(1f, S));
        private float CellWidth() => Math.Max(12, (MenuWidth - HeatmapLeft() - 48) / (float)ForecastDays);

        private void PopulateResults(string filter)
        {
            // Remove old table and create a new one
            if (ResultsTable?.Parent != null)
                ((Container)ResultsTable.Parent).RemoveChild(ResultsTable);

            float tableTop = (int)(140 * S);
            float tableHeight = MenuHeight - tableTop - (int)(60 * S);
            ResultsTable = new Table()
            {
                LocalPosition = new Vector2(16, tableTop),
                RowHeight = (int)(32 * S),
                Size = new Vector2(MenuWidth - 32, tableHeight),
            };

            // Filter items
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allItems.Take(100).ToList()
                : _allItems
                    .Where(i => i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || i.Category.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (filtered.Count == 0)
            {
                ResultsTable.AddRow(new Element[] { new Label()
                {
                    String = "No matching items found.",
                    IdleTextColor = Color.Gray,
                    LocalPosition = new Vector2(8, 4),
                }});
            }
            else
            {
                int limit = Math.Min(filtered.Count, 20);
                for (int i = 0; i < limit; i++)
                {
                    ResultsTable.AddRow(CreateForecastRow(filtered[i]));
                }
            }

            Ui.AddChild(ResultsTable);
        }

        private Element[] CreateForecastRow(ItemForecastEntry entry)
        {
            var elems = new List<Element>();
            float cellW = CellWidth();
            float heatmapLeft = HeatmapLeft();
            int rowContentY = (int)(4 * S);

            // Item name (truncate if long)
            string nameDisplay = entry.DisplayName.Length > 18
                ? entry.DisplayName.Substring(0, 16) + ".."
                : entry.DisplayName;
            elems.Add(new Label()
            {
                String = nameDisplay,
                LocalPosition = new Vector2(0, rowContentY),
                IdleTextColor = Color.Black,
                NonBoldScale = 0.75f,
            });

            // Category
            string catDisplay = entry.Category.Length > 10
                ? entry.Category.Substring(0, 8) + ".."
                : entry.Category;
            elems.Add(new Label()
            {
                String = catDisplay,
                LocalPosition = new Vector2((int)(204 * Math.Max(1f, S)), rowContentY),
                IdleTextColor = Color.DimGray,
                NonBoldScale = 0.7f,
            });

            // Current saturation
            var bottle = MarketManager.GetBottle(entry.BottleId);
            float currentSat = bottle?.Saturation ?? 0f;
            elems.Add(new Label()
            {
                String = $"{currentSat:P0}",
                LocalPosition = new Vector2((int)(324 * Math.Max(1f, S)), rowContentY),
                IdleTextColor = SaturationColor(currentSat),
                NonBoldScale = 0.7f,
            });

            // 14-day heatmap cells
            float[] forecast = _forecast.TryGetValue(entry.BottleId, out var f) ? f : null;
            for (int d = 0; d < ForecastDays; d++)
            {
                float sat = forecast != null && d < forecast.Length ? forecast[d] : 0f;
                Color cellColor = HeatmapColor(sat);

                elems.Add(new StaticContainer()
                {
                    LocalPosition = new Vector2(heatmapLeft - 16 + d * cellW, rowContentY),
                    Size = new Vector2(Math.Max(8, cellW - 8), Math.Max(8, (int)(12 * S))),
                    OutlineColor = cellColor,
                });
            }

            return elems.ToArray();
        }

        private void AddLegendEntry(string text, Color color, float x, float y)
        {
            Ui.AddChild(new StaticContainer()
            {
                LocalPosition = new Vector2(x, y + 2),
                Size = new Vector2(14, 14),
                OutlineColor = color,
            });

            Ui.AddChild(new Label()
            {
                String = text,
                LocalPosition = new Vector2(x + 20, y),
                IdleTextColor = Color.DimGray,
                NonBoldScale = 0.75f,
            });
        }

        // ==============================================================
        //  COLOURS
        // ==============================================================

        private static Color HeatmapColor(float saturation)
        {
            if (saturation <= 0.30f) return Color.ForestGreen;
            if (saturation <= 0.55f) return Color.Gold;
            if (saturation <= 0.80f) return Color.OrangeRed;
            return Color.DarkRed;
        }

        private static Color SaturationColor(float s)
        {
            if (s <= 0.30f) return Color.ForestGreen;
            if (s <= 0.55f) return Color.DarkGoldenrod;
            if (s <= 0.80f) return Color.OrangeRed;
            return Color.DarkRed;
        }

        // ==============================================================
        //  UPDATES & DRAWING
        // ==============================================================

        public override void receiveScrollWheelAction(int direction)
        {
            ResultsTable?.Scrollbar.ScrollBy(direction / -120);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            Ui.Update();

            // Check if search text changed
            string current = SearchBox?.String ?? "";
            if (current != _lastSearchText)
            {
                _lastSearchText = current;
                PopulateResults(current);
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);
            Ui.Draw(b);
            drawMouse(b);
        }

        // ==============================================================
        //  DATA STRUCTURES
        // ==============================================================

        private class ItemForecastEntry
        {
            public string ItemId;
            public string DisplayName;
            public string Category;
            public string BottleId;
        }
    }
}
