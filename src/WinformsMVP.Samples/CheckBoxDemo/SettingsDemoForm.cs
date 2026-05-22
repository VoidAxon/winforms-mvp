using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.CheckBoxDemo
{
    /// <summary>
    /// Demo form for CheckBox/RadioButton with ViewAction system.
    ///
    /// Demonstrates:
    /// - CheckBox binding with CheckedChanged event
    /// - RadioButton binding with CheckedChanged event
    /// - CanExecute controlling Enabled state
    /// - Proper MVP separation (no UI types in interface)
    /// </summary>
    public partial class SettingsDemoForm : Form, ISettingsView
    {
        // Private UI Controls (not exposed through interface)
        private CheckBox _autoSaveCheckBox;
        private CheckBox _notificationsCheckBox;
        private RadioButton _lightThemeRadio;
        private RadioButton _darkThemeRadio;
        private RadioButton _autoThemeRadio;
        private Button _applyButton;
        private Button _resetButton;
        private Label _statusLabel;
        private TextBox _summaryTextBox;

        // ViewActionBinder is internal to the Form
        private ViewActionBinder _viewActionBinder;

        // State
        private bool _hasSettings;

        public SettingsDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "WinForms MVP - CheckBox/RadioButton Demo";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "Settings - CheckBox/RadioButton ViewAction Demo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(540, 30),
                ForeColor = Color.DarkBlue
            };

            // Info
            var infoLabel = new Label
            {
                Text = "CheckBox and RadioButton controls trigger actions via CheckedChanged event.\n" +
                       "Apply/Reset buttons demonstrate CanExecute (enabled only when changes exist).",
                Location = new Point(20, 55),
                Size = new Size(540, 40),
                ForeColor = Color.DarkGray
            };

            // CheckBox Section
            var checkBoxGroupLabel = new Label
            {
                Text = "CheckBox Options:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 110),
                Size = new Size(200, 20)
            };

            _autoSaveCheckBox = new CheckBox
            {
                Text = "Enable Auto-Save",
                Location = new Point(40, 140),
                Size = new Size(200, 24),
                Checked = false
            };

            _notificationsCheckBox = new CheckBox
            {
                Text = "Enable Notifications",
                Location = new Point(40, 170),
                Size = new Size(200, 24),
                Checked = true
            };

            // RadioButton Section
            var radioGroupLabel = new Label
            {
                Text = "Theme Selection:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 210),
                Size = new Size(200, 20)
            };

            var themePanel = new Panel
            {
                Location = new Point(40, 240),
                Size = new Size(500, 90),
                BorderStyle = BorderStyle.FixedSingle
            };

            _lightThemeRadio = new RadioButton
            {
                Text = "Light Theme",
                Location = new Point(10, 10),
                Size = new Size(150, 24),
                Checked = true
            };

            _darkThemeRadio = new RadioButton
            {
                Text = "Dark Theme",
                Location = new Point(10, 35),
                Size = new Size(150, 24)
            };

            _autoThemeRadio = new RadioButton
            {
                Text = "Auto (System)",
                Location = new Point(10, 60),
                Size = new Size(150, 24)
            };

            themePanel.Controls.AddRange(new Control[] {
                _lightThemeRadio, _darkThemeRadio, _autoThemeRadio
            });

            // Action Buttons
            _applyButton = new Button
            {
                Text = "Apply Settings",
                Location = new Point(290, 250),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false // Controlled by CanExecute
            };
            _applyButton.FlatAppearance.BorderSize = 0;

            _resetButton = new Button
            {
                Text = "Reset to Default",
                Location = new Point(420, 250),
                Size = new Size(120, 32),
                Enabled = false // Controlled by CanExecute
            };

            // Settings Summary
            var summaryLabel = new Label
            {
                Text = "Current Settings:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 350),
                Size = new Size(200, 20)
            };

            _summaryTextBox = new TextBox
            {
                Location = new Point(20, 375),
                Size = new Size(540, 40),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // Status Bar
            var statusPanel = new Panel
            {
                Location = new Point(0, 430),
                Size = new Size(600, 40),
                BackColor = Color.FromArgb(240, 240, 240),
                Dock = DockStyle.Bottom
            };

            _statusLabel = new Label
            {
                Text = "Ready. Change settings to see CheckedChanged actions trigger.",
                Location = new Point(20, 10),
                Size = new Size(540, 20),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            statusPanel.Controls.Add(_statusLabel);

            // Add all controls
            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel,
                checkBoxGroupLabel, _autoSaveCheckBox, _notificationsCheckBox,
                radioGroupLabel, themePanel,
                _applyButton, _resetButton,
                summaryLabel, _summaryTextBox,
                statusPanel
            });

            _hasSettings = false;
        }

        #region ISettingsView Implementation

        public bool IsAutoSaveEnabled
        {
            get => _autoSaveCheckBox.Checked;
            set => _autoSaveCheckBox.Checked = value;
        }

        public bool IsNotificationsEnabled
        {
            get => _notificationsCheckBox.Checked;
            set => _notificationsCheckBox.Checked = value;
        }

        public string SelectedTheme
        {
            get
            {
                if (_lightThemeRadio.Checked) return "Light";
                if (_darkThemeRadio.Checked) return "Dark";
                if (_autoThemeRadio.Checked) return "Auto";
                return "Light";
            }
            set
            {
                _lightThemeRadio.Checked = value == "Light";
                _darkThemeRadio.Checked = value == "Dark";
                _autoThemeRadio.Checked = value == "Auto";
            }
        }

        public bool HasSettings => _hasSettings;

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            // ========================================
            // CRITICAL: This method should contain ONLY UI binding code!
            //
            // ✅ DO:
            //    - Map controls to actions (_binder.Add(...))
            //    - Bind to dispatcher (_binder.Bind(...))
            //
            // ❌ DO NOT:
            //    - Call database operations
            //    - Execute business logic
            //    - Perform expensive computations
            //    - Make network calls
            //
            // Think of this like WPF's InitializeComponent() - pure UI infrastructure only.
            // ========================================

            _viewActionBinder = new ViewActionBinder();

            // Bind CheckBox controls - will use CheckedChanged event
            _viewActionBinder.Add(SettingsDemoActions.ToggleAutoSave, _autoSaveCheckBox);
            _viewActionBinder.Add(SettingsDemoActions.ToggleNotifications, _notificationsCheckBox);

            // Bind RadioButton controls - will use CheckedChanged event
            _viewActionBinder.Add(SettingsDemoActions.SelectLightTheme, _lightThemeRadio);
            _viewActionBinder.Add(SettingsDemoActions.SelectDarkTheme, _darkThemeRadio);
            _viewActionBinder.Add(SettingsDemoActions.SelectAutoTheme, _autoThemeRadio);

            // Bind regular buttons - will use Click event
            _viewActionBinder.Add(SettingsDemoActions.ApplySettings, _applyButton);
            _viewActionBinder.Add(SettingsDemoActions.ResetSettings, _resetButton);

        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        public void ShowSettingsSummary(string summary)
        {
            _summaryTextBox.Text = summary;
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        #endregion

        #region Events

        public event EventHandler SettingsChanged;

        internal void OnSettingsChanged()
        {
            _hasSettings = true;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
