using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.CheckBoxDemo
{
    /// <summary>
    /// Action keys for Settings demo.
    /// </summary>
    public static class SettingsDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Settings");

        // Business-specific actions
        public static readonly ViewAction ToggleAutoSave = Factory.Create("ToggleAutoSave");
        public static readonly ViewAction ToggleNotifications = Factory.Create("ToggleNotifications");
        public static readonly ViewAction SelectLightTheme = Factory.Create("SelectLightTheme");
        public static readonly ViewAction SelectDarkTheme = Factory.Create("SelectDarkTheme");
        public static readonly ViewAction SelectAutoTheme = Factory.Create("SelectAutoTheme");

        // Standard actions - use directly (no prefix)
        public static readonly ViewAction ApplySettings = StandardActions.Apply;
        public static readonly ViewAction ResetSettings = StandardActions.Reset;
    }

    /// <summary>
    /// Presenter for Settings demo.
    ///
    /// Demonstrates:
    /// 1. CheckBox actions triggered by CheckedChanged event
    /// 2. RadioButton actions triggered by CheckedChanged event
    /// 3. CanExecute for Apply/Reset buttons (enabled only when HasSettings is true)
    /// 4. Automatic UI updates after actions
    /// 5. State-driven updates via RaiseCanExecuteChanged()
    /// 6. ZERO knowledge of UI controls (CheckBox, RadioButton, etc.)
    /// </summary>
    public class SettingsDemoPresenter : WindowPresenterBase<ISettingsView>
    {
        // No constructor needed - Platform services are automatically available

        protected override void OnViewAttached()
        {
            // Nothing to do - no UI element access
        }

        protected override void RegisterViewActions()
        {
            // CheckBox actions - no CanExecute (always enabled)
            _dispatcher.Register(SettingsDemoActions.ToggleAutoSave, OnToggleAutoSave);
            _dispatcher.Register(SettingsDemoActions.ToggleNotifications, OnToggleNotifications);

            // RadioButton actions - no CanExecute (always enabled)
            _dispatcher.Register(SettingsDemoActions.SelectLightTheme, OnSelectLightTheme);
            _dispatcher.Register(SettingsDemoActions.SelectDarkTheme, OnSelectDarkTheme);
            _dispatcher.Register(SettingsDemoActions.SelectAutoTheme, OnSelectAutoTheme);

            // Action buttons - CanExecute based on HasSettings
            _dispatcher.Register(
                SettingsDemoActions.ApplySettings,
                OnApplySettings,
                canExecute: () => View.HasSettings);

            _dispatcher.Register(
                SettingsDemoActions.ResetSettings,
                OnResetSettings,
                canExecute: () => View.HasSettings);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Subscribe to view events for state-driven updates
            View.SettingsChanged += (sender, e) =>
            {
                // Settings changed, refresh CanExecute states
                _dispatcher.RaiseCanExecuteChanged();
            };

            UpdateSettingsSummary();
            View.UpdateStatus("Ready. Change any setting to see CheckedChanged actions trigger.");
        }

        #region CheckBox Action Handlers

        private void OnToggleAutoSave()
        {
            var enabled = View.IsAutoSaveEnabled;
            View.UpdateStatus($"Auto-Save {(enabled ? "ENABLED" : "DISABLED")} (CheckedChanged triggered)");

            if (View is SettingsDemoForm form)
            {
                form.OnSettingsChanged();
            }

            UpdateSettingsSummary();

            // UI state automatically updates here!
            // Apply/Reset buttons will become enabled (HasSettings = true)
        }

        private void OnToggleNotifications()
        {
            var enabled = View.IsNotificationsEnabled;
            View.UpdateStatus($"Notifications {(enabled ? "ENABLED" : "DISABLED")} (CheckedChanged triggered)");

            if (View is SettingsDemoForm form)
            {
                form.OnSettingsChanged();
            }

            UpdateSettingsSummary();
        }

        #endregion

        #region RadioButton Action Handlers

        private void OnSelectLightTheme()
        {
            View.UpdateStatus("Light Theme selected (RadioButton CheckedChanged triggered)");

            if (View is SettingsDemoForm form)
            {
                form.OnSettingsChanged();
            }

            UpdateSettingsSummary();
        }

        private void OnSelectDarkTheme()
        {
            View.UpdateStatus("Dark Theme selected (RadioButton CheckedChanged triggered)");

            if (View is SettingsDemoForm form)
            {
                form.OnSettingsChanged();
            }

            UpdateSettingsSummary();
        }

        private void OnSelectAutoTheme()
        {
            View.UpdateStatus("Auto Theme selected (RadioButton CheckedChanged triggered)");

            if (View is SettingsDemoForm form)
            {
                form.OnSettingsChanged();
            }

            UpdateSettingsSummary();
        }

        #endregion

        #region Button Action Handlers

        private void OnApplySettings()
        {
            var summary = $"Settings Applied!\n\n" +
                         $"Auto-Save: {(View.IsAutoSaveEnabled ? "Enabled" : "Disabled")}\n" +
                         $"Notifications: {(View.IsNotificationsEnabled ? "Enabled" : "Disabled")}\n" +
                         $"Theme: {View.SelectedTheme}";

            Messages.ShowInfo(summary, "Settings Applied");

            View.UpdateStatus("Settings applied successfully!");
        }

        private void OnResetSettings()
        {
            if (!Messages.ConfirmYesNo("Reset all settings to default?", "Confirm Reset"))
            {
                return;
            }

            // Reset to defaults
            View.IsAutoSaveEnabled = false;
            View.IsNotificationsEnabled = true;
            View.SelectedTheme = "Light";

            if (View is SettingsDemoForm form)
            {
                // Clear HasSettings flag - this will disable Apply/Reset buttons
                typeof(SettingsDemoForm)
                    .GetField("_hasSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(form, false);
            }

            UpdateSettingsSummary();
            View.UpdateStatus("Settings reset to defaults.");

            // Trigger state refresh - Apply/Reset buttons will be disabled
            _dispatcher.RaiseCanExecuteChanged();
        }

        #endregion

        private void UpdateSettingsSummary()
        {
            var summary = $"Auto-Save: {(View.IsAutoSaveEnabled ? "ON" : "OFF")}\n" +
                         $"Notifications: {(View.IsNotificationsEnabled ? "ON" : "OFF")}\n" +
                         $"Theme: {View.SelectedTheme}";

            View.ShowSettingsSummary(summary);
        }
    }
}
