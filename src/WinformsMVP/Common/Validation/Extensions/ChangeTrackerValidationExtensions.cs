using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinformsMVP.Common.Validation.Core;

namespace WinformsMVP.Common.Validation.Extensions
{
    /// <summary>
    /// Provides extension methods for integrating validation with ChangeTracker&lt;T&gt;.
    /// Enables validation before accepting or rejecting changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These extensions allow you to validate dirty data before committing changes,
    /// preventing invalid data from being accepted into the model.
    /// </para>
    ///
    /// <para>
    /// <b>Common Pattern in Presenters:</b>
    /// <code>
    /// public class EditUserPresenter : WindowPresenterBase&lt;IEditUserView&gt;
    /// {
    ///     private readonly ChangeTracker&lt;UserModel&gt; _changeTracker;
    ///     private readonly IModelValidator _validator = ModelValidator.For&lt;UserModel&gt;();
    ///
    ///     protected override void OnInitialize()
    ///     {
    ///         var user = LoadUser(userId);
    ///         _changeTracker = new ChangeTracker&lt;UserModel&gt;(user);
    ///         View.Model = _changeTracker.CurrentValue;
    ///     }
    ///
    ///     private void OnSave()
    ///     {
    ///         // Only accept changes if validation passes
    ///         if (_changeTracker.AcceptChangesIfValid(_validator, out var errors))
    ///         {
    ///             SaveUser(_changeTracker.CurrentValue);
    ///             Messages.ShowInfo("User saved successfully!");
    ///         }
    ///         else
    ///         {
    ///             ShowValidationErrors(errors);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static class ChangeTrackerValidationExtensions
    {
        /// <summary>
        /// Validates the current value and only accepts changes if validation passes.
        /// </summary>
        /// <typeparam name="T">The type of model being tracked. Must implement ICloneable.</typeparam>
        /// <param name="tracker">The ChangeTracker instance.</param>
        /// <param name="validator">The ModelValidator to use for validation.</param>
        /// <param name="errors">
        /// Output parameter containing validation errors if validation fails.
        /// Empty list if validation succeeds.
        /// </param>
        /// <returns>
        /// true if validation passed and changes were accepted; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tracker"/> or <paramref name="validator"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// 1. Validates the current value using ValidateAll()
        /// 2. If validation passes, calls AcceptChanges() and returns true
        /// 3. If validation fails, returns false and outputs errors
        /// </para>
        ///
        /// <para>
        /// <b>Use Case - Save Button Handler:</b>
        /// <code>
        /// private void OnSave()
        /// {
        ///     if (!_changeTracker.AcceptChangesIfValid(_validator, out var errors))
        ///     {
        ///         var errorMessage = string.Join("\n", errors.Select(e => e.GetFormattedMessage()));
        ///         Messages.ShowWarning(errorMessage, "Validation Failed");
        ///         return;
        ///     }
        ///
        ///     // Validation passed, changes accepted
        ///     SaveModel(_changeTracker.CurrentValue);
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public static bool AcceptChangesIfValid<T>(
            this ChangeTracker<T> tracker,
            IModelValidator validator,
            out ReadOnlyCollection<ValidationResult> errors) where T : class, ICloneable
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            // Validate the current value
            errors = validator.ValidateAll(tracker.CurrentValue);

            // If there are any errors, don't accept changes
            if (errors.Any())
                return false;

            // Validation passed - accept changes
            tracker.AcceptChanges();
            return true;
        }

        /// <summary>
        /// Validates the current value sequentially and checks if it's valid.
        /// Does not modify the ChangeTracker state.
        /// </summary>
        /// <typeparam name="T">The type of model being tracked. Must implement ICloneable.</typeparam>
        /// <param name="tracker">The ChangeTracker instance.</param>
        /// <param name="validator">The ModelValidator to use for validation.</param>
        /// <param name="error">
        /// Output parameter containing the first validation error if validation fails.
        /// ValidationResult.Success if validation succeeds.
        /// </param>
        /// <returns>
        /// true if validation passed (no errors); otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tracker"/> or <paramref name="validator"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method validates the current value without modifying ChangeTracker state.
        /// Useful for checking validity before allowing certain actions.
        /// </para>
        ///
        /// <para>
        /// <b>Use Case - CanExecute Predicate:</b>
        /// <code>
        /// protected override void RegisterViewActions()
        /// {
        ///     _dispatcher.Register(
        ///         CommonActions.Save,
        ///         OnSave,
        ///         canExecute: () =>
        ///         {
        ///             return _changeTracker.IsChanged &&
        ///                    _changeTracker.IsCurrentValueValid(_validator, out _);
        ///         });
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public static bool IsCurrentValueValid<T>(
            this ChangeTracker<T> tracker,
            IModelValidator validator,
            out ValidationResult error) where T : class, ICloneable
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            // Validate sequentially (stops on first error)
            error = validator.ValidateSequential(tracker.CurrentValue);

            return error.IsValid;
        }

        /// <summary>
        /// Validates the current value and returns whether it's valid.
        /// Convenience overload that doesn't output the error.
        /// </summary>
        /// <typeparam name="T">The type of model being tracked. Must implement ICloneable.</typeparam>
        /// <param name="tracker">The ChangeTracker instance.</param>
        /// <param name="validator">The ModelValidator to use for validation.</param>
        /// <returns>
        /// true if the current value is valid; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tracker"/> or <paramref name="validator"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is a convenience overload when you don't need the actual error message.
        /// </para>
        ///
        /// <para>
        /// <b>Example:</b>
        /// <code>
        /// if (_changeTracker.IsCurrentValueValid(_validator))
        /// {
        ///     EnableSaveButton();
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public static bool IsCurrentValueValid<T>(
            this ChangeTracker<T> tracker,
            IModelValidator validator) where T : class, ICloneable
        {
            return IsCurrentValueValid(tracker, validator, out _);
        }

        /// <summary>
        /// Rejects changes if the current value is invalid.
        /// Useful for "Cancel" scenarios where you want to revert invalid changes.
        /// </summary>
        /// <typeparam name="T">The type of model being tracked. Must implement ICloneable.</typeparam>
        /// <param name="tracker">The ChangeTracker instance.</param>
        /// <param name="validator">The ModelValidator to use for validation.</param>
        /// <returns>
        /// true if changes were rejected (current value was invalid); otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tracker"/> or <paramref name="validator"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method checks if the current value is invalid, and if so, reverts to the original value.
        /// Useful for automatically discarding invalid changes.
        /// </para>
        ///
        /// <para>
        /// <b>Example - Auto-Reset on Invalid Data:</b>
        /// <code>
        /// private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        /// {
        ///     // Automatically reject invalid changes
        ///     if (_changeTracker.RejectChangesIfInvalid(_validator))
        ///     {
        ///         Messages.ShowWarning("Invalid value entered. Reverting changes.");
        ///         RefreshView();
        ///     }
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public static bool RejectChangesIfInvalid<T>(
            this ChangeTracker<T> tracker,
            IModelValidator validator) where T : class, ICloneable
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            // Check if current value is invalid
            if (!IsCurrentValueValid(tracker, validator, out _))
            {
                // Invalid - reject changes
                tracker.RejectChanges();
                return true;
            }

            return false;
        }
    }
}
