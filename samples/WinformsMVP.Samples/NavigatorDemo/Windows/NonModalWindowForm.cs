using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class NonModalWindowForm : Form, INonModalWindowView
    {
        private Label _counterLabel;
        private Button _incrementButton;
        private Button _closeButton;
        private ViewActionBinder _binder;

        public NonModalWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Non-Modal Window";
            this.Size = new Size(300, 200);
            this.StartPosition = FormStartPosition.CenterScreen;

            var infoLabel = new Label
            {
                Text = "This is a non-modal window.\nYou can still use the main window!",
                Location = new Point(20, 20),
                Size = new Size(240, 40),
                TextAlign = ContentAlignment.TopCenter
            };

            _counterLabel = new Label
            {
                Text = "Counter: 0",
                Location = new Point(20, 70),
                Size = new Size(240, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold)
            };

            _incrementButton = new Button
            {
                Text = "Increment",
                Location = new Point(60, 110),
                Size = new Size(80, 30)
            };

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(150, 110),
                Size = new Size(80, 30)
            };

            this.Controls.AddRange(new Control[] {
                infoLabel, _counterLabel, _incrementButton, _closeButton
            });
        }

        public void SetCounter(int value)
        {
            _counterLabel.Text = $"Counter: {value}";
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (NonModalWindowActions.Increment, _incrementButton),
                (NonModalWindowActions.Close, _closeButton)
            );
        }

    }
}
