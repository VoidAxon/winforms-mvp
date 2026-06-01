using System;
using System.IO;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.ExecutionRequestDemo
{
    public static class ExecutionRequestDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("ExecReqDemo");

        public static readonly ViewAction EditCustomer = Factory.Create("EditCustomer");
        public static readonly ViewAction SelectFile = Factory.Create("SelectFile");  // Use IDialogProvider
        public static readonly ViewAction SaveData = Factory.Create("SaveData");
    }

    /// <summary>
    /// Presenter for ExecutionRequest demonstration
    /// Demonstrates correct usage of ExecutionRequest and comparison with dependency injection
    /// </summary>
    public class ExecutionRequestDemoPresenter : WindowPresenterBase<IExecutionRequestDemoView>
    {
        private CustomerData _currentCustomer;
        private string _currentFilePath;

        protected override void OnViewAttached()
        {
            // Subscribe to ExecutionRequest events (learning tool, follows three iron rules)
            View.EditCustomerRequested += OnEditCustomerRequested;
            View.SaveDataRequested += OnSaveDataRequested;
        }

        protected override void RegisterViewActions()
        {
            // Register ViewActions
            _dispatcher.Register(
                ExecutionRequestDemoActions.EditCustomer,
                OnEditCustomerAction);

            // ✅ Recommended approach - Use IDialogProvider for file selection
            _dispatcher.Register(
                ExecutionRequestDemoActions.SelectFile,
                OnSelectFileAction);

            _dispatcher.Register(
                ExecutionRequestDemoActions.SaveData,
                OnSaveDataAction,
                canExecute: () => _currentCustomer != null);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.UpdateStatus("Ready. Scenario 1 uses ExecutionRequest (learning tool, View opens legacy form), Scenario 2 uses IDialogProvider (recommended).", true);
        }

        #region ViewAction Handlers

        private void OnEditCustomerAction()
        {
            // ViewAction triggered, actual logic handled by ExecutionRequest
            // Nothing to do here, the real logic is in OnEditCustomerRequested
        }

        /// <summary>
        /// ✅ Correct approach: Use IDialogProvider directly, do not use ExecutionRequest
        /// </summary>
        private void OnSelectFileAction()
        {
            var options = new WinformsMVP.Services.Implementations.DialogOptions.OpenFileDialogOptions
            {
                Filter = "Text Files|*.txt|CSV Files|*.csv|All Files|*.*",
                Title = "Select File"
            };

            var result = Dialogs.ShowOpenFileDialog(options);

            if (result.Status == WinformsMVP.Common.InteractionStatus.Ok)
            {
                _currentFilePath = result.Value;
                View.ShowSelectedFile(_currentFilePath);
                View.UpdateStatus($"File selected: {System.IO.Path.GetFileName(_currentFilePath)}", true);
            }
            else
            {
                View.UpdateStatus("No file selected", false);
            }
        }

        private void OnSaveDataAction()
        {
            // ViewAction triggered, actual logic handled by ExecutionRequest
        }

        #endregion

        #region ExecutionRequest Handlers

        /// <summary>
        /// Scenario 1: Handle request to edit customer information
        /// ✅ Follows iron rules: Parameters and return values are all business data types (CustomerData)
        ///
        /// Correct ExecutionRequest workflow:
        /// 1. View opens legacy form itself to get user input (View handles UI)
        /// 2. View passes edited business data to Presenter through ExecutionRequest event
        /// 3. Presenter handles business logic (validation, saving, etc.)
        /// 4. Presenter returns result through callback
        /// </summary>
        private void OnEditCustomerRequested(object sender,
            ExecutionRequestEventArgs<CustomerData, CustomerData> e)
        {
            try
            {
                // View has already opened the legacy form and obtained the edited data
                // Presenter only handles business logic
                var editedData = e.Param;  // ✅ Business data passed by View

                if (editedData != null)
                {
                    // Execute business logic (validation, processing, etc.)
                    // Validation logic can be added here
                    if (string.IsNullOrWhiteSpace(editedData.Name))
                    {
                        Messages.ShowWarning("Customer name cannot be empty", "Validation Failed");
                        e.Callback?.Invoke(null);
                        return;
                    }

                    // Save current data
                    _currentCustomer = editedData;

                    // Update CanExecute
                    _dispatcher.RaiseCanExecuteChanged();

                    // Return processed data
                    e.Callback?.Invoke(editedData);  // ✅ Return business data
                }
                else
                {
                    // User cancelled editing
                    e.Callback?.Invoke(null);
                }
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Processing failed: {ex.Message}", "Error");
                e.Callback?.Invoke(null);  // ✅ Return null on failure
            }
        }


        /// <summary>
        /// Scenario 2: Handle save data request
        /// ✅ Follows iron rules: Parameters and return values are all business data types
        /// </summary>
        private void OnSaveDataRequested(object sender,
            ExecutionRequestEventArgs<CustomerData, bool> e)
        {
            try
            {
                // Presenter executes business logic
                var result = SaveCustomerData(e.Param);
                e.Callback?.Invoke(result);  // ✅ Return bool
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Save failed: {ex.Message}", "Error");
                e.Callback?.Invoke(false);
            }
        }

        #endregion

        #region Business Logic - Presenter's business logic implementation

        /// <summary>
        /// Save data logic provided by Presenter
        /// ✅ Only handles business logic, no UI involvement
        /// </summary>
        private bool SaveCustomerData(CustomerData data)
        {
            try
            {
                // Simulate saving to file or database
                var message = $"Customer data saved:\n" +
                             $"Name: {data.Name}\n" +
                             $"Email: {data.Email}\n" +
                             $"Age: {data.Age}";

                Messages.ShowInfo(message, "Save Successful");
                View.UpdateStatus("Customer data saved successfully", true);

                return true;
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Save failed: {ex.Message}", "Error");
                View.UpdateStatus("Save failed", false);
                return false;
            }
        }

        #endregion

        protected override void Cleanup()
        {
            // Unsubscribe from events
            if (View != null)
            {
                View.EditCustomerRequested -= OnEditCustomerRequested;
                View.SaveDataRequested -= OnSaveDataRequested;
            }
        }
    }
}
