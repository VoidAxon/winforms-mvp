using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Kibinago.Presenters;
using Kibinago.Services;

namespace Kibinago.Samples.MVPComparisonDemo
{
    /// <summary>
    /// Launcher form to demonstrate the difference between
    /// Passive View and Supervising Controller patterns.
    /// </summary>
    public partial class MVPComparisonLauncher : Form
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

        private void OpenPassiveViewDemo()
        {
            try
            {
                var presenter = new PassiveView.UserEditorPresenter();
                var view = new PassiveView.UserEditorForm();

                // Adopted hosting: Connect does attach + initialize + close wiring, so the
                // presenter's RequestClose closes this form (in real apps, use WindowNavigator)
                presenter.Connect(view);

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

                // Adopted hosting: Connect does attach + initialize + close wiring, so the
                // presenter's RequestClose closes this form (in real apps, use WindowNavigator)
                presenter.Connect(view);

                view.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Supervising Controller demo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
