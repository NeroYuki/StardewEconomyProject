using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceCore.UI;
using StardewValley;
using StardewValley.Menus;
using StardewEconomyProject.source.economy;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Bargaining dialogue / menu using SpaceCore.UI.
    /// Shows pending NPC offers with Accept / Reject / Counter-offer controls.
    /// </summary>
    public class BargainMenu : IClickableMenu
    {
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width  - 32, (int)(820 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 32, (int)(560 * S));

        private RootElement Ui;
        private Table OffersTable;
        private Label TabPendingLabel;
        private Label TabAcceptedLabel;
        private bool ShowingAccepted = false;
        private bool _pendingRefresh = false;
        private Action _pendingAction = null;

        // Tracks the player's current counter-offer per offer ID (survives rebuilds)
        private readonly Dictionary<string, int> _counterValues = new();

        public BargainMenu()
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

            // Title
            Ui.AddChild(new Label()
            {
                String = "NPC Trade Offers",
                // Bold = true,  for some godforsaken reason bold font just does not accept any other color than drak gray (SpaceShared.UI.Label.cs line 78)
                LocalPosition = new Vector2(MenuWidth / 2 - 120, (int)(16 * S)),
                IdleTextColor = Color.White,
                HoverTextColor = Color.White,
            });

            // ── Tabs ──
            TabPendingLabel = new Label()
            {
                String = "Pending",
                Bold = true,
                LocalPosition = new Vector2(32, (int)(72 * S)),
                IdleTextColor = Color.DarkGoldenrod,
                HoverTextColor = Color.Gold,
                Callback = _ => { ShowingAccepted = false; _pendingRefresh = true; },
            };
            Ui.AddChild(TabPendingLabel);

            TabAcceptedLabel = new Label()
            {
                String = "Accepted (deliver items)",
                Bold = true,
                LocalPosition = new Vector2((int)(250 * Math.Max(1f, S)), (int)(72 * S)),
                IdleTextColor = Color.Gray,
                HoverTextColor = Color.Gold,
                Callback = _ => { ShowingAccepted = true; _pendingRefresh = true; },
            };
            Ui.AddChild(TabAcceptedLabel);

            OffersTable = MakeTable();
            Ui.AddChild(OffersTable);

            RebuildTable();
        }

        private Table MakeTable()
        {
            return new Table()
            {
                LocalPosition = new Vector2(16, (int)(120 * S)),
                RowHeight = (int)(38 * S),
                Size = new Vector2(MenuWidth - 32, MenuHeight - (int)(160 * S)),
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private int Col(float frac) => 8 + (int)((MenuWidth - 48) * frac);

        private void AddSeparatorRow(Table table)
        {
            table.AddRow(new Element[] { new Label()
            {
                String = new string('─', 66),
                LocalPosition = new Vector2(8, (int)(14 * S)),
                IdleTextColor = Color.LightGray,
                NonBoldScale = 0.55f,
            }});
        }

        private void RefreshStyles()
        {
            TabPendingLabel.IdleTextColor = ShowingAccepted ? Color.Gray : Color.DarkGoldenrod;
            TabAcceptedLabel.IdleTextColor = ShowingAccepted ? Color.DarkGoldenrod : Color.Gray;
        }

        private void RebuildTable()
        {
            if (OffersTable?.Parent != null)
                ((Container)OffersTable.Parent).RemoveChild(OffersTable);

            OffersTable = MakeTable();

            var offers = ShowingAccepted
                ? BargainManager.GetAcceptedOffers()
                : BargainManager.GetPendingOffers();

            if (offers.Count == 0)
            {
                string msg = ShowingAccepted
                    ? "No accepted trades awaiting delivery."
                    : "No trade offers right now. NPCs may approach you soon!";
                OffersTable.AddRow(new Element[] { new Label()
                {
                    String = msg,
                    IdleTextColor = Color.Gray,
                    LocalPosition = new Vector2(16, 8),
                }});
            }
            else
            {
                foreach (var offer in offers)
                {
                    if (ShowingAccepted) 
                        AddAcceptedEntry(OffersTable, offer);
                    else                
                        AddPendingEntry(OffersTable, offer);
                }
            }

            Ui.AddChild(OffersTable);
        }

        private void AddPendingEntry(Table table, BargainOffer offer)
        {
            string offerId = offer.OfferId;
            int daysLeft   = offer.DeliveryDaysRemaining;
            bool canCounter = offer.RoundNumber < offer.MaxRounds;

            // Slider range: NPC offer (min) up to 3× offer (max), snapping by 5g
            int sliderMin  = offer.OfferPrice;
            int sliderMax  = Math.Max(sliderMin + 5, offer.OfferPrice * 3);
            int sliderDefault = canCounter
                ? Math.Min(sliderMax, (int)(offer.OfferPrice * 1.15) / 5 * 5 + 5)
                : sliderMin;
            if (!_counterValues.ContainsKey(offerId))
                _counterValues[offerId] = sliderDefault;
            int currentVal = _counterValues[offerId];

            // ── Row 1: who + what │ deadline ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{offer.NpcName} wants {offer.Quantity}x {offer.ItemDisplayName}",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkSlateBlue,
                },
                new Label()
                {
                    String = $"Deliver within: {daysLeft} day(s)",
                    LocalPosition = new Vector2(Col(0.62f), (int)(4 * S)),
                    IdleTextColor = daysLeft <= 1 ? Color.OrangeRed : Color.DimGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 2: NPC offer + round ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"NPC offering: {offer.OfferPrice}g   \u00b7   Round {offer.RoundNumber} / {offer.MaxRounds}",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 3: counter-offer slider  (only shown when counter is available) ──
            if (canCounter)
            {
                int sliderWidth = (int)((MenuWidth - 80) * 0.62f);

                // Live value label — updated directly by the slider Callback (safe: no tree mutation)
                var valueLabel = new Label()
                {
                    String = $"Your offer: {currentVal}g",
                    Bold = true,
                    LocalPosition = new Vector2(sliderWidth + (int)(24 * Math.Max(1f, S)), (int)(4 * S)),
                    IdleTextColor = Color.DarkOrange,
                };

                var slider = new IntSlider()
                {
                    RequestWidth = sliderWidth,
                    Minimum  = sliderMin,
                    Maximum  = sliderMax,
                    Value    = currentVal,
                    Interval = 5,
                    LocalPosition = new Vector2(8, (int)(7 * S)),
                    Callback = el =>
                    {
                        int v = ((IntSlider)el).Value;
                        _counterValues[offerId] = v;
                        valueLabel.String = $"Your offer: {v}g";
                    },
                };

                table.AddRow(new Element[] { slider, valueLabel });
            }
            else
            {
                // No counter available — show a greyed hint
                table.AddRow(new Element[]
                {
                    new Label()
                    {
                        String = "No counter available (final round)",
                        LocalPosition = new Vector2(8, (int)(4 * S)),
                        IdleTextColor = Color.Gray,
                        NonBoldScale = 0.8f,
                    },
                });
            }

            // ── Row 4: action buttons ──
            var row4 = new List<Element>()
            {
                new Label()
                {
                    String = "[ACCEPT]",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.ForestGreen,
                    HoverTextColor = Color.LimeGreen,
                    Callback = _ => _pendingAction = () =>
                    {
                        BargainManager.AcceptOffer(offerId);
                        Game1.playSound("purchase");
                        RebuildTable();
                    },
                },
                new Label()
                {
                    String = "[REJECT]",
                    Bold = true,
                    LocalPosition = new Vector2(Col(0.28f), (int)(4 * S)),
                    IdleTextColor = Color.Firebrick,
                    HoverTextColor = Color.Red,
                    Callback = _ => _pendingAction = () =>
                    {
                        BargainManager.RejectOffer(offerId);
                        _counterValues.Remove(offerId);
                        Game1.playSound("trashcan");
                        RebuildTable();
                    },
                },
            };

            if (canCounter)
            {
                row4.Add(new Label()
                {
                    String = "[COUNTER MY OFFER]",
                    Bold = true,
                    LocalPosition = new Vector2(Col(0.55f), (int)(4 * S)),
                    IdleTextColor = Color.DarkOrange,
                    HoverTextColor = Color.Orange,
                    Callback = _ => _pendingAction = () =>
                    {
                        int v = _counterValues.TryGetValue(offerId, out int cv) ? cv : sliderDefault;
                        BargainManager.CounterOffer(offerId, v);
                        Game1.playSound("dwop");
                        RebuildTable();
                    },
                });
            }

            table.AddRow(row4.ToArray());

            // ── Divider ──
            AddSeparatorRow(table);
        }

        private void AddAcceptedEntry(Table table, BargainOffer offer)
        {
            // ── Row 1: NPC + item ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{offer.NpcName}: {offer.Quantity}x {offer.ItemDisplayName}",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkGreen,
                },
            });

            // ── Row 2: agreed price (left) | deadline (right) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"Agreed price: {offer.OfferPrice}g",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.85f,
                },
                new Label()
                {
                    String = $"Deliver within: {offer.DeliveryDaysRemaining} day(s)",
                    LocalPosition = new Vector2(Col(0.52f), (int)(4 * S)),
                    IdleTextColor = offer.DeliveryDaysRemaining <= 1 ? Color.OrangeRed : Color.DimGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 3: delivery tip (full width) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = "Talk to the NPC with the items in your inventory to deliver.",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.Gray,
                    NonBoldScale = 0.8f,
                },
            });

            // ── Divider ──
            AddSeparatorRow(table);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            OffersTable.Scrollbar.ScrollBy(direction / -120);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            Ui.Update();

            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                RefreshStyles();
                RebuildTable();
            }

            if (_pendingAction != null)
            {
                var act = _pendingAction;
                _pendingAction = null;
                act();
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);
            Ui.Draw(b);
            drawMouse(b);
        }

        // ══════════════════════════════════════════════════════════════
        //  INNER CLASS: integer slider replicating SpaceCore Slider<int>
        //  (Slider<T> is internal in SpaceCore; we subclass the public base)
        // ══════════════════════════════════════════════════════════════

        private class IntSlider : SpaceCore.UI.Slider
        {
            public int Minimum { get; set; }
            public int Maximum { get; set; }
            public int Value   { get; set; }
            public int Interval { get; set; } = 1;

            public override void Update(bool isOffScreen = false)
            {
                base.Update(isOffScreen);

                if (this.Clicked)
                    this.Dragging = true;
                if (Mouse.GetState().LeftButton == ButtonState.Released)
                    this.Dragging = false;

                if (this.Dragging)
                {
                    float perc = (Game1.getOldMouseX() - this.Position.X) / Math.Max(1, this.Width);
                    perc = Math.Clamp(perc, 0f, 1f);
                    int raw = (int)(perc * (Maximum - Minimum) + Minimum);
                    if (Interval > 1) raw = (raw / Interval) * Interval;
                    Value = Math.Clamp(raw, Minimum, Maximum);
                    this.Callback?.Invoke(this);
                }
            }

            public override void Draw(SpriteBatch b)
            {
                if (this.IsHidden()) return;

                float perc = Maximum > Minimum ? (float)(Value - Minimum) / (Maximum - Minimum) : 0f;
                int bx = (int)this.Position.X;
                int by = (int)this.Position.Y;

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                    new Rectangle(403, 383, 6, 6),
                    bx, by, this.Width, this.Height,
                    Color.White, Game1.pixelZoom, false);
                b.Draw(Game1.mouseCursors,
                    new Vector2(bx + perc * (this.Width - 40), by),
                    new Rectangle(420, 441, 10, 6),
                    Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.9f);
            }
        }
    }
}
