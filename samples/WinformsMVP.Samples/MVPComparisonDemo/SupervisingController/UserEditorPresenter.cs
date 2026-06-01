using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.MVPComparisonDemo.SupervisingController
{
    /// <summary>
    /// Actions for Supervising Controller user editor
    /// </summary>
    public static class UserEditorActions
    {
        // Use standard actions directly (no prefix)
        public static readonly ViewAction Save = StandardActions.Save;
        public static readonly ViewAction Reset = StandardActions.Reset;
        public static readonly ViewAction Close = StandardActions.Close;
    }

    /// <summary>
    /// Presenter for Supervising Controller pattern.
    ///
    /// Supervising Controller characteristics:
    /// - Presenter works with the MODEL, not individual view properties
    /// - View handles simple data binding automatically (Name, Email, Age, etc.)
    /// - Presenter focuses on COORDINATION and COMPLEX LOGIC
    /// - Validation happens in the Model (simple) or Presenter (complex)
    /// - Less code compared to Passive View for simple scenarios
    ///
    /// Advantages:
    /// - Less boilerplate code
    /// - Automatic UI synchronization via data binding
    /// - Presenter focuses on business logic, not UI details
    /// - Cleaner separation for complex scenarios
    ///
    /// Disadvantages:
    /// - Some behavior is "hidden" in data binding
    /// - Requires understanding of binding mechanism
    /// - Model must implement INotifyPropertyChanged
    /// </summary>
    public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
    {
        private readonly IMessageService _messageService;
        private UserModel _originalModel;

        public UserEditorPresenter(IMessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        protected override void OnViewAttached()
        {
            // Nothing to do here
        }

        protected override void RegisterViewActions()
        {
            // Register actions with CanExecute predicates
            _dispatcher.Register(
                UserEditorActions.Save,
                OnSave,
                canExecute: () => View.Model != null && View.Model.IsValid);

            _dispatcher.Register(UserEditorActions.Reset, OnReset);
            _dispatcher.Register(UserEditorActions.Close, OnClose);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Create and initialize the model
            // In Supervising Controller, Presenter creates the Model
            var model = new UserModel
            {
                Name = "John Doe",
                Email = "john.doe@example.com",
                Age = 30,
                IsActive = true
            };

            // Subscribe to model property changes for CanExecute updates
            model.PropertyChanged += (s, e) =>
            {
                // When model properties change, update action availability
                _dispatcher.RaiseCanExecuteChanged();
            };

            // Set the model - View will automatically bind to it
            View.Model = model;

            // Save snapshot for reset
            _originalModel = model.Clone() as UserModel;

            View.UpdateStatus("Ready. Modify the fields and watch validation update automatically!");
        }

        private void OnSave()
        {
            // Presenter only handles high-level coordination
            // No need to read individual properties - work with the model!
            var model = View.Model;

            // Model validation already happened automatically
            // Just check if it's valid
            if (!model.IsValid)
            {
                // This should not happen because Save button should be disabled
                // but we check anyway for safety
                View.UpdateStatus("Validation failed. Please fix the errors.");
                return;
            }

            // Simulate saving
            _messageService.ShowInfo(
                $"User saved successfully!\n\n" +
                $"Name: {model.Name}\n" +
                $"Email: {model.Email}\n" +
                $"Age: {model.Age}\n" +
                $"Active: {model.IsActive}",
                "Success");

            // Update snapshot after successful save
            _originalModel = model.Clone() as UserModel;

            View.UpdateStatus("User data saved successfully!");
        }

        private void OnReset()
        {
            // Presenter coordinates the reset
            // No need to manually update each property -
            // just restore the model and binding handles the rest!
            View.Model.CopyFrom(_originalModel);

            View.UpdateStatus("Form reset to original values.");
        }

        private void OnClose()
        {
            // Close the form
            if (View is System.Windows.Forms.Form form)
            {
                form.Close();
            }
        }
    }
}
