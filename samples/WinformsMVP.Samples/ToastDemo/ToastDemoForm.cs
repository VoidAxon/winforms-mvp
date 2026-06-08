using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples.ToastDemo
{
    /// <summary>
    /// Demonstrates toast notifications and proves they never enter Application.OpenForms.
    /// The toast is a layered Win32 popup (NativeWindow), so — like a native MessageBox — it is
    /// invisible to OpenForms. The live counter below stays at the same value before and after a
    /// toast appears, so host code that enumerates OpenForms is never disturbed.
    /// </summary>
    public class ToastDemoForm : Form
    {
        private readonly IMessageService _messages = new MessageService();
        private Label _openFormsLabel;
        private Timer _pollTimer;

        public ToastDemoForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Toast Notification Demo";
            this.Size = new Size(460, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var title = new Label
            {
                Text = "Click a button to show a toast",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(420, 28),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var infoButton = MakeButton("Info", Color.FromArgb(0, 120, 215), new Point(40, 70),
                () => _messages.ShowToast("情報メッセージ / Info toast", ToastType.Info));
            var successButton = MakeButton("Success", Color.FromArgb(16, 137, 62), new Point(240, 70),
                () => _messages.ShowToast("保存しました / Saved successfully", ToastType.Success));
            var warningButton = MakeButton("Warning", Color.FromArgb(255, 140, 0), new Point(40, 130),
                () => _messages.ShowToast("警告メッセージ / Warning toast", ToastType.Warning));
            var errorButton = MakeButton("Error", Color.FromArgb(232, 17, 35), new Point(240, 130),
                () => _messages.ShowToast("エラーが発生しました / Error toast", ToastType.Error));

            var enumerateButton = MakeButton("foreach OpenForms (legacy code)", Color.FromArgb(75, 0, 130),
                new Point(40, 200), EnumerateOpenForms);
            enumerateButton.Size = new Size(380, 44);

            _openFormsLabel = new Label
            {
                Text = "Application.OpenForms.Count: 0",
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                Location = new Point(20, 270),
                Size = new Size(420, 28),
                ForeColor = Color.DarkGreen,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var hint = new Label
            {
                Text = "Note: the count stays 0 even while a toast is on screen.",
                Location = new Point(20, 300),
                Size = new Size(420, 20),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.AddRange(new Control[]
            {
                title, infoButton, successButton, warningButton, errorButton,
                enumerateButton, _openFormsLabel, hint
            });

            // Poll OpenForms.Count so the user can watch it stay unchanged while toasts appear.
            _pollTimer = new Timer { Interval = 200 };
            _pollTimer.Tick += (s, e) =>
                _openFormsLabel.Text = "Application.OpenForms.Count: " + Application.OpenForms.Count;
            _pollTimer.Start();

            this.FormClosed += (s, e) => _pollTimer.Dispose();
        }

        private Button MakeButton(string text, Color color, Point location, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(180, 44),
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

        // Mimics legacy host code that iterates OpenForms. Before the toast was a NativeWindow,
        // a toast auto-closing mid-iteration would throw "Collection was modified".
        private void EnumerateOpenForms()
        {
            int count = 0;
            foreach (Form f in Application.OpenForms)
            {
                count++;
            }
            _messages.ShowToast("foreach OK — visited " + count + " form(s), no exception", ToastType.Success);
        }
    }
}
