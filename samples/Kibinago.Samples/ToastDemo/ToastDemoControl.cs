using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Services;

namespace Kibinago.Samples.ToastDemo
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
    public partial class ToastDemoControl : UserControl
    {
        private readonly IWinFormsToastService _toasts = ServiceLocator.Current.ResolveRequired<IWinFormsToastService>();

        public ToastDemoControl()
        {
            InitializeComponent();
        }

        /// <summary>Builds a <see cref="ToastOptions"/> from the current control values.</summary>
        private ToastOptions CurrentOptions()
        {
            return new ToastOptions
            {
                Position = (ToastPosition)_positionCombo.SelectedItem,
                Size = new Size((int)_widthNum.Value, (int)_heightNum.Value),
                Font = new Font("Segoe UI", (float)_fontSizeNum.Value),
                Duration = (int)_durationNum.Value,
                Style = (ToastStyle)_styleCombo.SelectedItem,
                ShowCloseButton = _showCloseCheck.Checked,
                AutoHeight = _autoHeightCheck.Checked
            };
        }

        private void Fire(string text, ToastType type)
        {
            _toasts.Show(text, type, CurrentOptions());
        }

        // Fires a handful of toasts in one go so the vertical stacking (and the 5-toast cap) is
        // easy to see. They share the current options, so they go to the same corner and overlap.
        private void ShowBurst()
        {
            var types = new[] { ToastType.Info, ToastType.Success, ToastType.Warning, ToastType.Error, ToastType.Info };
            for (int i = 0; i < 6; i++)
            {
                string suffix = i == 5 ? "（最古が押し出される / oldest evicted）" : "";
                _toasts.Show((i + 1) + "件目 / Toast #" + (i + 1) + suffix, types[i % types.Length], CurrentOptions());
            }
        }

        // Shows a toast painted by a custom ToastRenderer instead of the built-in look.
        private void ShowWithCustomRenderer()
        {
            var options = CurrentOptions();
            // An explicit Renderer wins over Style (see ToastRendererResolver), so the style
            // combo has no effect on this button.
            options.Renderer = new DarkCardToastRenderer();
            _toasts.Show("カスタム描画 / Owner-drawn toast", ToastType.Success, options);
        }

        /// <summary>
        /// Example custom renderer: a dark rounded card with a colored accent bar on the left and
        /// the message text — a completely different look from <c>DefaultToastRenderer</c>.
        /// </summary>
        private sealed class DarkCardToastRenderer : ToastRenderer
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
