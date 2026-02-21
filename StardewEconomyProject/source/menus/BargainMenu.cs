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
    /// Bargaining dialogue / menu using SpaceCore.UI.
    /// Shows pending NPC offers with Accept / Reject / Counter-offer controls.
    /// </summary>
    public class BargainMenu : IClickableMenu
    {
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width  - 64, (int)(820 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 64, (int)(560 * S));

        private RootElement Ui;
        private Table OffersTable;
        private Label TabPendingLabel;
        private Label TabAcceptedLabel;
        private bool ShowingAccepted = false;
        private bool _pendingRefresh = false;
        private Action _pendingAction = null;

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
                Bold = true,
                LocalPosition = new Vector2(MenuWidth / 2 - 120, (int)(16 * S)),
                IdleTextColor = Color.SaddleBrown,
            });

            // ── Tabs ──
            TabPendingLabel = new Label()
            {
                String = "Pending",
                Bold = true,
                LocalPosition = new Vector2(32, (int)(52 * S)),
                IdleTextColor = Color.DarkGoldenrod,
                HoverTextColor = Color.Gold,
                Callback = _ => { ShowingAccepted = false; _pendingRefresh = true; },
            };
            Ui.AddChild(TabPendingLabel);

            TabAcceptedLabel = new Label()
            {
                String = "Accepted (deliver items)",
                Bold = true,
                LocalPosition = new Vector2((int)(250 * Math.Max(1f, S)), (int)(52 * S)),
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
                LocalPosition = new Vector2(16, (int)(100 * S)),
                RowHeight = (int)(38 * S),
                Size = new Vector2(MenuWidth - 32, MenuHeight - (int)(140 * S)),
            };
        }

        // Column X helpers — fraction of usable table width
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
                    if (ShowingAccepted) AddAcceptedEntry(OffersTable, offer);
                    else                AddPendingEntry(OffersTable, offer);
                }
            }

            Ui.AddChild(OffersTable);
        }

        private void AddPendingEntry(Table table, BargainOffer offer)
        {
            string offerId = offer.OfferId;
            int daysLeft = offer.DeliveryDaysRemaining;

            // ── Row 1: NPC + item request (left) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"{offer.NpcName} wants {offer.Quantity}x {offer.ItemDisplayName}",
                    Bold = true,
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DarkSlateBlue,
                },
            });

            // ── Row 2: offer price + round info (left) | deadline (right) ──
            table.AddRow(new Element[]
            {
                new Label()
                {
                    String = $"Offering: {offer.OfferPrice}g   ·   Round {offer.RoundNumber} / {offer.MaxRounds}",
                    LocalPosition = new Vector2(8, (int)(4 * S)),
                    IdleTextColor = Color.DimGray,
                    NonBoldScale = 0.85f,
                },
                new Label()
                {
                    String = $"Deadline: {daysLeft} day(s)",
                    LocalPosition = new Vector2(Col(0.60f), (int)(4 * S)),
                    IdleTextColor = daysLeft <= 1 ? Color.OrangeRed : Color.DimGray,
                    NonBoldScale = 0.85f,
                },
            });

            // ── Row 3: action buttons spread across the row ──
            int suggestedCounter = (int)(offer.OfferPrice * 1.15);
            bool canCounter = offer.RoundNumber < offer.MaxRounds;

            float btnCol0 = 0.00f;
            float btnCol1 = canCounter ? 0.35f : 0.40f;
            float btnCol2 = 0.65f;

            var row3 = new List<Element>()
            {
                new Label()
                {
                    String = "[ACCEPT]",
                    Bold = true,
                    LocalPosition = new Vector2(Col(btnCol0), (int)(4 * S)),
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
                    LocalPosition = new Vector2(Col(btnCol1), (int)(4 * S)),
                    IdleTextColor = Color.Firebrick,
                    HoverTextColor = Color.Red,
                    Callback = _ => _pendingAction = () =>
                    {
                        BargainManager.RejectOffer(offerId);
                        Game1.playSound("trashcan");
                        RebuildTable();
                    },
                },
            };

            if (canCounter)
            {
                row3.Add(new Label()
                {
                    String = $"[COUNTER {suggestedCounter}g]",
                    Bold = true,
                    LocalPosition = new Vector2(Col(btnCol2), (int)(4 * S)),
                    IdleTextColor = Color.DarkOrange,
                    HoverTextColor = Color.Orange,
                    Callback = _ => _pendingAction = () =>
                    {
                        BargainManager.CounterOffer(offerId, suggestedCounter);
                        Game1.playSound("dwop");
                        RebuildTable();
                    },
                });
            }

            table.AddRow(row3.ToArray());

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
    }
}
