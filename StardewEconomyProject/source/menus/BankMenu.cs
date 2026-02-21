using System;
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
    /// Banking menu — Deposit / Withdraw / Loans / Fixed-term savings.
    /// </summary>
    public class BankMenu : IClickableMenu
    {
        private static float S => Math.Max(0.5f, Math.Min(3.0f, ModConfig.GetInstance().UiSpacingScale));
        private static int MenuWidth  => Math.Min(Game1.uiViewport.Width  - 64, (int)(700 * Math.Max(1f, S)));
        private static int MenuHeight => Math.Min(Game1.uiViewport.Height - 64, (int)(680 * S));

        private RootElement Ui;
        private Textbox AmountBox;

        // Parsed value from AmountBox
        private int AmountValue => int.TryParse(AmountBox?.String, out int v) ? Math.Max(0, v) : 0;

        // Fixed-term period selection
        private int _selectedSeasons = 1;
        private Label[] _periodButtons;
        private Label PeriodsPreviewLabel;

        // Status labels to refresh
        private Label SavingsLabel;
        private Label LoanLabel;
        private Label InterestLabel;
        private Label FixedTermLabel;
        private Label WalletLabel;
        private Label FeedbackLabel;

        public BankMenu()
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
                String = "Stardew Valley Bank",
                Bold = true,
                LocalPosition = new Vector2(MenuWidth / 2 - 130, y),
                IdleTextColor = Color.DarkGoldenrod,
            });
            y += (int)(48 * S);

            // ── Account Summary ──
            Ui.AddChild(SectionHeader("Account Summary", y));
            y += (int)(32 * S);

            WalletLabel = new Label()
            {
                String = $"Wallet: {Game1.player.Money}g",
                LocalPosition = new Vector2(32, y),
                IdleTextColor = Color.DarkSlateGray,
            };
            Ui.AddChild(WalletLabel);

            SavingsLabel = new Label()
            {
                String = $"Savings: {BankManager.GetSavingsBalance()}g",
                LocalPosition = new Vector2((int)(320 * Math.Max(1f, S)), y),
                IdleTextColor = Color.ForestGreen,
            };
            Ui.AddChild(SavingsLabel);
            y += (int)(28 * S);

            InterestLabel = new Label()
            {
                String = $"Total Interest Earned: {BankManager.GetTotalInterestEarned()}g",
                LocalPosition = new Vector2(32, y),
                IdleTextColor = Color.DimGray,
            };
            Ui.AddChild(InterestLabel);

            FixedTermLabel = new Label()
            {
                String = GetFixedTermText(),
                LocalPosition = new Vector2((int)(320 * Math.Max(1f, S)), y),
                IdleTextColor = BankManager.IsFixedTerm() ? Color.DarkOrange : Color.DimGray,
            };
            Ui.AddChild(FixedTermLabel);
            y += (int)(28 * S);

            LoanLabel = new Label()
            {
                String = GetLoanText(),
                LocalPosition = new Vector2(32, y),
                IdleTextColor = BankManager.GetLoanBalance() > 0 ? Color.Firebrick : Color.DimGray,
            };
            Ui.AddChild(LoanLabel);
            y += (int)(48 * S);

            // ── Amount Input ──
            Ui.AddChild(new Label()
            {
                String = "Amount:",
                Bold = true,
                LocalPosition = new Vector2(32, y + 4),
                IdleTextColor = Color.Black,
            });

            AmountBox = new Textbox()
            {
                LocalPosition = new Vector2(140, y),
                String = "",
            };
            AmountBox.Selected = true;  // auto-focus so player can type immediately
            Ui.AddChild(AmountBox);
            y += (int)(48 * S);

            // ── Action Buttons ──
            float btnX = 32;
            float btnSpacing = (int)(155 * Math.Max(1f, S));

            // Deposit
            Ui.AddChild(MakeButton("Deposit", btnX, y, Color.ForestGreen, () =>
            {
                int amt = AmountValue;
                if (amt <= 0) { ShowFeedback("Enter a positive amount.", Color.Red); return; }
                bool ok = BankManager.Deposit(amt);
                if (ok)
                {
                    Game1.playSound("purchase");
                    ShowFeedback($"Deposited {amt}g.", Color.ForestGreen);
                }
                else ShowFeedback("Not enough gold in wallet.", Color.Red);
                RefreshLabels();
            }));

            // Withdraw
            Ui.AddChild(MakeButton("Withdraw", btnX + btnSpacing, y, Color.DarkOrange, () =>
            {
                int amt = AmountValue;
                if (amt <= 0) { ShowFeedback("Enter a positive amount.", Color.Red); return; }
                bool ok = BankManager.Withdraw(amt);
                if (ok)
                {
                    Game1.playSound("coin");
                    ShowFeedback($"Withdrew {amt}g.", Color.ForestGreen);
                }
                else ShowFeedback("Insufficient savings or fixed-term active.", Color.Red);
                RefreshLabels();
            }));

            // Take Loan
            Ui.AddChild(MakeButton("Take Loan", btnX + btnSpacing * 2, y, Color.Firebrick, () =>
            {
                int amt = AmountValue;
                if (amt <= 0) { ShowFeedback("Enter a positive amount.", Color.Red); return; }
                bool ok = BankManager.TakeLoan(amt);
                if (ok)
                {
                    Game1.playSound("purchase");
                    ShowFeedback($"Loan of {amt}g approved!", Color.ForestGreen);
                }
                else ShowFeedback("Existing loan outstanding or amount too high.", Color.Red);
                RefreshLabels();
            }));

            // Repay Loan
            Ui.AddChild(MakeButton("Repay Loan", btnX + btnSpacing * 3, y, Color.RoyalBlue, () =>
            {
                int amt = AmountValue;
                if (amt <= 0) { ShowFeedback("Enter a positive amount.", Color.Red); return; }
                bool ok = BankManager.RepayLoan(amt);
                if (ok)
                {
                    Game1.playSound("coin");
                    ShowFeedback($"Repaid {amt}g toward loan.", Color.ForestGreen);
                }
                else ShowFeedback("No active loan or not enough gold.", Color.Red);
                RefreshLabels();
            }));

            y += (int)(48 * S);

            // ── Fixed-Term Deposit ──
            Ui.AddChild(SectionHeader("Fixed-Term Deposit", y));
            y += (int)(30 * S);

            Ui.AddChild(new Label()
            {
                String = "Lock-in:",
                Bold = true,
                LocalPosition = new Vector2(32, y + 4),
                IdleTextColor = Color.Black,
            });

            // Clickable season selector buttons: 1s / 2s / 4s / 8s / 12s
            int[] seasonOptions = { 1, 2, 4, 8, 12 };
            float seasonBtnX     = (int)(130 * Math.Max(1f, S));
            float seasonBtnSpacing = (int)(90 * Math.Max(1f, S));
            _periodButtons = new Label[seasonOptions.Length];
            for (int i = 0; i < seasonOptions.Length; i++)
            {
                int s = seasonOptions[i];
                var btn = new Label()
                {
                    String = $"[{s}s]",
                    Bold = true,
                    LocalPosition = new Vector2(seasonBtnX + i * seasonBtnSpacing, y),
                    IdleTextColor = (s == _selectedSeasons) ? Color.Teal : Color.Gray,
                    HoverTextColor = Color.LightSeaGreen,
                    Callback = _ => { _selectedSeasons = s; RefreshPeriodButtons(); },
                };
                _periodButtons[i] = btn;
                Ui.AddChild(btn);
            }
            y += (int)(38 * S);

            PeriodsPreviewLabel = new Label()
            {
                String = GetPeriodsPreview(_selectedSeasons),
                LocalPosition = new Vector2(32, y),
                IdleTextColor = Color.Teal,
            };
            Ui.AddChild(PeriodsPreviewLabel);
            y += (int)(34 * S);

            Ui.AddChild(MakeButton("Start Fixed Term", btnX, y, Color.Teal, () =>
            {
                bool ok = BankManager.StartFixedTerm(_selectedSeasons);
                if (ok)
                {
                    Game1.playSound("newArtifact");
                    ShowFeedback($"Fixed-term locked for {_selectedSeasons} season(s).", Color.ForestGreen);
                }
                else ShowFeedback("No savings or fixed-term already active.", Color.Red);
                RefreshLabels();
            }));

            y += (int)(52 * S);

            // ── Feedback ──
            FeedbackLabel = new Label()
            {
                String = "",
                LocalPosition = new Vector2(32, y),
                IdleTextColor = Color.DimGray,
            };
            Ui.AddChild(FeedbackLabel);
        }

        private void RefreshLabels()
        {
            WalletLabel.String = $"Wallet: {Game1.player.Money}g";
            SavingsLabel.String = $"Savings: {BankManager.GetSavingsBalance()}g";
            InterestLabel.String = $"Total Interest Earned: {BankManager.GetTotalInterestEarned()}g";
            FixedTermLabel.String = GetFixedTermText();
            FixedTermLabel.IdleTextColor = BankManager.IsFixedTerm() ? Color.DarkOrange : Color.DimGray;
            LoanLabel.String = GetLoanText();
            LoanLabel.IdleTextColor = BankManager.GetLoanBalance() > 0 ? Color.Firebrick : Color.DimGray;
        }

        private void ShowFeedback(string msg, Color color)
        {
            FeedbackLabel.String = msg;
            FeedbackLabel.IdleTextColor = color;
        }

        private string GetFixedTermText()
        {
            if (BankManager.IsFixedTerm())
            {
                int daysLeft = BankManager.GetFixedTermDaysRemaining();
                int seasonsLeft = (daysLeft + 27) / 28;
                return $"Fixed Term: {daysLeft}d left (~{seasonsLeft}s)  ·  {BankManager.GetFixedTermRateMultiplier():F1}x rate";
            }
            return "No fixed-term active";
        }

        private string GetLoanText()
        {
            int loan = BankManager.GetLoanBalance();
            if (loan > 0)
            {
                int payment = BankManager.GetSeasonalPayment();
                return $"Loan: {loan}g  |  Seasonal payment: {payment}g";
            }
            return "No outstanding loan";
        }

        private Label SectionHeader(string text, float y)
        {
            return new Label()
            {
                String = $"━━ {text} ━━",
                Bold = true,
                LocalPosition = new Vector2(32, y),
                IdleTextColor = Color.SaddleBrown,
            };
        }

        private Label MakeButton(string text, float x, float y, Color color, Action onClick)
        {
            return new Label()
            {
                String = $"[{text}]",
                Bold = true,
                LocalPosition = new Vector2(x, y),
                IdleTextColor = color,
                HoverTextColor = Color.White,
                Callback = _ => onClick(),
            };
        }

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            // Only close on Escape/menu button. Do NOT call base for other keys so the
            // textbox can capture digits without the game swallowing them.
            if (Game1.options.menuButton.Any(b => b.key == key) && readyToClose())
                exitThisMenu();
        }

        // ── Period selection helpers ──
        private static readonly int[] SeasonOptions = { 1, 2, 4, 8, 12 };

        private void RefreshPeriodButtons()
        {
            if (_periodButtons == null) return;
            for (int i = 0; i < _periodButtons.Length && i < SeasonOptions.Length; i++)
                _periodButtons[i].IdleTextColor = (SeasonOptions[i] == _selectedSeasons) ? Color.Teal : Color.Gray;
        }

        private string GetPeriodsPreview(int seasons)
        {
            var cfg = ModConfig.GetInstance();
            int days        = seasons * 28;
            double mult     = BankManager.GetFixedTermRateForSeasons(seasons);
            double daily    = cfg.BaseInterestRate * mult;
            double totalPct = Math.Pow(1.0 + daily, days) - 1.0;
            int savings     = BankManager.GetSavingsBalance();
            string est      = savings > 0 ? $"  ·  est. +{(int)(savings * totalPct):N0}g" : "";
            return $"{seasons} season(s) = {days} days  ·  {mult:F1}x rate  ·  ~{totalPct:P1} total{est}";
        }

        public override void update(GameTime time)
        {
            base.update(time);
            Ui.Update();

            // Strip any non-digit characters that may have been typed
            if (AmountBox != null)
            {
                string raw = AmountBox.String ?? "";
                string digitsOnly = System.Text.RegularExpressions.Regex.Replace(raw, @"[^0-9]", "");
                if (digitsOnly != raw)
                    AmountBox.String = digitsOnly;
            }

            // Refresh the period preview (cheap string compare each frame)
            if (PeriodsPreviewLabel != null)
            {
                string preview = GetPeriodsPreview(_selectedSeasons);
                if (PeriodsPreviewLabel.String != preview)
                    PeriodsPreviewLabel.String = preview;
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
