using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MessageBoxDemo
{
    public partial class MessageBoxDemoForm : Form, IMessageBoxDemoView
    {
        private Button _btnTopLeft;
        private Button _btnTopRight;
        private Button _btnBottomLeft;
        private Button _btnBottomRight;
        private Button _btnAtMouse;
        private Button _btnCentered;
        private Button _btnConfirmAtMouse;
        private Label _titleLabel;
        private Label _infoLabel;
        private ViewActionBinder _binder;

        public MessageBoxDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "MessageBox Positioning Demo - Native Windows API";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Title
            _titleLabel = new Label
            {
                Text = "Native MessageBox Positioning Demo",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(640, 30),
                ForeColor = Color.DarkBlue
            };

            // Info
            _infoLabel = new Label
            {
                Text = "Click buttons below to see MessageBox appear at different positions.\n" +
                       "All MessageBoxes use NATIVE Windows MessageBox with CBT Hook positioning.\n\n" +
                       "Features:\n" +
                       "✓ Native Windows appearance (not a custom Form!)\n" +
                       "✓ System icons and theme support (including Dark Mode)\n" +
                       "✓ Keyboard shortcuts work automatically (Enter, Esc, Y/N, etc.)\n" +
                       "✓ System sounds",
                Location = new Point(20, 60),
                Size = new Size(640, 120),
                ForeColor = Color.DarkGray
            };

            // Buttons - Positioned
            int buttonWidth = 200;
            int buttonHeight = 40;
            int col1X = 50;
            int col2X = 250;
            int row1Y = 200;
            int row2Y = 250;
            int row3Y = 300;

            _btnTopLeft = new Button
            {
                Text = "Show at Top-Left",
                Location = new Point(col1X, row1Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnTopLeft.FlatAppearance.BorderSize = 0;

            _btnTopRight = new Button
            {
                Text = "Show at Top-Right",
                Location = new Point(col2X, row1Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(255, 140, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnTopRight.FlatAppearance.BorderSize = 0;

            _btnBottomLeft = new Button
            {
                Text = "Show at Bottom-Left",
                Location = new Point(col1X, row2Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBottomLeft.FlatAppearance.BorderSize = 0;

            _btnBottomRight = new Button
            {
                Text = "Show at Bottom-Right",
                Location = new Point(col2X, row2Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBottomRight.FlatAppearance.BorderSize = 0;

            _btnAtMouse = new Button
            {
                Text = "Show at Mouse Cursor",
                Location = new Point(col1X, row3Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(111, 66, 193),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnAtMouse.FlatAppearance.BorderSize = 0;

            _btnCentered = new Button
            {
                Text = "Show Centered (Standard)",
                Location = new Point(col2X, row3Y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnCentered.FlatAppearance.BorderSize = 0;

            _btnConfirmAtMouse = new Button
            {
                Text = "Confirm at Mouse (Yes/No)",
                Location = new Point(col1X, row3Y + 50),
                Size = new Size(400, buttonHeight),
                BackColor = Color.FromArgb(23, 162, 184),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnConfirmAtMouse.FlatAppearance.BorderSize = 0;

            // Add controls
            this.Controls.AddRange(new Control[] {
                _titleLabel,
                _infoLabel,
                _btnTopLeft,
                _btnTopRight,
                _btnBottomLeft,
                _btnBottomRight,
                _btnAtMouse,
                _btnCentered,
                _btnConfirmAtMouse
            });
        }

        #region IMessageBoxDemoView Implementation

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            _binder.Add(MessageBoxDemoActions.ShowAtTopLeft, _btnTopLeft);
            _binder.Add(MessageBoxDemoActions.ShowAtTopRight, _btnTopRight);
            _binder.Add(MessageBoxDemoActions.ShowAtBottomLeft, _btnBottomLeft);
            _binder.Add(MessageBoxDemoActions.ShowAtBottomRight, _btnBottomRight);
            _binder.Add(MessageBoxDemoActions.ShowAtMouse, _btnAtMouse);
            _binder.Add(MessageBoxDemoActions.ShowCentered, _btnCentered);
            _binder.Add(MessageBoxDemoActions.ShowConfirmAtMouse, _btnConfirmAtMouse);
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        #endregion
    }
}
