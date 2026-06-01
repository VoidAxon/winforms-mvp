using System;
using System.Text;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.MVPComparisonDemo.PassiveView
{
    /// <summary>
    /// Actions for Passive View user editor
    /// </summary>
    public static class UserEditorActions
    {
        // Use standard actions directly (no prefix)
        public static readonly ViewAction Save = StandardActions.Save;
        public static readonly ViewAction Reset = StandardActions.Reset;
        public static readonly ViewAction Close = StandardActions.Close;
    }

    /// <summary>
    /// Presenter for Passive View pattern.
    ///
    /// Passive View characteristics:
    /// - Presenter has COMPLETE control over the view
    /// - Presenter manually reads and writes ALL view properties
    /// - Presenter handles ALL validation and business logic
    /// - Presenter manually synchronizes view state
    /// - No data binding - everything is explicit
    ///
    /// Advantages:
    /// - Complete control and testability
    /// - No hidden behavior (everything explicit)
    /// - Easy to understand data flow
    ///
    /// Disadvantages:
    /// - More boilerplate code
    /// - Presenter must manually sync view state
    /// - More work for simple scenarios
    /// </summary>
    public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
    {
        // Presenter holds the original data for reset functionality
        private string _originalName;
        private string _originalEmail;
        private int _originalAge;
        private bool _originalIsActive;

        protected override void OnViewAttached()
        {
            // Nothing to do here
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(UserEditorActions.Save, OnSave);
            _dispatcher.Register(UserEditorActions.Reset, OnReset);
            _dispatcher.Register(UserEditorActions.Close, OnClose);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Initialize with sample data
            // In Passive View, Presenter MANUALLY sets all view properties
            View.UserName = "John Doe";
            View.Email = "john.doe@example.com";
            View.Age = 30;
            View.IsActive = true;

            // Store original values for reset
            SaveOriginalValues();

            View.UpdateStatus("Ready. Modify the fields and click Save.");
        }

        private void SaveOriginalValues()
        {
            // Presenter manually reads from view to save state
            _originalName = View.UserName;
            _originalEmail = View.Email;
            _originalAge = View.Age;
            _originalIsActive = View.IsActive;
        }

        private void OnSave()
        {
            // Presenter MANUALLY reads all data from view
            var name = View.UserName;
            var email = View.Email;
            var age = View.Age;
            var isActive = View.IsActive;

            // Presenter handles ALL validation logic
            var errors = ValidateUserData(name, email, age);
            if (!string.IsNullOrEmpty(errors))
            {
                // Presenter MANUALLY tells view to show errors
                View.ShowValidationErrors(errors);
                View.UpdateStatus("Validation failed. Please fix the errors.");
                return;
            }

            // Clear errors
            View.ClearValidationErrors();

            // Simulate saving
            Messages.ShowInfo(
                $"User saved successfully!\n\n" +
                $"Name: {name}\n" +
                $"Email: {email}\n" +
                $"Age: {age}\n" +
                $"Active: {isActive}",
                "Success");

            // Update original values after successful save
            SaveOriginalValues();

            // Presenter MANUALLY updates status
            View.UpdateStatus("User data saved successfully!");
        }

        private void OnReset()
        {
            // Presenter MANUALLY restores all view properties
            View.UserName = _originalName;
            View.Email = _originalEmail;
            View.Age = _originalAge;
            View.IsActive = _originalIsActive;

            View.ClearValidationErrors();
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

        /// <summary>
        /// Presenter contains ALL validation logic
        /// </summary>
        private string ValidateUserData(string name, string email, int age)
        {
            var errors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.AppendLine("• Name is required");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.AppendLine("• Email is required");
            }
            else if (!email.Contains("@"))
            {
                errors.AppendLine("• Email must contain @");
            }

            if (age < 18 || age > 120)
            {
                errors.AppendLine("• Age must be between 18 and 120");
            }

            return errors.ToString().Trim();
        }
    }
}
