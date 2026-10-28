using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Services;
using Kibinago.Views;

namespace Kibinago.Samples.MessageBoxDemo
{
    /// <summary>
    /// Demonstrates <see cref="AnchoredMessageBox"/> — a native MessageBox shown next to a UI element.
    /// <para>
    /// A MessageBox is centered by default; the only reason to position one is to <em>anchor it to
    /// something the user is looking at</em> — a control or the cursor. So this demo contrasts the
    /// standard centered box with anchored ones. Positioning is a View concern (only the View knows
    /// screen coordinates via <see cref="Control.PointToScreen"/> / <see cref="Cursor.Position"/>),
    /// so the demo is View-driven with no presenter. Anchors near a screen edge are pulled back in
    /// automatically, so the dialog is always fully visible.
    /// </para>
    /// </summary>
    public partial class MessageBoxDemoControl : UserControl, IViewBase   // a view-only demo: no presenter, but a view the shell can host
    {
        public MessageBoxDemoControl()
        {
            InitializeComponent();
        }

        private Button MakeButton(string text, Point location, Color color, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(380, 40),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => onClick();
            return button;
        }
    }
}
