using System;
using System.Collections.Generic;
using System.Linq;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// Represents the result of a model validation operation with ordering metadata.
    /// The framework's own type, intentionally distinct from
    /// <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/> (no name collision),
    /// adding Order information for sequential validation support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ModelValidationResult is immutable and thread-safe. It can represent:
    /// - Success: ErrorMessage is null or empty
    /// - Failure: ErrorMessage contains description, MemberNames contains affected properties
    /// </para>
    ///
    /// <para>
    /// <b>Usage in Sequential Validation:</b>
    /// The Order property indicates which validation attribute produced this result,
    /// allowing validators to stop at the first error in sequence.
    /// </para>
    /// </remarks>
    public class ModelValidationResult
    {
        /// <summary>
        /// Represents a successful validation result.
        /// </summary>
        public static readonly ModelValidationResult Success = new ModelValidationResult(null, null, 0);

        /// <summary>
        /// Gets the error message for this validation result.
        /// </summary>
        /// <value>
        /// The error message describing the validation failure,
        /// or null/empty if validation succeeded.
        /// </value>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the collection of member names that failed validation.
        /// </summary>
        /// <value>
        /// A collection of property or field names that failed validation.
        /// Empty collection if no specific members were identified.
        /// </value>
        public IEnumerable<string> MemberNames { get; }

        /// <summary>
        /// Gets the order of the validation attribute that produced this result.
        /// </summary>
        /// <value>
        /// The Order value from the OrderedValidationAttribute that generated this result.
        /// Lower values indicate earlier validation execution.
        /// </value>
        public int Order { get; }

        /// <summary>
        /// Gets a value indicating whether this result represents a successful validation.
        /// </summary>
        /// <value>
        /// true if validation succeeded (no error message); otherwise, false.
        /// </value>
        public bool IsValid => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelValidationResult"/> class.
        /// </summary>
        /// <param name="errorMessage">
        /// The error message describing the validation failure, or null for success.
        /// </param>
        /// <param name="memberNames">
        /// The collection of member names that failed validation.
        /// Pass null for empty collection.
        /// </param>
        /// <param name="order">
        /// The order of the validation attribute that produced this result.
        /// </param>
        public ModelValidationResult(string errorMessage, IEnumerable<string> memberNames, int order)
        {
            ErrorMessage = errorMessage;
            MemberNames = memberNames ?? Enumerable.Empty<string>();
            Order = order;
        }

        /// <summary>
        /// Gets a formatted error message that includes the property name.
        /// </summary>
        /// <returns>
        /// A formatted string in the form "PropertyName: ErrorMessage",
        /// or just "ErrorMessage" if no property name is available.
        /// Returns empty string if validation succeeded.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Examples:</b>
        /// <code>
        /// // With property name
        /// "Email: Invalid email address format"
        ///
        /// // Without property name
        /// "Validation failed"
        ///
        /// // Success
        /// ""
        /// </code>
        /// </para>
        /// </remarks>
        public string GetFormattedMessage()
        {
            if (IsValid)
                return string.Empty;

            var memberName = MemberNames.FirstOrDefault();
            if (string.IsNullOrEmpty(memberName))
                return ErrorMessage;

            return $"{memberName}: {ErrorMessage}";
        }

        /// <summary>
        /// Returns a string that represents the current validation result.
        /// </summary>
        /// <returns>
        /// The formatted error message, or "Success" if validation passed.
        /// </returns>
        public override string ToString()
        {
            return IsValid ? "Success" : GetFormattedMessage();
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current validation result.
        /// </summary>
        /// <param name="obj">The object to compare with the current result.</param>
        /// <returns>
        /// true if the specified object is a ModelValidationResult with the same ErrorMessage;
        /// otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj is ModelValidationResult other)
            {
                return ErrorMessage == other.ErrorMessage;
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for this validation result.
        /// </summary>
        /// <returns>
        /// A hash code based on the error message.
        /// </returns>
        public override int GetHashCode()
        {
            return ErrorMessage?.GetHashCode() ?? 0;
        }
    }
}
