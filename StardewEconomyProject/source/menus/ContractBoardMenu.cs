using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore.UI;
using StardewValley;
using StardewValley.Menus;
using StardewEconomyProject.source.economy;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Full-screen contract board menu using SpaceCore.UI.
    /// Shows available contracts (accept) and active contracts (progress).
    /// </summary>
    public class ContractBoardMenu : IClickableMenu
    {
        // Scale factor from config — applied to row heights, padding, and menu dimensions.
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width - 32, (int)(900 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 32, (int)(664 * S));

        private RootElement Ui;
        private Table AvailableTable;
        private Table ActiveTable;
        private Label TabAvailableLabel;
        private Label TabActiveLabel;

        private bool ShowingActive = false;
        private bool _pendingRefresh = false;

        public ContractBoardMenu()
            : base(
                (Game1.uiViewport.Width - MenuWidth) / 2,
                (Game1.uiViewport.Height - MenuHeight) / 2,
                MenuWidth, MenuHeight, true)
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Ui = new RootElement()
            {
                LocalPosition = new Vector2(xPositionOnScreen, yPositionOnScreen),
            };

            // ── Tab bar ──
            TabAvailableLabel = new Label()
            {
                String = "Available Contracts",
                Bold = true,
                LocalPosition = new Vector2(64, (int)(72 * S)),
                IdleTextColor = ShowingActive ? Color.Gray : Color.DarkGoldenrod,
                HoverTextColor = Color.Gold,
                Callback = _ => { ShowingActive = false; _pendingRefresh = true; },
            };
            Ui.AddChild(TabAvailableLabel);

            TabActiveLabel = new Label()
            {
                String = "Active Contracts",
                Bold = true,
                LocalPosition = new Vector2((int)(480 * S), (int)(72 * S)),
                IdleTextColor = ShowingActive ? Color.DarkGoldenrod : Color.Gray,
                HoverTextColor = Color.Gold,
                Callback = _ => { ShowingActive = true; _pendingRefresh = true; },
            };
            Ui.AddChild(TabActiveLabel);

            // ── Tables (both added to Ui so they have a parent; visibility controlled via ForceHide) ──
            AvailableTable = MakeTable();
            Ui.AddChild(AvailableTable);

            ActiveTable = MakeTable();
            ActiveTable.ForceHide = () => !ShowingActive;
            Ui.AddChild(ActiveTable);

            AvailableTable.ForceHide = () => ShowingActive;

            RefreshContent();
        }

        private Table MakeTable()
        {
            return new Table()
            {
                LocalPosition = new Vector2(16, (int)(120 * S)),
                RowHeight = (int)(32 * S),
                Size = new Vector2(MenuWidth - 32, MenuHeight - (int)(160 * S)),
            };
        }

        // Column X helpers — fraction of usable table width
        private int Col(float frac) => 8 + (int)((MenuWidth - 48) * frac);

        private void AddSeparatorRow(Table table)
        {
            table.AddRow(new Element[] { new Label()
            {
                String = new string('─', 72),
                LocalPosition = new Vector2(8, (int)(14 * S)),
                IdleTextColor = Color.LightGray,
                NonBoldScale = 0.55f,
            }});
        }

        private void RefreshTabs()
        {
            TabAvailableLabel.IdleTextColor = ShowingActive ? Color.Gray : Color.DarkGoldenrod;
            TabActiveLabel.IdleTextColor = ShowingActive ? Color.DarkGoldenrod : Color.Gray;
        }

        private void RefreshContent()
        {
            PopulateAvailable();
            PopulateActive();
        }

        private void ReplaceTable(ref Table field, Action<Table> populate)
        {
            var hide = field.ForceHide;
            if (field.Parent != null)
                ((Container)field.Parent).RemoveChild(field);
            field = MakeTable();
            field.ForceHide = hide;
            populate(field);
            Ui.AddChild(field);
        }

        private void PopulateAvailable()
        {
            ReplaceTable(ref AvailableTable, table =>
            {
                var contracts = ContractManager.GetAvailableContracts();
                if (contracts.Count == 0)
                {
                    table.AddRow(new Element[] { new Label()
                    {
                        String = "No contracts available today. Check back tomorrow!",
                        IdleTextColor = Color.Gray,
                        LocalPosition = new Vector2(16, 8),
                    }});
                }
                else
                {
                    foreach (var c in contracts)
                        AddAvailableEntry(table, c);
                }
            });
        }

        private void PopulateActive()
        {
            ReplaceTable(ref ActiveTable, table =>
            {
                var contracts = ContractManager.GetActiveContracts();
                if (contracts.Count == 0)
                {
                    table.AddRow(new Element[] { new Label()
                    {
                        String = "No active contracts. Accept some from the board!",
                        IdleTextColor = Color.Gray,
                        LocalPosition = new Vector2(16, 8),
                    }});
                }
                else
                {
                    foreach (var c in contracts)
                        AddActiveEntry(table, c);
                }
            });
        }

        private void AddAvailableEntry(Table table, Contract contract)
        {
            string contractId = contract.ContractId;
            string qualStr = contract.MinimumQuality switch
            {
                1 => "Silver+",
                2 => "Gold+",
                4 => "Iridium",
                _ => "Any quality"
            };

            // Build a human-readable items string, truncated so it fits one row
            var itemsList = contract.RequiredItems.Select(ri => $"{ri.Value}x {ri.Key}").ToList();
            string itemsStr = TruncateItems(itemsList);

            // ── Row 1: contract title (left) + [ACCEPT] button (right) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{contract.RequesterNpc}: {contract.Name}",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.SaddleBrown,
                },
                new Label()
                {
                    String = "[ACCEPT]",
                    Bold = true,
                    LocalPosition = new Vector2(Col(0.78f), (int)(4 * S)),
                    IdleTextColor = Color.ForestGreen,
                    HoverTextColor = Color.LimeGreen,
                    Callback = _ =>
                    {
                        bool ok = ContractManager.AcceptContract(contractId);
                        Game1.playSound(ok ? "newArtifact" : "cancel");
                        if (ok) _pendingRefresh = true;
                    },
                },
            });

            // ── Row 2: required items (full width) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"Deliver: {itemsStr}",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkSlateGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 3: quality (left) | reward (centre) | duration (right) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"Quality: {qualStr}",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.8f,
                },
                new Label()
                {
                    String = $"Reward: {contract.TotalReward}g",
                    LocalPosition = new Vector2(Col(0.38f), (int)(4 * S)),
                    IdleTextColor = Color.DarkGreen,
                    NonBoldScale = 0.8f,
                },
                new Label()
                {
                    String = $"Duration: {contract.DurationDays} days",
                    LocalPosition = new Vector2(Col(0.65f), (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.8f,
                },
            });

            // ── Divider ──
            AddSeparatorRow(table);
        }

        private void AddActiveEntry(Table table, Contract contract)
        {
            // Build progress string per required item, truncated to fit
            var itemsList = contract.RequiredItems.Select(ri =>
            {
                contract.DeliveredItems.TryGetValue(ri.Key, out int delivered);
                return $"{delivered}/{ri.Value}x {ri.Key}";
            }).ToList();
            string progressStr = TruncateItems(itemsList);

            Color timeColor = contract.DaysRemaining <= 2 ? Color.OrangeRed : Color.DimGray;

            // ── Row 1: contract title ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{contract.RequesterNpc}: {contract.Name}",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkBlue,
                },
            });

            // ── Row 2: item delivery progress (full width) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"Progress: {progressStr}",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkSlateGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 3: completion % (left) | days remaining (centre) | reward (right) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{contract.CompletionPercentage:P0} complete",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.8f,
                },
                new Label()
                {
                    String = $"Days left: {contract.DaysRemaining}",
                    LocalPosition = new Vector2(Col(0.38f), (int)(4 * S)),
                    IdleTextColor = timeColor,
                    NonBoldScale = 0.8f,
                },
                new Label()
                {
                    String = $"Reward: {contract.TotalReward}g",
                    LocalPosition = new Vector2(Col(0.65f), (int)(4 * S)),
                    IdleTextColor = Color.DarkGreen,
                    NonBoldScale = 0.8f,
                },
            });

            // ── Divider ──
            AddSeparatorRow(table);
        }

        /// <summary>Joins item strings; truncates to a safe display length.</summary>
        private static string TruncateItems(List<string> items)
        {
            const int maxLen = 62;
            string full = string.Join(", ", items);
            if (full.Length <= maxLen) return full;
            // Try progressively fewer items
            for (int take = items.Count - 1; take >= 1; take--)
            {
                string partial = string.Join(", ", items.Take(take));
                int extra = items.Count - take;
                string candidate = extra > 0 ? $"{partial}  +{extra} more" : partial;
                if (candidate.Length <= maxLen) return candidate;
            }
            // Last resort: hard truncate
            return full.Substring(0, maxLen - 3) + "...";
        }

        public override void receiveScrollWheelAction(int direction)
        {
            var table = ShowingActive ? ActiveTable : AvailableTable;
            table.Scrollbar.ScrollBy(direction / -120);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            Ui.Update();

            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                RefreshTabs();
                RefreshContent();
            }
        }

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

            // Draw menu box
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            Ui.Draw(b);

            // Tooltip for hovered items
            drawMouse(b);
        }
    }
}
