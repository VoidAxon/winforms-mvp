using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Samples.ToDoDemo;
using WinformsMVP.Samples.CheckBoxDemo;
using WinformsMVP.Samples.BulkBindingDemo;
using WinformsMVP.Samples.NavigatorDemo;
using WinformsMVP.Samples.MVPComparisonDemo;
using WinformsMVP.Samples.ExecutionRequestDemo;
using WinformsMVP.Samples.MessageBoxDemo;
using WinformsMVP.Samples.EmailDemo;
using WinformsMVP.Samples.EmailDemo.Services;
using WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased;
using WinformsMVP.Samples.CascadeDemo;
using WinformsMVP.Samples.ToastDemo;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples
{
    /// <summary>
    /// Sample launcher for WinForms MVP demos. Demos are grouped into categories and laid out in a
    /// two-column grid so the whole catalog fits on screen without a tall single column.
    /// </summary>
    public class SampleLauncherForm : Form
    {
        private const int ContentWidth = 720;
        private const int ColumnGutter = 8;

        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300 };

        public SampleLauncherForm()
        {
            InitializeComponent();
        }

        /// <summary>A single launchable demo entry.</summary>
        private sealed class DemoItem
        {
            public readonly string Title;
            public readonly string Description;
            public readonly Color Color;
            public readonly Action Launch;

            public DemoItem(string title, string description, Color color, Action launch)
            {
                Title = title;
                Description = description;
                Color = color;
                Launch = launch;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "WinForms MVP - Sample Launcher";
            this.ClientSize = new Size(ContentWidth + 56, 785);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            // Accent header bar
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(0, 120, 215)
            };
            var titleLabel = new Label
            {
                Text = "WinForms MVP Framework",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 12),
                AutoSize = true
            };
            var subtitleLabel = new Label
            {
                Text = "Select a demo to explore the framework",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(220, 235, 250),
                Location = new Point(26, 44),
                AutoSize = true
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            // Scrollable content area holding the category sections
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(24, 16, 24, 16),
                BackColor = Color.White
            };

            // Add content first, then header, so the docked header stays on top.
            this.Controls.Add(content);
            this.Controls.Add(header);

            AddSection(content, "ViewAction",
                new DemoItem("ToDo List Demo",
                    "Action-driven updates • CanExecute • State management",
                    Color.FromArgb(0, 120, 215), LaunchToDoDemo),
                new DemoItem("CheckBox / RadioButton Demo",
                    "CheckedChanged events • Settings UI • MVP pattern",
                    Color.FromArgb(16, 137, 62), LaunchCheckBoxDemo),
                new DemoItem("Bulk Binding Demo (Survey)",
                    "AddRange • AddByTag • Many RadioButtons",
                    Color.FromArgb(139, 69, 19), LaunchBulkBindingDemo));

            AddSection(content, "Navigation & Windows",
                new DemoItem("WindowNavigator Demo",
                    "Modal • Non-Modal • Parameters • Results",
                    Color.FromArgb(75, 0, 130), LaunchNavigatorDemo),
                new DemoItem("MessageBox Positioning Demo",
                    "Native MessageBox • Windows API Hook • Positioning",
                    Color.FromArgb(255, 140, 0), LaunchMessageBoxDemo),
                new DemoItem("Toast Notification Demo",
                    "Layered popup • Invisible to OpenForms (like MessageBox)",
                    Color.FromArgb(0, 150, 199), LaunchToastDemo));

            AddSection(content, "Architecture",
                new DemoItem("MVP Pattern Comparison",
                    "Passive View vs Supervising Controller",
                    Color.FromArgb(220, 20, 60), LaunchMVPComparisonDemo),
                new DemoItem("ExecutionRequest Pattern",
                    "Legacy Integration • Delayed Execution",
                    Color.FromArgb(128, 0, 128), LaunchExecutionRequestDemo));

            AddSection(content, "Cross-Presenter Communication",
                new DemoItem("Order Mgmt (Service-Based)",
                    "Shared Service Layer • Zero Presenter Coupling",
                    Color.FromArgb(34, 139, 34), LaunchServiceBasedDemo),
                new DemoItem("Order Mgmt (EventAggregator)",
                    "Event Aggregator Pub-Sub • Decoupled Messaging",
                    Color.FromArgb(184, 134, 11), LaunchEventBasedDemo),
                new DemoItem("Cascade (N-level selection)",
                    "N-level cascading selection • SelectionStore • Child presenter wiring",
                    Color.FromArgb(0, 100, 160), LaunchCascadeDemo));

            AddSection(content, "Complete Application",
                new DemoItem("Email Demo (Complete App)",
                    "All Features • ChangeTracker • Navigator • Validation",
                    Color.FromArgb(0, 150, 136), LaunchEmailDemo));
        }

        /// <summary>Builds one category: a heading, a divider line, and a two-column button grid.</summary>
        private void AddSection(FlowLayoutPanel parent, string name, params DemoItem[] items)
        {
            var section = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0, 0, 0, 14)
            };

            var heading = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(64, 64, 64),
                AutoSize = false,
                Size = new Size(ContentWidth, 22),
                Margin = new Padding(0, 0, 0, 2),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var divider = new Panel
            {
                Height = 1,
                Width = ContentWidth,
                BackColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 0, 8)
            };

            var grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            foreach (var item in items)
            {
                grid.Controls.Add(CreateDemoButton(item));
            }

            section.Controls.Add(heading);
            section.Controls.Add(divider);
            section.Controls.Add(grid);
            parent.Controls.Add(section);
        }

        private Button CreateDemoButton(DemoItem item)
        {
            int buttonWidth = (ContentWidth - ColumnGutter) / 2 - 8;
            var button = new Button
            {
                Text = item.Title,
                Size = new Size(buttonWidth, 50),
                Margin = new Padding(4),
                Font = new Font("Segoe UI", 10f),
                BackColor = item.Color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => item.Launch();
            _toolTip.SetToolTip(button, item.Description);
            return button;
        }

        private void LaunchToDoDemo()
        {
            var view = new ToDoDemoForm();
            var presenter = new ToDoDemoPresenter();  // Uses CommonServices.Default

            presenter.AttachView(view);
            presenter.Initialize();  // Must call Initialize!
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchCheckBoxDemo()
        {
            var view = new SettingsDemoForm();
            var presenter = new SettingsDemoPresenter();

            presenter.AttachView(view);
            presenter.Initialize();  // Must call Initialize!
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchBulkBindingDemo()
        {
            var view = new SurveyDemoForm();
            var presenter = new SurveyDemoPresenter();

            presenter.AttachView(view);
            presenter.Initialize();  // Must call Initialize!
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchNavigatorDemo()
        {
            // Automatic assembly scanning - registers all Views in the assembly
            var viewMappingRegister = new ViewMappingRegister();
            int registered = viewMappingRegister.RegisterFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
            System.Diagnostics.Debug.WriteLine($"ViewMappingRegister: Auto-registered {registered} Views from assembly");

            // Configure ServiceLocator with the scanned register so Navigator resolves correctly.
            ServiceLocator.Configure(reg =>
            {
                reg.RegisterInstance<IViewMappingRegister>(viewMappingRegister);
            });

            var view = new NavigatorDemoForm();
            var presenter = new NavigatorDemoPresenter();  // No constructor parameters needed!

            presenter.AttachView(view);
            presenter.Initialize();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchMVPComparisonDemo()
        {
            var launcher = new MVPComparisonLauncher();
            launcher.Show();

            this.Hide();
            launcher.FormClosed += (s, e) => this.Show();
        }

        private void LaunchExecutionRequestDemo()
        {
            var view = new ExecutionRequestDemoForm();
            var presenter = new ExecutionRequestDemoPresenter();

            presenter.AttachView(view);
            presenter.Initialize();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchMessageBoxDemo()
        {
            // View-driven demo: positioning is a View concern, so there is no presenter.
            var view = new MessageBoxDemoForm();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchEmailDemo()
        {
            // Automatic assembly scanning - registers all Views in the assembly
            var viewMappingRegister = new ViewMappingRegister();
            int registered = viewMappingRegister.RegisterFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
            System.Diagnostics.Debug.WriteLine($"ViewMappingRegister: Auto-registered {registered} Views from assembly");

            // Configure ServiceLocator with the scanned register so Navigator resolves correctly.
            ServiceLocator.Configure(reg =>
            {
                reg.RegisterInstance<IViewMappingRegister>(viewMappingRegister);
            });

            // Create repository and main view
            var repository = new InMemoryEmailRepository();
            var view = new MainEmailForm();
            var presenter = new MainEmailPresenter(repository);

            presenter.AttachView(view);
            presenter.Initialize();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchToastDemo()
        {
            var view = new ToastDemoForm();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchServiceBasedDemo()
        {
            this.Hide();
            ComplexInteractionDemoServiceBasedProgram.Run();
            this.Show();
        }

        private void LaunchEventBasedDemo()
        {
            this.Hide();
            ComplexInteractionDemoEventBasedProgram.Run();
            this.Show();
        }

        private void LaunchCascadeDemo()
        {
            this.Hide();
            CascadeDemoProgram.Run();
            this.Show();
        }
    }
}
