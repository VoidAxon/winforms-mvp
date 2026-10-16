using System;

namespace WinformsMVP.Common.Validation.Attributes
{
    /// <summary>
    /// Specifies the validation order for a property.
    /// Used to control the sequence in which properties are validated during sequential validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ValidationOrderAttribute is applied at the property level to specify when that property
    /// should be validated relative to other properties. All validation attributes on the same
    /// property are executed in their declaration order.
    /// </para>
    ///
    /// <para>
    /// <b>Design Rationale:</b>
    /// Rather than wrapping every standard ValidationAttribute with an Order property,
    /// ValidationOrderAttribute provides property-level ordering. This approach:
    /// - Works directly with standard .NET ValidationAttribute instances
    /// - Reduces code duplication (no wrapper classes needed)
    /// - Is more intuitive (properties validated in Order sequence, attributes within property by declaration order)
    /// - Simpler to maintain (one attribute instead of many wrappers)
    /// </para>
    ///
    /// <para>
    /// <b>Example Usage:</b>
    /// <code>
    /// public class UserModel
    /// {
    ///     [ValidationOrder(1)]
    ///     [Required(ErrorMessage = "用户名必填")]
    ///     [StringLength(50, MinimumLength = 3)]
    ///     public string UserName { get; set; }
    ///
    ///     [ValidationOrder(2)]
    ///     [Required(ErrorMessage = "邮箱必填")]
    ///     [EmailAddress]
    ///     public string Email { get; set; }
    ///
    ///     [ValidationOrder(3)]
    ///     [Required(ErrorMessage = "密码必填")]
    ///     [StringLength(100, MinimumLength = 8)]
    ///     public string Password { get; set; }
    ///
    ///     [ValidationOrder(4)]
    ///     [Required(ErrorMessage = "确认密码必填")]
    ///     [Compare(nameof(Password))]
    ///     public string ConfirmPassword { get; set; }
    /// }
    /// </code>
    /// In this example:
    /// - Properties are validated in order: UserName (1) → Email (2) → Password (3) → ConfirmPassword (4)
    /// - Within UserName: Required → StringLength (declaration order)
    /// - Within Email: Required → EmailAddress (declaration order)
    /// </para>
    ///
    /// <para>
    /// <b>Default Order:</b>
    /// Properties without ValidationOrderAttribute have Order = 0 (validated first).
    /// Within the same Order, properties are validated in an undefined sequence.
    /// </para>
    ///
    /// <para>
    /// <b>Attribute Declaration Order:</b>
    /// Within a single property, validation attributes execute in the order they appear in code:
    /// <code>
    /// [ValidationOrder(1)]
    /// [Required]        // Executes first
    /// [StringLength(50)] // Executes second
    /// [EmailAddress]     // Executes third
    /// public string Email { get; set; }
    /// </code>
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ValidationOrderAttribute : Attribute
    {
        /// <summary>
        /// Gets the validation order for this property.
        /// Lower values are validated first.
        /// </summary>
        /// <value>
        /// The order value. Default is 0 if not specified.
        /// </value>
        public int Order { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationOrderAttribute"/> class
        /// with the specified order.
        /// </summary>
        /// <param name="order">
        /// The order in which this property should be validated.
        /// Lower values are validated first (e.g., 1 before 2).
        /// </param>
        /// <example>
        /// <code>
        /// [ValidationOrder(1)]
        /// [Required]
        /// public string UserName { get; set; }
        ///
        /// [ValidationOrder(2)]
        /// [EmailAddress]
        /// public string Email { get; set; }
        /// </code>
        /// UserName will be validated before Email.
        /// </example>
        public ValidationOrderAttribute(int order)
        {
            Order = order;
        }
    }
}
