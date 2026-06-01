using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ValidationDemo
{
    /// <summary>
    /// Demonstrates complex validation patterns in MVP.
    /// Shows field-level, cross-field, and asynchronous validation.
    /// </summary>
    public interface IValidationDemoView : IWindowView
    {
        // Form data
        string UserName { get; set; }
        string Email { get; set; }
        string Password { get; set; }
        string ConfirmPassword { get; set; }
        int Age { get; set; }
        string PhoneNumber { get; set; }

        // Validation state
        bool HasValidationErrors { get; }

        // Error display
        void SetFieldError(string fieldName, string errorMessage);
        void ClearFieldError(string fieldName);
        void ClearAllErrors();
        void ShowValidationSummary(IEnumerable<string> errors);

        // Events
        event EventHandler UserNameChanged;
        event EventHandler EmailChanged;
        event EventHandler PasswordChanged;
        event EventHandler ConfirmPasswordChanged;
        event EventHandler AgeChanged;
        event EventHandler PhoneNumberChanged;
    }
}
