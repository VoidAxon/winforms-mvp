using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples.ToastDemo
{
    /// <summary>
    /// Demonstrates configurable toast notifications and proves they never enter
    /// Application.OpenForms. The toast is a layered Win32 popup (NativeWindow), so — like a
    /// native MessageBox — it is invisible to OpenForms. The live counter stays unchanged before
    /// and after a toast appears, so host code that enumerates OpenForms is never disturbed.
    /// <para>
    /// The controls let you change position (corner), size, font size and duration per call, then
    /// fire toasts to watch them stack, evict (5-toast cap), and truncate long text with an ellipsis.
    /// </para>
    /// </summary>
    public class ToastDemoForm : Form
    {
        private readonly IMessageService _messages = new MessageService();

        private ComboBox _positionCombo;
        private NumericUpDown _fontSizeNum;
        private NumericUpDown _widthNum;
        private NumericUpDown _heightNum;
        private NumericUpDown _durationNum;
        private Label _openFormsLabel;
        private Timer _pollTimer;

        public ToastDemoForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Toast Notification Demo";
            this.Size = new Size(520, 714);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var title = new Label
            {
                Text = "Configure, then fire toasts",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 16),
                Size = new Size(470, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // --- Options panel ---
            int labelX = 24, fieldX = 150, rowH = 34;
            int y = 56;

            _positionCombo = new ComboBox
            {
                Location = new Point(fieldX, y),
                Size = new Size(160, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _positionCombo.Items.AddRange(new object[]
            {
                ToastPosition.TopLeft, ToastPosition.TopRight,
                ToastPosition.BottomLeft, ToastPosition.BottomRight
            });
            _positionCombo.SelectedItem = ToastPosition.BottomRight;
            var positionLabel = MakeLabel("Position:", labelX, y + 3);

            y += rowH;
            _fontSizeNum = MakeNumeric(fieldX, y, 6, 48, 10);
            var fontLabel = MakeLabel("Font size (pt):", labelX, y + 3);

            y += rowH;
            _widthNum = MakeNumeric(fieldX, y, 150, 800, 350);
            var widthLabel = MakeLabel("Width (px):", labelX, y + 3);

            y += rowH;
            _heightNum = MakeNumeric(fieldX, y, 50, 400, 80);
            var heightLabel = MakeLabel("Height (px):", labelX, y + 3);

            y += rowH;
            _durationNum = MakeNumeric(fieldX, y, 500, 20000, 4000);
            _durationNum.Increment = 500;
            var durationLabel = MakeLabel("Duration (ms):", labelX, y + 3);

            // --- Action buttons ---
            y += rowH + 12;
            var infoButton = MakeButton("Info", Color.FromArgb(0, 120, 215), new Point(24, y),
                () => Fire("情報メッセージ / Info toast", ToastType.Info));
            var successButton = MakeButton("Success", Color.FromArgb(16, 137, 62), new Point(150, y),
                () => Fire("保存しました / Saved successfully", ToastType.Success));
            var warningButton = MakeButton("Warning", Color.FromArgb(255, 140, 0), new Point(276, y),
                () => Fire("警告メッセージ / Warning toast", ToastType.Warning));
            var errorButton = MakeButton("Error", Color.FromArgb(232, 17, 35), new Point(402, y),
                () => Fire("エラーが発生しました / Error toast", ToastType.Error));
            SizeButtons(infoButton, successButton, warningButton, errorButton);

            y += 54;
            var longTextButton = MakeButton("Long text (ellipsis test)", Color.FromArgb(96, 96, 96),
                new Point(24, y),
                () => Fire("これは非常に長いメッセージで、トーストの枠に収まりきらないため末尾が省略記号で切り詰められます。" +
                           "This is a very long message that overflows the toast and gets truncated with an ellipsis.",
                    ToastType.Info));
            longTextButton.Size = new Size(220, 44);

            var burstButton = MakeButton("Show several (stack + cap)", Color.FromArgb(0, 150, 199),
                new Point(256, y), ShowBurst);
            burstButton.Size = new Size(232, 44);

            // Anchored (View-layer) toasts: single, point-positioned, auto-pulled on screen.
            y += 54;
            var anchorCursorButton = MakeButton("Anchored at cursor (single)", Color.FromArgb(120, 40, 140),
                new Point(24, y), ShowAnchoredAtCursor);
            anchorCursorButton.Size = new Size(220, 44);

            var anchorClampButton = MakeButton("Anchored off-screen (clamp test)", Color.FromArgb(140, 40, 40),
                new Point(256, y), ShowAnchoredOffScreen);
            anchorClampButton.Size = new Size(232, 44);

            // Custom renderer: owner-draw the toast surface (dark card + accent bar), framework
            // still owns position / stacking / fade.
            y += 54;
            var customRenderButton = MakeButton("Custom renderer (owner-draw)", Color.FromArgb(45, 45, 48),
                new Point(24, y), ShowWithCustomRenderer);
            customRenderButton.Size = new Size(464, 44);

            y += 54;
            var enumerateButton = MakeButton("foreach OpenForms (legacy code)", Color.FromArgb(75, 0, 130),
                new Point(24, y), EnumerateOpenForms);
            enumerateButton.Size = new Size(464, 44);

            y += 60;
            _openFormsLabel = new Label
            {
                Text = "Application.OpenForms.Count: 0",
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                Location = new Point(20, y),
                Size = new Size(470, 26),
                ForeColor = Color.DarkGreen,
                TextAlign = ContentAlignment.MiddleCenter
            };

            y += 28;
            var hint = new Label
            {
                Text = "Corner toasts stack; anchored toasts are single + pulled fully on screen. Count stays 0.",
                Location = new Point(20, y),
                Size = new Size(470, 20),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.AddRange(new Control[]
            {
                title,
                positionLabel, _positionCombo,
                fontLabel, _fontSizeNum,
                widthLabel, _widthNum,
                heightLabel, _heightNum,
                durationLabel, _durationNum,
                infoButton, successButton, warningButton, errorButton,
                longTextButton, burstButton,
                anchorCursorButton, anchorClampButton,
                customRenderButton, enumerateButton,
                _openFormsLabel, hint
            });

            // Poll OpenForms.Count so the user can watch it stay unchanged while toasts appear.
            _pollTimer = new Timer { Interval = 200 };
            _pollTimer.Tick += (s, e) =>
                _openFormsLabel.Text = "Application.OpenForms.Count: " + Application.OpenForms.Count;
            _pollTimer.Start();

            this.FormClosed += (s, e) => _pollTimer.Dispose();
        }

        /// <summary>Builds a <see cref="ToastOptions"/> from the current control values.</summary>
        private ToastOptions CurrentOptions()
        {
            return new ToastOptions
            {
                Position = (ToastPosition)_positionCombo.SelectedItem,
                Size = new Size((int)_widthNum.Value, (int)_heightNum.Value),
                Font = new Font("Segoe UI", (float)_fontSizeNum.Value),
                Duration = (int)_durationNum.Value
            };
        }

        private void Fire(string text, ToastType type)
        {
            _messages.ShowToast(text, type, CurrentOptions());
        }

        // Fires a handful of toasts in one go so the vertical stacking (and the 5-toast cap) is
        // easy to see. They share the current options, so they go to the same corner and overlap.
        private void ShowBurst()
        {
            var types = new[] { ToastType.Info, ToastType.Success, ToastType.Warning, ToastType.Error, ToastType.Info };
            for (int i = 0; i < 6; i++)
            {
                string suffix = i == 5 ? "（最古が押し出される / oldest evicted）" : "";
                _messages.ShowToast((i + 1) + "件目 / Toast #" + (i + 1) + suffix, types[i % types.Length], CurrentOptions());
            }
        }

        // Shows a toast painted by a custom ToastRenderer instead of the built-in look.
        private void ShowWithCustomRenderer()
        {
            var options = CurrentOptions();
            options.Renderer = new CardToastRenderer();
            _messages.ShowToast("カスタム描画 / Owner-drawn toast", ToastType.Success, options);
        }

        /// <summary>
        /// Example custom renderer: a dark rounded card with a colored accent bar on the left and
        /// the message text — a completely different look from <c>DefaultToastRenderer</c>.
        /// </summary>
        private sealed class CardToastRenderer : ToastRenderer
        {
            public override void Render(ToastRenderContext context)
            {
                var g = context.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(45, 45, 48)); // dark card

                Color accent;
                switch (context.Type)
                {
                    case ToastType.Success: accent = Color.FromArgb(76, 209, 130); break;
                    case ToastType.Warning: accent = Color.FromArgb(255, 184, 0); break;
                    case ToastType.Error: accent = Color.FromArgb(255, 99, 99); break;
                    default: accent = Color.FromArgb(86, 156, 255); break;
                }

                using (var accentBrush = new SolidBrush(accent))
                using (var textBrush = new SolidBrush(Color.WhiteSmoke))
                using (var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                })
                {
                    // Left accent bar
                    g.FillRectangle(accentBrush, 0, 0, 6, context.Bounds.Height);
                    // Message
                    g.DrawString(context.Message, context.Font, textBrush,
                        new RectangleF(18, 8, context.Bounds.Width - 28, context.Bounds.Height - 16), format);
                }
            }
        }

        // Anchored, View-layer toast at the current cursor position. Only one exists at a time:
        // clicking again replaces the previous anchored toast. Independent of the corner stack.
        private void ShowAnchoredAtCursor()
        {
            AnchoredToast.Show("カーソル位置に表示 / Anchored at cursor", ToastType.Info, Cursor.Position, CurrentOptions());
        }

        // Passes a wildly off-screen anchor to prove the toast is pulled back fully on screen.
        private void ShowAnchoredOffScreen()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var offScreen = new Point(area.Right + 500, area.Bottom + 500);
            AnchoredToast.Show("画面外を指定 → 引き戻し / Pulled back on screen", ToastType.Warning, offScreen, CurrentOptions());
        }

        // Mimics legacy host code that iterates OpenForms. Before the toast was a NativeWindow,
        // a toast auto-closing mid-iteration would throw "Collection was modified".
        private void EnumerateOpenForms()
        {
            int count = 0;
            foreach (Form f in Application.OpenForms)
            {
                count++;
            }
            Fire("foreach OK — visited " + count + " form(s), no exception", ToastType.Success);
        }

        private Label MakeLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(120, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private NumericUpDown MakeNumeric(int x, int y, int min, int max, int value)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(90, 24),
                Minimum = min,
                Maximum = max,
                Value = value
            };
        }

        private Button MakeButton(string text, Color color, Point location, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(110, 44),
                Font = new Font("Segoe UI", 10f),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => onClick();
            return button;
        }

        private static void SizeButtons(params Button[] buttons)
        {
            foreach (var b in buttons)
            {
                b.Size = new Size(110, 44);
            }
        }
    }
}
