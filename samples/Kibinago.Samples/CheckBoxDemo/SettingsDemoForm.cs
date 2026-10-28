using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Events;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.CheckBoxDemo
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
        // ViewActionBinder is internal to the Form
        private ViewActionBinder _viewActionBinder;

        // State
        private bool _hasSettings;

        public SettingsDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
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

        public bool HasSettings
        {
            get => _hasSettings;
            set
            {
                _hasSettings = value;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

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

        #region Events

        public event EventHandler SettingsChanged;

        #endregion
    }
}
