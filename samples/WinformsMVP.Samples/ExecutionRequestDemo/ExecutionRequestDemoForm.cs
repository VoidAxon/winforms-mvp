using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ExecutionRequestDemo
{
    /// <summary>
    /// ExecutionRequest pattern demonstration form
    /// </summary>
    public partial class ExecutionRequestDemoForm : Form, IExecutionRequestDemoView
    {
        // UI Controls
        private Button _openLegacyButton;
        private Button _selectFileButton;
        private Button _saveDataButton;
        private Label _customerInfoLabel;
        private Label _filePathLabel;
        private Label _statusLabel;
        private Panel _statusPanel;

        private ViewActionBinder _viewActionBinder;

        // ExecutionRequest events (follows three iron rules: use only business data types)
        public event EventHandler<ExecutionRequestEventArgs<CustomerData, CustomerData>>
            EditCustomerRequested;

        public event EventHandler<ExecutionRequestEventArgs<CustomerData, bool>>
            SaveDataRequested;

        public ExecutionRequestDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "ExecutionRequest Pattern Demo";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "ExecutionRequest Pattern Demo",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Location = new Point(30, 20),
                Size = new Size(640, 35),
                ForeColor = Color.DarkBlue
            };

            var descLabel = new Label
            {
                Text = "Demonstrates how to use ExecutionRequest pattern to integrate legacy code and handle special logic",
                Location = new Point(30, 60),
                Size = new Size(640, 25),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 10f)
            };

            // Scenario 1: Edit customer information (ExecutionRequest)
            var scenario1Label = new Label
            {
                Text = "Scenario 1: Edit Customer Information (ExecutionRequest Pattern)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(30, 110),
                Size = new Size(640, 25),
                ForeColor = Color.DarkGreen
            };

            var scenario1DescLabel = new Label
            {
                Text = "✅ Correct Usage: Parameters and return values are business data (CustomerData), no UI types",
                Location = new Point(30, 135),
                Size = new Size(640, 20),
                ForeColor = Color.DarkGreen
            };

            _openLegacyButton = new Button
            {
                Text = "Edit Customer Info",
                Location = new Point(30, 165),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f)
            };
            _openLegacyButton.FlatAppearance.BorderSize = 0;

            _customerInfoLabel = new Label
            {
                Text = "Customer Info: (Not Set)",
                Location = new Point(250, 170),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.Gray
            };

            // Scenario 2: File selection dialog
            var scenario2Label = new Label
            {
                Text = "Scenario 2: File Selection Dialog (Recommended: Service Interface)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(30, 230),
                Size = new Size(640, 25),
                ForeColor = Color.DarkGreen
            };

            var scenario2DescLabel = new Label
            {
                Text = "✅ Recommended Approach: Use IDialogProvider service interface (simpler and easier to test)",
                Location = new Point(30, 255),
                Size = new Size(640, 20),
                ForeColor = Color.DarkGreen
            };

            _selectFileButton = new Button
            {
                Text = "Select File",
                Location = new Point(30, 285),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f)
            };
            _selectFileButton.FlatAppearance.BorderSize = 0;

            _filePathLabel = new Label
            {
                Text = "Selected File: (None)",
                Location = new Point(250, 290),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.Gray,
                AutoEllipsis = true
            };

            // Scenario 3: Save data
            var scenario3Label = new Label
            {
                Text = "Scenario 3: Save Business Data (ExecutionRequest Pattern)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(30, 350),
                Size = new Size(640, 25),
                ForeColor = Color.DarkGreen
            };

            var scenario3DescLabel = new Label
            {
                Text = "✅ Correct Usage: Parameter is business data (CustomerData), return value is business result (bool)",
                Location = new Point(30, 375),
                Size = new Size(640, 20),
                ForeColor = Color.DarkGreen
            };

            _saveDataButton = new Button
            {
                Text = "Save Customer Data",
                Location = new Point(30, 405),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(139, 69, 19),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                Enabled = false  // Initially disabled, enabled after data is available
            };
            _saveDataButton.FlatAppearance.BorderSize = 0;

            var saveHintLabel = new Label
            {
                Text = "💡 Hint: First open the legacy form to add customer data, then save",
                Location = new Point(250, 410),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Orange
            };

            // Status bar
            _statusPanel = new Panel
            {
                Location = new Point(0, 480),
                Size = new Size(700, 70),
                BackColor = Color.FromArgb(240, 240, 240),
                Dock = DockStyle.Bottom
            };

            var statusTitleLabel = new Label
            {
                Text = "Status:",
                Location = new Point(20, 15),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(70, 15),
                Size = new Size(600, 40),
                ForeColor = Color.FromArgb(64, 64, 64),
                Font = new Font("Segoe UI", 9f)
            };

            _statusPanel.Controls.AddRange(new Control[] {
                statusTitleLabel, _statusLabel
            });

            // Add all controls
            this.Controls.AddRange(new Control[] {
                titleLabel, descLabel,
                scenario1Label, scenario1DescLabel, _openLegacyButton, _customerInfoLabel,
                scenario2Label, scenario2DescLabel, _selectFileButton, _filePathLabel,
                scenario3Label, scenario3DescLabel, _saveDataButton, saveHintLabel,
                _statusPanel
            });
        }

        #region IExecutionRequestDemoView Implementation

        public void ShowCustomerInfo(CustomerData data)
        {
            _customerInfoLabel.Text = $"Customer Info: {data}";
            _customerInfoLabel.ForeColor = Color.DarkGreen;
        }

        public void ShowSelectedFile(string filePath)
        {
            _filePathLabel.Text = $"Selected File: {filePath}";
            _filePathLabel.ForeColor = Color.DarkGreen;
        }

        public void UpdateStatus(string message, bool isSuccess)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isSuccess
                ? Color.Green
                : Color.Red;
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(ExecutionRequestDemoActions.EditCustomer, _openLegacyButton);
            _viewActionBinder.Add(ExecutionRequestDemoActions.SelectFile, _selectFileButton);  // Directly triggers ViewAction, Presenter uses IDialogProvider
            _viewActionBinder.Add(ExecutionRequestDemoActions.SaveData, _saveDataButton);

            // Scenario 1 and 3 use ExecutionRequest
            // Scenario 2 uses ViewAction + IDialogProvider directly
            _openLegacyButton.Click += OnEditCustomerButtonClick;
            _saveDataButton.Click += OnSaveDataButtonClick;
        }

        #endregion

        #region Event Handlers - Trigger ExecutionRequest

        /// <summary>
        /// Scenario 1: Edit customer information
        /// ✅ Follows the iron rule: Only passes business data (CustomerData), not UI types
        /// </summary>
        private void OnEditCustomerButtonClick(object sender, EventArgs e)
        {
            // Get current customer data (null means new)
            var currentCustomer = GetCurrentCustomerData();

            // Create ExecutionRequest - View only passes business data and callback
            var request = new ExecutionRequestEventArgs<CustomerData, CustomerData>(
                param: currentCustomer,         // ✅ Business data
                callback: OnCustomerEdited      // ✅ Callback function
            );

            // Trigger event
            EditCustomerRequested?.Invoke(this, request);
        }

        // ✅ Scenario 2: File selection - Does not use ExecutionRequest
        // _selectFileButton is directly bound to ViewAction, Presenter uses IDialogProvider

        /// <summary>
        /// Scenario 3: Save data
        /// ✅ Follows the iron rule: Parameters and return values are business data types
        /// </summary>
        private void OnSaveDataButtonClick(object sender, EventArgs e)
        {
            // Get current customer data
            var customerData = GetCurrentCustomerData();
            if (customerData == null)
            {
                UpdateStatus("No data to save", false);
                return;
            }

            // Create ExecutionRequest
            var request = new ExecutionRequestEventArgs<CustomerData, bool>(
                param: customerData,    // ✅ Business data
                callback: OnDataSaved   // ✅ Callback function
            );

            SaveDataRequested?.Invoke(this, request);
        }

        #endregion

        #region Callbacks - Handle ExecutionRequest results

        /// <summary>
        /// Callback for customer editing completion
        /// </summary>
        private void OnCustomerEdited(CustomerData editedCustomer)
        {
            if (editedCustomer != null)
            {
                // User confirmed edit
                ShowCustomerInfo(editedCustomer);
                UpdateStatus($"Customer information updated: {editedCustomer.Name}", true);
            }
            else
            {
                // User cancelled edit
                UpdateStatus("User cancelled operation", false);
            }
        }

        /// <summary>
        /// Callback for data save completion
        /// </summary>
        private void OnDataSaved(bool success)
        {
            if (success)
            {
                UpdateStatus("Data saved successfully!", true);
            }
            else
            {
                UpdateStatus("Data save failed", false);
            }
        }

        #endregion

        #region Helper Methods

        private CustomerData GetCurrentCustomerData()
        {
            // This should retrieve from actual data source
            // For demo purposes, we parse from customerInfoLabel
            var text = _customerInfoLabel.Text;
            if (text.Contains("Not Set"))
                return null;

            // Simplified: In real applications, this should be stored in a field
            return new CustomerData
            {
                Name = "Sample Customer",
                Email = "example@test.com",
                Age = 30
            };
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);

        #endregion
    }
}
