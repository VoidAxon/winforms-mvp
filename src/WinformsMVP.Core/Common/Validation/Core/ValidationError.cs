using System;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// Represents a single validation error as a lightweight value type.
    /// Alternative to ModelValidationResult for simple error scenarios where
    /// immutability and minimal allocation are important.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ValidationError is a struct (value type) designed for:
    /// - Minimal memory allocation
    /// - Simple error representation
    /// - Collection scenarios (List&lt;ValidationError&gt;)
    /// </para>
    ///
    /// <para>
    /// <b>When to Use:</b>
    /// - Simple validation errors with single property name
    /// - Performance-critical scenarios
    /// - Collections of errors
    /// </para>
    ///
    /// <para>
    /// <b>When to Use ModelValidationResult Instead:</b>
    /// - Multiple member names per error
    /// - Need for reference equality
    /// - Integration with DataAnnotations APIs
    /// </para>
    /// </remarks>
    public struct ValidationError : IEquatable<ValidationError>
    {
        /// <summary>
        /// Gets the name of the property that failed validation.
        /// </summary>
        /// <value>
        /// The property name, or empty string if not specified.
        /// </value>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the error message describing the validation failure.
        /// </summary>
        /// <value>
        /// The error message, or empty string if not specified.
        /// </value>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the order of the validation attribute that produced this error.
        /// </summary>
        /// <value>
        /// The Order value from the OrderedValidationAttribute.
        /// </value>
        public int Order { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> struct.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property that failed validation, or null for no property.
        /// </param>
        /// <param name="errorMessage">
        /// The error message describing the validation failure, or null for empty message.
        /// </param>
        /// <param name="order">
        /// The order of the validation attribute. Default is 0.
        /// </param>
        public ValidationError(string propertyName, string errorMessage, int order = 0)
        {
            PropertyName = propertyName ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            Order = order;
        }

        /// <summary>
        /// Returns a formatted string representing this validation error.
        /// </summary>
        /// <returns>
        /// A string in the form "PropertyName: ErrorMessage" if property name is specified,
        /// or just "ErrorMessage" if no property name.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Examples:</b>
        /// <code>
        /// // With property name
        /// var error = new ValidationError("Email", "Invalid email format");
        /// error.ToString(); // "Email: Invalid email format"
        ///
        /// // Without property name
        /// var error = new ValidationError(null, "General validation error");
        /// error.ToString(); // "General validation error"
        /// </code>
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return string.IsNullOrEmpty(PropertyName)
                ? ErrorMessage
                : $"{PropertyName}: {ErrorMessage}";
        }

        /// <summary>
        /// Determines whether this validation error is equal to another.
        /// </summary>
        /// <param name="other">The validation error to compare with this instance.</param>
        /// <returns>
        /// true if PropertyName, ErrorMessage, and Order are all equal; otherwise, false.
        /// </returns>
        public bool Equals(ValidationError other)
        {
            return PropertyName == other.PropertyName &&
                   ErrorMessage == other.ErrorMessage &&
                   Order == other.Order;
        }

        /// <summary>
        /// Determines whether the specified object is equal to this validation error.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>
        /// true if obj is a ValidationError and is equal to this instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is ValidationError other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this validation error.
        /// </summary>
        /// <returns>
        /// A hash code computed from PropertyName, ErrorMessage, and Order.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (PropertyName?.GetHashCode() ?? 0);
                hash = hash * 23 + (ErrorMessage?.GetHashCode() ?? 0);
                hash = hash * 23 + Order.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two validation errors are equal.
        /// </summary>
        /// <param name="left">The first validation error to compare.</param>
        /// <param name="right">The second validation error to compare.</param>
        /// <returns>
        /// true if the two validation errors are equal; otherwise, false.
        /// </returns>
        public static bool operator ==(ValidationError left, ValidationError right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two validation errors are not equal.
        /// </summary>
        /// <param name="left">The first validation error to compare.</param>
        /// <param name="right">The second validation error to compare.</param>
        /// <returns>
        /// true if the two validation errors are not equal; otherwise, false.
        /// </returns>
        public static bool operator !=(ValidationError left, ValidationError right)
        {
            return !left.Equals(right);
        }
    }
}
