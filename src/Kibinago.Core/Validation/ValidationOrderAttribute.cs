using System;

namespace Kibinago.Validation
{
    /// <summary>
    /// Moves a property in the screen's order of validation errors: the order of
    /// <c>ValidationState.ErrorsInOrder()</c> / <c>FirstError</c> (the message shown and the field
    /// focused after a failed submit) and of <c>IModelValidator.ValidateSequential</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usually not needed: without it, properties keep their declaration order (base class first),
    /// so declaring the input model's properties in screen order is enough. Use it when the screen
    /// differs from the declaration, or for a partial class split over files (whose order is
    /// undefined). Unnumbered properties count as 0; equal numbers keep the declaration order.
    /// </para>
    /// <code>
    /// public sealed class OrderInput : NotifyingObject
    /// {
    ///     [Required(ErrorMessage = "Enter the customer.")]
    ///     public string Customer { get; set; }          // 0: first
    ///
    ///     [ValidationOrder(1)]                           // shown below the lines on the screen
    ///     [Required(ErrorMessage = "Enter the delivery date.")]
    ///     public DateTime? DeliveryDate { get; set; }
    ///
    ///     [Required(ErrorMessage = "Enter the lines.")]
    ///     public string Lines { get; set; }              // 0: second
    /// }
    /// </code>
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
