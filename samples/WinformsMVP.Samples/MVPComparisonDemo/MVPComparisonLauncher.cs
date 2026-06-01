using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples.MVPComparisonDemo
{
    /// <summary>
    /// Launcher form to demonstrate the difference between
    /// Passive View and Supervising Controller patterns.
    /// </summary>
    public class MVPComparisonLauncher : Form
    {
        private readonly IServiceProvider _serviceProvider;

        public MVPComparisonLauncher()
        {
            // Setup DI container for demo
            var services = new ServiceCollection();
            services.AddSingleton<IMessageService, MessageService>();
            _serviceProvider = services.BuildServiceProvider();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "MVP Pattern Comparison Demo";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "MVP Pattern Comparison",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                Location = new Point(40, 30),
                Size = new Size(700, 40),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Subtitle
            var subtitleLabel = new Label
            {
                Text = "Compare Passive View vs Supervising Controller patterns",
                Font = new Font("Segoe UI", 11f),
                Location = new Point(40, 75),
                Size = new Size(700, 25),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Passive View Panel
            var passivePanel = new Panel
            {
                Location = new Point(40, 130),
                Size = new Size(340, 360),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 245, 255)
            };

            var passiveTitle = new Label
            {
                Text = "Passive View Pattern",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                ForeColor = Color.DarkBlue
            };

            var passiveDesc = new Label
            {
                Text = "Characteristics:\n\n" +
                       "✓ Presenter has COMPLETE control\n\n" +
                       "✓ View is completely passive\n\n" +
                       "✓ NO data binding\n\n" +
                       "✓ All properties explicitly managed\n\n" +
                       "✓ Maximum testability\n\n" +
                       "✗ More boilerplate code\n\n" +
                       "✗ Manual synchronization",
                Location = new Point(20, 60),
                Size = new Size(300, 220),
                Font = new Font("Segoe UI", 9.5f)
            };

            var passiveButton = new Button
            {
                Text = "Open Passive View Demo",
                Location = new Point(40, 290),
                Size = new Size(260, 40),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            passiveButton.FlatAppearance.BorderSize = 0;
            passiveButton.Click += (s, e) => OpenPassiveViewDemo();

            passivePanel.Controls.AddRange(new Control[] {
                passiveTitle, passiveDesc, passiveButton
            });

            // Supervising Controller Panel
            var supervisingPanel = new Panel
            {
                Location = new Point(420, 130),
                Size = new Size(340, 360),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 255, 245)
            };

            var supervisingTitle = new Label
            {
                Text = "Supervising Controller",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                ForeColor = Color.DarkGreen
            };

            var supervisingDesc = new Label
            {
                Text = "Characteristics:\n\n" +
                       "✓ Uses BindableBase (INotifyPropertyChanged)\n\n" +
                       "✓ Automatic data binding\n\n" +
                       "✓ Less boilerplate code\n\n" +
                       "✓ Automatic UI synchronization\n\n" +
                       "✓ Cleaner for simple scenarios\n\n" +
                       "✗ Some behavior is \"hidden\"\n\n" +
                       "✗ Requires binding understanding",
                Location = new Point(20, 60),
                Size = new Size(300, 220),
                Font = new Font("Segoe UI", 9.5f)
            };

            var supervisingButton = new Button
            {
                Text = "Open Supervising Controller Demo",
                Location = new Point(40, 290),
                Size = new Size(260, 40),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            supervisingButton.FlatAppearance.BorderSize = 0;
            supervisingButton.Click += (s, e) => OpenSupervisingControllerDemo();

            supervisingPanel.Controls.AddRange(new Control[] {
                supervisingTitle, supervisingDesc, supervisingButton
            });

            // Info Label
            var infoLabel = new Label
            {
                Text = "💡 Both demos implement the SAME functionality!\n" +
                       "Try both to see how the patterns differ in implementation.",
                Location = new Point(40, 510),
                Size = new Size(720, 40),
                ForeColor = Color.DarkOrange,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Add all controls
            this.Controls.AddRange(new Control[] {
                titleLabel, subtitleLabel,
                passivePanel, supervisingPanel,
                infoLabel
            });
        }

        private void OpenPassiveViewDemo()
        {
            try
            {
                var presenter = new PassiveView.UserEditorPresenter();
                var view = new PassiveView.UserEditorForm();

                // Manual MVP wiring (in real apps, use WindowNavigator)
                presenter.AttachView(view);
                ((IInitializable)presenter).Initialize();

                view.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Passive View demo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSupervisingControllerDemo()
        {
            try
            {
                var messageService = _serviceProvider.GetRequiredService<IMessageService>();
                var presenter = new SupervisingController.UserEditorPresenter(messageService);
                var view = new SupervisingController.UserEditorForm();

                // Manual MVP wiring (in real apps, use WindowNavigator)
                presenter.AttachView(view);
                ((IInitializable)presenter).Initialize();

                view.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Supervising Controller demo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
