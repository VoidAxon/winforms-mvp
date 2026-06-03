using System.Linq;
using WinformsMVP.Common.Validation.Core;
using WinformsMVP.Services;

namespace WinformsMVP.Common.Validation.Helpers
{
    /// <summary>
    /// Provides helper methods for common validation scenarios.
    /// Simplifies validation with optional error display via IMessageService.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ValidationHelper provides flexible validation with optional UI feedback:
    /// </para>
    ///
    /// <para>
    /// <b>Validation only (no UI):</b>
    /// <code>
    /// if (!ValidationHelper.ValidateSequential(model, validator))
    ///     return;  // Validation failed, but no message shown
    /// </code>
    /// </para>
    ///
    /// <para>
    /// <b>Validation with error display:</b>
    /// <code>
    /// if (!ValidationHelper.ValidateSequential(model, validator, Messages))
    ///     return;  // Validation failed, error shown to user
    /// </code>
    /// </para>
    ///
    /// <para>
    /// <b>Validation with custom title:</b>
    /// <code>
    /// if (!ValidationHelper.ValidateSequential(model, validator, Messages, "自定义标题"))
    ///     return;
    /// </code>
    /// </para>
    /// </remarks>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validates all properties on the model and optionally displays errors.
        /// </summary>
        /// <typeparam name="T">The type of model to validate.</typeparam>
        /// <param name="model">The model instance to validate.</param>
        /// <param name="validator">The validator to use for validation.</param>
        /// <param name="messageService">
        /// Optional. If provided, displays all validation errors to the user.
        /// If null, validation occurs silently.
        /// </param>
        /// <param name="errorTitle">
        /// The title for the error message dialog. Default is "Validation Failed".
        /// Only used if messageService is provided.
        /// </param>
        /// <returns>
        /// true if the model is valid (no errors); otherwise, false.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs ValidateAll() to collect all validation errors.
        /// If messageService is provided, displays them in a single message dialog.
        /// </para>
        ///
        /// <para>
        /// <b>Example 1 - Validation only (no UI):</b>
        /// <code>
        /// if (!ValidationHelper.ValidateAll(model, validator))
        ///     return;  // Invalid, but user not notified
        /// </code>
        /// </para>
        ///
        /// <para>
        /// <b>Example 2 - Validation with error display:</b>
        /// <code>
        /// if (!ValidationHelper.ValidateAll(model, validator, Messages))
        ///     return;  // Invalid, user was notified
        /// </code>
        /// </para>
        /// </remarks>
        public static bool ValidateAll<T>(
            T model,
            IModelValidator<T> validator,
            IMessageService messageService = null,
            string errorTitle = "Validation Failed") where T : class
        {
            var errors = validator.ValidateAll(model);
            if (!errors.Any())
                return true;

            // Only show message if messageService was provided
            if (messageService != null)
            {
                var errorMessage = string.Join("\n", errors.Select(e => e.GetFormattedMessage()));
                messageService.ShowWarning(errorMessage, errorTitle);
            }

            return false;
        }

        /// <summary>
        /// Validates the model sequentially and optionally displays the first error.
        /// </summary>
        /// <typeparam name="T">The type of model to validate.</typeparam>
        /// <param name="model">The model instance to validate.</param>
        /// <param name="validator">The validator to use for validation.</param>
        /// <param name="messageService">
        /// Optional. If provided, displays the first validation error to the user.
        /// If null, validation occurs silently.
        /// </param>
        /// <param name="errorTitle">
        /// The title for the error message dialog. Default is "Validation Failed".
        /// Only used if messageService is provided.
        /// </param>
        /// <returns>
        /// true if the model is valid (no errors); otherwise, false.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs ValidateSequential() to find the first validation error
        /// (by Order), then optionally displays it. This is useful for:
        /// - Guiding users through fixing errors one at a time
        /// - Performance (stops at first error)
        /// - Preventing cascading errors
        /// </para>
        ///
        /// <para>
        /// <b>Example 1 - Validation only (no UI):</b>
        /// <code>
        /// if (!ValidationHelper.ValidateSequential(model, validator))
        ///     return;  // Invalid, but user not notified
        /// </code>
        /// </para>
        ///
        /// <para>
        /// <b>Example 2 - Validation with error display:</b>
        /// <code>
        /// if (!ValidationHelper.ValidateSequential(model, validator, Messages))
        ///     return;  // Invalid, first error shown to user
        /// </code>
        /// </para>
        ///
        /// <para>
        /// <b>Example 3 - Custom error title:</b>
        /// <code>
        /// if (!ValidationHelper.ValidateSequential(model, validator, Messages, "输入错误"))
        ///     return;
        /// </code>
        /// </para>
        /// </remarks>
        public static bool ValidateSequential<T>(
            T model,
            IModelValidator<T> validator,
            IMessageService messageService = null,
            string errorTitle = "Validation Failed") where T : class
        {
            var error = validator.ValidateSequential(model);
            if (error.IsValid)
                return true;

            // Only show message if messageService was provided
            if (messageService != null)
            {
                messageService.ShowWarning(error.GetFormattedMessage(), errorTitle);
            }

            return false;
        }

        /// <summary>
        /// Validates a single property and displays errors via IMessageService.
        /// </summary>
        /// <typeparam name="T">The type of model to validate.</typeparam>
        /// <param name="model">The model instance to validate.</param>
        /// <param name="propertyName">The name of the property to validate.</param>
        /// <param name="validator">The validator to use for validation.</param>
        /// <param name="messageService">The message service to display errors.</param>
        /// <param name="errorTitle">
        /// The title for the error message dialog. Default is "Validation Error".
        /// </param>
        /// <returns>
        /// true if the property is valid (no errors); otherwise, false.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is useful for real-time validation scenarios where you want
        /// to validate a property as the user types or changes focus.
        /// </para>
        ///
        /// <para>
        /// <b>Example Usage in Property Setter:</b>
        /// <code>
        /// public string Email
        /// {
        ///     set
        ///     {
        ///         _email = value;
        ///         ValidationHelper.ValidatePropertyAndShow(
        ///             this,
        ///             nameof(Email),
        ///             _validator,
        ///             _messageService);
        ///     }
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        // NOTE: ValidateProperty is not part of IModelValidator interface
        // If needed, use validator.ValidateAll() and filter by property name manually
    }
}
