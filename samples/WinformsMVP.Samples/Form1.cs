using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Samples.ToDoDemo;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Welcome form
            this.Text = "WinForms MVP - Demo Launcher";
            this.Size = new Size(600, 350);
            this.StartPosition = FormStartPosition.CenterScreen;

            var titleLabel = new Label
            {
                Text = "WinForms MVP Framework Demo",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(550, 40),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var infoLabel = new Label
            {
                Text = "Click the button below to launch the interactive ToDo List demo.\n\n" +
                       "You'll see:\n" +
                       "• ViewAction system with CanExecute support\n" +
                       "• Buttons automatically enable/disable based on state\n" +
                       "• Action-driven updates (automatic after actions)\n" +
                       "• State-driven updates (via RaiseCanExecuteChanged)\n" +
                       "• Complete MVP pattern with dependency injection",
                Location = new Point(30, 70),
                Size = new Size(540, 140),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.TopLeft
            };

            var launchButton = new Button
            {
                Text = "Launch ToDo Demo",
                Location = new Point(200, 230),
                Size = new Size(200, 45),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            launchButton.FlatAppearance.BorderSize = 0;
            launchButton.Click += LaunchToDoDemo;

            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel, launchButton
            });
        }

        private void LaunchToDoDemo(object sender, EventArgs e)
        {
            // Create the demo form
            var demoForm = new ToDoDemoForm();

            // Create presenter - uses CommonServices.Default automatically
            var presenter = new ToDoDemoPresenter();

            // Attach view to presenter
            presenter.AttachView(demoForm);
            presenter.Initialize();

            // Show the demo
            demoForm.ShowDialog(this);
        }
    }
}
