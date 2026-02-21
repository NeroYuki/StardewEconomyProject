using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore.UI;
using StardewValley;
using StardewValley.Menus;
using StardewEconomyProject.source.economy;

namespace StardewEconomyProject.source.menus
{
    /// <summary>
    /// Tax bill / assessment display showing current tracking,
    /// estimated taxes, and the last seasonal bill.
    /// </summary>
    public class TaxBillMenu : IClickableMenu
    {
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width  - 64, (int)(720 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 64, (int)(520 * S));

        private RootElement Ui;

        public TaxBillMenu()
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

            float y = (int)(20 * S);

            // Title
            Ui.AddChild(new Label()
            {
                String = "Tax Assessment Office",
                Bold = true,
                LocalPosition = new Vector2(MenuWidth / 2 - 140, y),
                IdleTextColor = Color.DarkGoldenrod,
            });
            y += (int)(48 * S);

            // ── Current Season Tracking ──
            Ui.AddChild(SectionHeader("Current Season Activity", y));
            y += (int)(32 * S);

            var tracking = TaxManager.GetCurrentTracking();
            AddRow("Gross Income This Season:", $"{tracking.SeasonGrossIncome}g", y);
            y += (int)(26 * S);
            AddRow("Sprinkler Tiles Watered:", $"{tracking.SprinklerTilesWatered}", y);
            y += (int)(26 * S);
            AddRow("Machine Activations:", $"{tracking.MachineActivations}", y);
            y += (int)(40 * S);

            // ── Estimated Tax ──
            Ui.AddChild(SectionHeader("Estimated Tax (End of Season)", y));
            y += (int)(32 * S);

            var estimate = TaxManager.EstimateCurrentTax();
            int estimatedTotal = estimate.IncomeTax + estimate.UtilityTax + estimate.PropertyTax;

            AddRow("Est. Income Tax:", $"{estimate.IncomeTax}g", y, estimate.IncomeTax > 0 ? Color.Firebrick : Color.DimGray);
            y += (int)(26 * S);
            AddRow("Est. Utility Tax:", $"{estimate.UtilityTax}g", y, estimate.UtilityTax > 0 ? Color.DarkOrange : Color.DimGray);
            y += (int)(26 * S);
            AddRow("Est. Property Tax:", $"{estimate.PropertyTax}g", y, estimate.PropertyTax > 0 ? Color.DarkOrange : Color.DimGray);
            y += (int)(26 * S);

            Ui.AddChild(new Label()
            {
                String = $"Estimated Total Tax: {estimatedTotal}g",
                Bold = true,
                LocalPosition = new Vector2((int)(48 * S), y),
                IdleTextColor = estimatedTotal > 0 ? Color.Firebrick : Color.ForestGreen,
            });
            y += (int)(28 * S);

            // Show effective rate
            if (tracking.SeasonGrossIncome > 0)
            {
                float effectiveRate = (float)estimatedTotal / tracking.SeasonGrossIncome * 100f;
                Ui.AddChild(new Label()
                {
                    String = $"Effective Tax Rate: {effectiveRate:F1}%",
                    LocalPosition = new Vector2((int)(48 * S), y),
                    IdleTextColor = Color.DimGray,
                });
            }
            y += (int)(48 * S);

            // ── Last Season's Bill ──
            var lastBill = TaxManager.GetLastBill();
            if (lastBill != null)
            {
                Ui.AddChild(SectionHeader($"Last Bill ({lastBill.Season})", y));
                y += (int)(32 * S);

                AddRow("Gross Income:", $"{lastBill.GrossIncome}g", y);
                y += (int)(26 * S);
                AddRow("Income Tax:", $"{lastBill.IncomeTax}g", y, lastBill.IncomeTax > 0 ? Color.Firebrick : Color.DimGray);
                y += (int)(26 * S);
                AddRow("Utility Tax:", $"{lastBill.UtilityTax}g", y, lastBill.UtilityTax > 0 ? Color.DarkOrange : Color.DimGray);
                y += (int)(26 * S);
                AddRow("Property Tax:", $"{lastBill.PropertyTax}g", y, lastBill.PropertyTax > 0 ? Color.DarkOrange : Color.DimGray);
                y += (int)(26 * S);

                // Divider
                Ui.AddChild(new Label()
                {
                    String = "────────────────────────────",
                    LocalPosition = new Vector2((int)(48 * S), y),
                    IdleTextColor = Color.LightGray,
                });
                y += (int)(24 * S);

                AddRow("Total Tax Paid:", $"{lastBill.TotalTax}g", y, Color.DarkRed);
                y += (int)(28 * S);

                if (lastBill.GrossIncome > 0)
                {
                    float rate = (float)lastBill.TotalTax / lastBill.GrossIncome * 100f;
                    Ui.AddChild(new Label()
                    {
                        String = $"Effective Rate: {rate:F1}%",
                        LocalPosition = new Vector2((int)(48 * S), y),
                        IdleTextColor = Color.DimGray,
                    });
                }
            }
            else
            {
                Ui.AddChild(SectionHeader("Last Season's Bill", y));
                y += (int)(32 * S);
                Ui.AddChild(new Label()
                {
                    String = "No previous tax bill on record.",
                    LocalPosition = new Vector2((int)(48 * S), y),
                    IdleTextColor = Color.Gray,
                });
            }
        }

        private void AddRow(string label, string value, float y, Color? valueColor = null)
        {
            Ui.AddChild(new Label()
            {
                String = label,
                LocalPosition = new Vector2((int)(48 * S), y),
                IdleTextColor = Color.DarkSlateGray,
            });
            Ui.AddChild(new Label()
            {
                String = value,
                LocalPosition = new Vector2((int)(380 * Math.Max(1f, S)), y),
                IdleTextColor = valueColor ?? Color.Black,
            });
        }

        private Label SectionHeader(string text, float y)
        {
            return new Label()
            {
                String = $"━━ {text} ━━",
                Bold = true,
                LocalPosition = new Vector2((int)(32 * S), y),
                IdleTextColor = Color.SaddleBrown,
            };
        }

        public override void update(GameTime time)
        {
            base.update(time);
            Ui.Update();
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
