using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples.MessageBoxDemo
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
    public class MessageBoxDemoForm : Form
    {
        public MessageBoxDemoForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "MessageBox Positioning Demo - Native Windows API";
            this.Size = new Size(560, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var titleLabel = new Label
            {
                Text = "Native MessageBox: centered vs. anchored",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(500, 30),
                ForeColor = Color.DarkBlue
            };

            var infoLabel = new Label
            {
                Text = "A MessageBox is centered by default. Anchor one (to a control or the cursor)\n" +
                       "only when it should point at what the user is looking at.\n\n" +
                       "✓ Native appearance, system icons, Dark Mode, keyboard shortcuts, sounds\n" +
                       "✓ Anchored boxes near a screen edge are pulled fully on screen\n" +
                       "✓ Never appears in Application.OpenForms (it is not a Form)",
                Location = new Point(20, 58),
                Size = new Size(500, 110),
                ForeColor = Color.DarkGray
            };

            var btnCentered = MakeButton("Show Centered (Standard / default)", new Point(30, 185), Color.Gray,
                () => MessageBox.Show(this,
                    "A STANDARD centered MessageBox — the normal case, no positioning.",
                    "Standard Centered", MessageBoxButtons.OK, MessageBoxIcon.Information));

            var btnAtCursor = MakeButton("Show at Mouse Cursor", new Point(30, 235), Color.FromArgb(111, 66, 193),
                () => AnchoredMessageBox.ShowInfo(
                    "Anchored at the current cursor position.", Cursor.Position, "At Cursor"));

            // The realistic case: anchor the box to one of this form's controls so it points at it.
            Button btnBelow = null;
            btnBelow = MakeButton("Show Below This Button", new Point(30, 285), Color.FromArgb(0, 120, 215),
                () => AnchoredMessageBox.ShowInfo(
                    "Anchored just below this button via PointToScreen — the typical reason to position.",
                    btnBelow.PointToScreen(new Point(0, btnBelow.Height + 4)),
                    "Anchored to a control"));

            Button btnConfirmBelow = null;
            btnConfirmBelow = MakeButton("Confirm Below This Button (Yes/No)", new Point(30, 335),
                Color.FromArgb(23, 162, 184),
                () =>
                {
                    var anchor = btnConfirmBelow.PointToScreen(new Point(0, btnConfirmBelow.Height + 4));
                    bool yes = AnchoredMessageBox.ConfirmYesNo("Proceed?  (try the Y / N keys)", anchor, "Confirm");
                    AnchoredMessageBox.ShowInfo(yes ? "You clicked YES ✓" : "You clicked NO ✗", anchor, "Result");
                });

            this.Controls.AddRange(new Control[]
            {
                titleLabel, infoLabel,
                btnCentered, btnAtCursor, btnBelow, btnConfirmBelow
            });
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
