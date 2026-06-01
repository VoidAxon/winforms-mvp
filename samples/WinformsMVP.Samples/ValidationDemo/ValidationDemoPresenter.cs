using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ValidationDemo
{
    /// <summary>
    /// Presenter demonstrating various validation patterns:
    /// - Field-level validation (immediate feedback)
    /// - Cross-field validation (password confirmation)
    /// - Pattern validation (email, phone)
    /// - Business rule validation (age restrictions)
    /// - Form-level validation (all fields must be valid)
    /// </summary>
    public class ValidationDemoPresenter : WindowPresenterBase<IValidationDemoView>
    {
        private readonly Dictionary<string, List<string>> _fieldErrors = new Dictionary<string, List<string>>();

        protected override void OnViewAttached()
        {
            // Subscribe to field changes for real-time validation
            View.UserNameChanged += (s, e) => ValidateUserName();
            View.EmailChanged += (s, e) => ValidateEmail();
            View.PasswordChanged += (s, e) =>
            {
                ValidatePassword();
                ValidatePasswordConfirmation();  // Re-validate confirmation
            };
            View.ConfirmPasswordChanged += (s, e) => ValidatePasswordConfirmation();
            View.AgeChanged += (s, e) => ValidateAge();
            View.PhoneNumberChanged += (s, e) => ValidatePhoneNumber();
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(
                ValidationActions.Submit,
                OnSubmit,
                canExecute: () => !View.HasValidationErrors);

            _dispatcher.Register(
                ValidationActions.Reset,
                OnReset);

            _dispatcher.Register(
                ValidationActions.ValidateAll,
                OnValidateAll);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.ClearAllErrors();
        }

        private void OnSubmit()
        {
            // Final validation before submission
            if (!ValidateAllFields())
            {
                View.ShowValidationSummary(GetAllErrors());
                Messages.ShowWarning("Please correct the validation errors before submitting.", "Validation Failed");
                return;
            }

            // Simulate form submission
            Messages.ShowInfo(
                $"Form submitted successfully!\n\n" +
                $"User Name: {View.UserName}\n" +
                $"Email: {View.Email}\n" +
                $"Age: {View.Age}\n" +
                $"Phone: {View.PhoneNumber}",
                "Success");

            OnReset();
        }

        private void OnReset()
        {
            // Clear form
            View.UserName = string.Empty;
            View.Email = string.Empty;
            View.Password = string.Empty;
            View.ConfirmPassword = string.Empty;
            View.Age = 0;
            View.PhoneNumber = string.Empty;

            // Clear all validation errors
            _fieldErrors.Clear();
            View.ClearAllErrors();

            _dispatcher.RaiseCanExecuteChanged();
        }

        private void OnValidateAll()
        {
            ValidateAllFields();

            var errors = GetAllErrors().ToList();
            if (errors.Any())
            {
                View.ShowValidationSummary(errors);
                Messages.ShowWarning($"Found {errors.Count} validation error(s).", "Validation Results");
            }
            else
            {
                Messages.ShowInfo("All fields are valid!", "Validation Results");
            }
        }

        private bool ValidateAllFields()
        {
            _fieldErrors.Clear();
            View.ClearAllErrors();

            ValidateUserName();
            ValidateEmail();
            ValidatePassword();
            ValidatePasswordConfirmation();
            ValidateAge();
            ValidatePhoneNumber();

            _dispatcher.RaiseCanExecuteChanged();

            return !_fieldErrors.Any();
        }

        private void ValidateUserName()
        {
            var fieldName = "UserName";
            ClearFieldErrors(fieldName);

            var value = View.UserName;

            // Required validation
            if (string.IsNullOrWhiteSpace(value))
            {
                AddFieldError(fieldName, "User name is required.");
                return;
            }

            // Length validation
            if (value.Length < 3)
            {
                AddFieldError(fieldName, "User name must be at least 3 characters.");
            }

            if (value.Length > 50)
            {
                AddFieldError(fieldName, "User name must not exceed 50 characters.");
            }

            // Pattern validation
            if (!Regex.IsMatch(value, @"^[a-zA-Z0-9_]+$"))
            {
                AddFieldError(fieldName, "User name can only contain letters, numbers, and underscores.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ValidateEmail()
        {
            var fieldName = "Email";
            ClearFieldErrors(fieldName);

            var value = View.Email;

            // Required validation
            if (string.IsNullOrWhiteSpace(value))
            {
                AddFieldError(fieldName, "Email is required.");
                UpdateFieldErrorDisplay(fieldName);
                return;
            }

            // Email pattern validation
            var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(value, emailPattern))
            {
                AddFieldError(fieldName, "Email format is invalid.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ValidatePassword()
        {
            var fieldName = "Password";
            ClearFieldErrors(fieldName);

            var value = View.Password;

            // Required validation
            if (string.IsNullOrWhiteSpace(value))
            {
                AddFieldError(fieldName, "Password is required.");
                UpdateFieldErrorDisplay(fieldName);
                return;
            }

            // Length validation
            if (value.Length < 8)
            {
                AddFieldError(fieldName, "Password must be at least 8 characters.");
            }

            // Complexity validation
            if (!Regex.IsMatch(value, @"[A-Z]"))
            {
                AddFieldError(fieldName, "Password must contain at least one uppercase letter.");
            }

            if (!Regex.IsMatch(value, @"[a-z]"))
            {
                AddFieldError(fieldName, "Password must contain at least one lowercase letter.");
            }

            if (!Regex.IsMatch(value, @"[0-9]"))
            {
                AddFieldError(fieldName, "Password must contain at least one digit.");
            }

            if (!Regex.IsMatch(value, @"[!@#$%^&*(),.?""':{}|<>]"))
            {
                AddFieldError(fieldName, "Password must contain at least one special character.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ValidatePasswordConfirmation()
        {
            var fieldName = "ConfirmPassword";
            ClearFieldErrors(fieldName);

            var password = View.Password;
            var confirmPassword = View.ConfirmPassword;

            // Required validation
            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                AddFieldError(fieldName, "Password confirmation is required.");
                UpdateFieldErrorDisplay(fieldName);
                return;
            }

            // Cross-field validation
            if (password != confirmPassword)
            {
                AddFieldError(fieldName, "Passwords do not match.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ValidateAge()
        {
            var fieldName = "Age";
            ClearFieldErrors(fieldName);

            var value = View.Age;

            // Required validation
            if (value <= 0)
            {
                AddFieldError(fieldName, "Age is required.");
                UpdateFieldErrorDisplay(fieldName);
                return;
            }

            // Range validation (business rule)
            if (value < 18)
            {
                AddFieldError(fieldName, "You must be at least 18 years old.");
            }

            if (value > 120)
            {
                AddFieldError(fieldName, "Age must not exceed 120 years.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ValidatePhoneNumber()
        {
            var fieldName = "PhoneNumber";
            ClearFieldErrors(fieldName);

            var value = View.PhoneNumber;

            // Optional field - only validate if provided
            if (string.IsNullOrWhiteSpace(value))
            {
                UpdateFieldErrorDisplay(fieldName);
                return;
            }

            // Phone number pattern validation
            // Accepts formats: (123) 456-7890, 123-456-7890, 1234567890
            var phonePattern = @"^(\(\d{3}\)\s?|\d{3}[-.]?)?\d{3}[-.]?\d{4}$";
            if (!Regex.IsMatch(value, phonePattern))
            {
                AddFieldError(fieldName, "Phone number format is invalid. Use (123) 456-7890 or 123-456-7890.");
            }

            UpdateFieldErrorDisplay(fieldName);
        }

        private void ClearFieldErrors(string fieldName)
        {
            if (_fieldErrors.ContainsKey(fieldName))
            {
                _fieldErrors.Remove(fieldName);
            }
        }

        private void AddFieldError(string fieldName, string error)
        {
            if (!_fieldErrors.ContainsKey(fieldName))
            {
                _fieldErrors[fieldName] = new List<string>();
            }
            _fieldErrors[fieldName].Add(error);
        }

        private void UpdateFieldErrorDisplay(string fieldName)
        {
            if (_fieldErrors.ContainsKey(fieldName) && _fieldErrors[fieldName].Any())
            {
                var errorMessage = string.Join("\n", _fieldErrors[fieldName]);
                View.SetFieldError(fieldName, errorMessage);
            }
            else
            {
                View.ClearFieldError(fieldName);
            }

            _dispatcher.RaiseCanExecuteChanged();
        }

        private IEnumerable<string> GetAllErrors()
        {
            foreach (var kvp in _fieldErrors)
            {
                foreach (var error in kvp.Value)
                {
                    yield return $"{kvp.Key}: {error}";
                }
            }
        }
    }

    public static class ValidationActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Validation");

        public static readonly ViewAction Submit = Factory.Create("Submit");
        public static readonly ViewAction Reset = Factory.Create("Reset");
        public static readonly ViewAction ValidateAll = Factory.Create("ValidateAll");
    }
}
