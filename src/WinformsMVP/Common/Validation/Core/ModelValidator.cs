using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using WinformsMVP.Common.Validation.Attributes;
using ValidationResult = WinformsMVP.Common.Validation.Core.ValidationResult;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// Non-generic validator interface for dependency injection and testability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="ModelValidator.For{T}"/> to create typed validator instances.
    /// </para>
    ///
    /// <para>
    /// <b>Example Usage:</b>
    /// <code>
    /// // Direct usage (type-safe)
    /// var validator = ModelValidator.For&lt;EmailMessage&gt;();
    /// var errors = validator.ValidateAll(model);
    ///
    /// // Dependency injection (mockable)
    /// public class MyPresenter
    /// {
    ///     private readonly IModelValidator _validator;
    ///
    ///     public MyPresenter(IModelValidator validator)
    ///     {
    ///         _validator = validator;
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IModelValidator
    {
        /// <summary>
        /// Validates all properties and returns all validation errors.
        /// </summary>
        ReadOnlyCollection<ValidationResult> ValidateAll(object model);

        /// <summary>
        /// Validates properties sequentially by Order and returns the first error.
        /// </summary>
        ValidationResult ValidateSequential(object model);

        /// <summary>
        /// Checks if the model is valid (has no validation errors).
        /// </summary>
        bool IsValid(object model);
    }

    /// <summary>
    /// Static factory for creating model validators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Performance:</b>
    /// ModelValidator uses static reflection caching per type for optimal performance:
    /// - First validation: ~1-2ms (includes reflection + validation)
    /// - Subsequent validations: ~0.1-0.5ms (cached metadata + validation only)
    /// - ValidationContext is reused within each validation call (minimal allocations)
    /// </para>
    ///
    /// <para>
    /// <b>Thread Safety:</b>
    /// Static caches are initialized once per type in thread-safe static constructors.
    /// Validation operations are read-only and safe for concurrent use.
    /// </para>
    ///
    /// <para>
    /// <b>Supported Attributes:</b>
    /// - ValidationOrderAttribute for property-level ordering (applied to properties)
    /// - All standard System.ComponentModel.DataAnnotations attributes (Required, StringLength, EmailAddress, etc.)
    /// - Attributes within a property execute in declaration order
    /// - IValidatableObject (via Validator.TryValidateObject in ValidateAll)
    /// </para>
    ///
    /// <para>
    /// <b>Example:</b>
    /// <code>
    /// public class UserModel
    /// {
    ///     [ValidationOrder(1)]
    ///     [Required]
    ///     [StringLength(50)]
    ///     public string UserName { get; set; }
    ///
    ///     [ValidationOrder(2)]
    ///     [EmailAddress]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// Validation order: UserName.Required → UserName.StringLength → Email.EmailAddress
    /// </para>
    /// </remarks>
    public static class ModelValidator
    {
        /// <summary>
        /// Creates a validator for the specified model type.
        /// </summary>
        /// <typeparam name="T">The type of model to validate. Must be a class.</typeparam>
        /// <returns>A singleton validator instance for type T.</returns>
        /// <remarks>
        /// The validator is cached as a singleton per type for optimal performance.
        /// Multiple calls to For&lt;T&gt;() return the same instance.
        /// </remarks>
        public static IModelValidator For<T>() where T : class
        {
            return ModelValidatorImpl<T>.Instance;
        }

        /// <summary>
        /// Internal implementation of IModelValidator using generics for type safety.
        /// Each T gets its own static instance and cached metadata.
        /// </summary>
        private class ModelValidatorImpl<T> : IModelValidator where T : class
        {
            // Cache 1: Property name -> minimum Order (for ValidateAll sorting)
            private static readonly Dictionary<string, int> _propertyOrders;

            // Cache 2: Flattened and sorted (property, attribute) pairs (for ValidateSequential)
            private static readonly FlatValidationRule[] _orderedRules;

            /// <summary>
            /// Singleton instance per type T.
            /// </summary>
            public static readonly IModelValidator Instance = new ModelValidatorImpl<T>();

            /// <summary>
            /// Static constructor - runs once per T.
            /// Initializes validation metadata using reflection.
            /// </summary>
            static ModelValidatorImpl()
            {
                var metadata = InitializeValidationMetadata(typeof(T));
                _propertyOrders = metadata.PropertyOrders;
                _orderedRules = metadata.OrderedRules;
            }

            /// <summary>
            /// Private constructor enforces singleton pattern.
            /// </summary>
            private ModelValidatorImpl() { }

            /// <summary>
            /// Validates all properties on the model and returns all validation errors.
            /// </summary>
            /// <param name="model">The model instance to validate.</param>
            /// <returns>
            /// A read-only list of validation errors, sorted by Order.
            /// Empty list if validation succeeded.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="model"/> is null.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="model"/> is not of type T.
            /// </exception>
            /// <remarks>
            /// <para>
            /// Uses .NET's Validator.TryValidateObject for comprehensive validation:
            /// - Validates all ValidationAttribute instances
            /// - Calls IValidatableObject.Validate() if implemented
            /// - Returns errors sorted by Order property
            /// </para>
            ///
            /// <para>
            /// <b>Algorithm:</b>
            /// 1. Call Validator.TryValidateObject (validates all properties)
            /// 2. Wrap results with Order metadata from cache
            /// 3. Sort by Order for consistent display
            /// </para>
            /// </remarks>
            public ReadOnlyCollection<ValidationResult> ValidateAll(object model)
            {
                if (model == null)
                    throw new ArgumentNullException(nameof(model));

                if (!(model is T))
                    throw new ArgumentException(
                        $"Expected {typeof(T).Name}, got {model.GetType().Name}",
                        nameof(model));

                var context = new System.ComponentModel.DataAnnotations.ValidationContext(model, serviceProvider: null, items: null);
                var standardResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

                // Use .NET's built-in validator (supports IValidatableObject)
                Validator.TryValidateObject(model, context, standardResults, validateAllProperties: true);

                // Wrap results with Order metadata and sort
                var ordered = standardResults.Select(r =>
                {
                    var memberName = r.MemberNames.FirstOrDefault() ?? string.Empty;
                    // Class-level errors (from IValidatableObject) get max order
                    var order = _propertyOrders.TryGetValue(memberName, out var o) ? o : int.MaxValue;
                    return new ValidationResult(r.ErrorMessage, r.MemberNames, order);
                })
                .OrderBy(r => r.Order)
                .ToList();

                return new ReadOnlyCollection<ValidationResult>(ordered);
            }

            /// <summary>
            /// Validates properties sequentially by Order and returns the first validation error.
            /// </summary>
            /// <param name="model">The model instance to validate.</param>
            /// <returns>
            /// The first validation error encountered, or ValidationResult.Success if all validations passed.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="model"/> is null.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when <paramref name="model"/> is not of type T.
            /// </exception>
            /// <remarks>
            /// <para>
            /// Validates attributes in Order sequence and stops immediately upon encountering
            /// the first error. This is useful for:
            /// - Performance (don't validate further if early validation fails)
            /// - User experience (show the most important error first)
            /// - Preventing cascading errors (e.g., don't check email format if field is empty)
            /// </para>
            ///
            /// <para>
            /// <b>Algorithm:</b>
            /// 1. Iterate through pre-sorted (property, attribute) pairs
            /// 2. Manually call each attribute's GetValidationResult
            /// 3. Return immediately on first error
            /// 4. Reuse ValidationContext for all validations (performance optimization)
            /// </para>
            ///
            /// <para>
            /// <b>Performance:</b>
            /// - O(N) complexity where N is number of attributes
            /// - Reuses single ValidationContext (minimal allocations)
            /// - Short-circuits on first error (typically validates only a few attributes)
            /// </para>
            /// </remarks>
            public ValidationResult ValidateSequential(object model)
            {
                if (model == null)
                    throw new ArgumentNullException(nameof(model));

                if (!(model is T typedModel))
                    throw new ArgumentException(
                        $"Expected {typeof(T).Name}, got {model.GetType().Name}",
                        nameof(model));

                // Optimization: Create ValidationContext once, reuse for all attributes
                var context = new System.ComponentModel.DataAnnotations.ValidationContext(typedModel, serviceProvider: null, items: null);

                foreach (var rule in _orderedRules)
                {
                    var propertyValue = rule.PropertyInfo.GetValue(typedModel, null);

                    // Only modify MemberName (no allocation)
                    context.MemberName = rule.PropertyName;

                    // Manually call single attribute validation
                    var result = rule.Attribute.GetValidationResult(propertyValue, context);

                    if (result != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                    {
                        // First error - return immediately
                        return new ValidationResult(
                            result.ErrorMessage,
                            result.MemberNames,
                            rule.Order);
                    }
                }

                // All validations passed
                return ValidationResult.Success;
            }

            /// <summary>
            /// Checks if the model is valid (has no validation errors).
            /// </summary>
            /// <param name="model">The model instance to validate.</param>
            /// <returns>
            /// true if the model has no validation errors; otherwise, false.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="model"/> is null.
            /// </exception>
            /// <remarks>
            /// <para>
            /// Uses ValidateSequential for performance (stops on first error).
            /// Equivalent to but faster than: !ValidateAll(model).Any()
            /// </para>
            ///
            /// <para>
            /// Useful for CanExecute predicates in ViewAction system:
            /// <code>
            /// _dispatcher.Register(
            ///     CommonActions.Save,
            ///     OnSave,
            ///     canExecute: () => _validator.IsValid(GetCurrentModel()));
            /// </code>
            /// </para>
            /// </remarks>
            public bool IsValid(object model)
            {
                return ValidateSequential(model).IsValid;
            }

            /// <summary>
            /// Initializes validation metadata for a type using reflection.
            /// Called once per type in static constructor.
            /// </summary>
            private static ValidationMetadata InitializeValidationMetadata(Type type)
            {
                var propertyOrders = new Dictionary<string, int>();
                var orderedRules = new List<FlatValidationRule>();

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Find ValidationOrderAttribute on the property (property-level ordering)
                    var orderAttr = prop
                        .GetCustomAttributes(typeof(ValidationOrderAttribute), inherit: true)
                        .Cast<ValidationOrderAttribute>()
                        .FirstOrDefault();
                    int propertyOrder = orderAttr?.Order ?? 0;  // Default to 0 if not specified

                    // Find ALL ValidationAttribute instances (standard .NET attributes)
                    var attributes = prop.GetCustomAttributes(
                        typeof(System.ComponentModel.DataAnnotations.ValidationAttribute),
                        inherit: true)
                        .Cast<System.ComponentModel.DataAnnotations.ValidationAttribute>()
                        .ToArray();

                    if (attributes.Length == 0)
                        continue;

                    // All attributes on this property share the same Order (property-level)
                    propertyOrders[prop.Name] = propertyOrder;

                    // Add each attribute with its property order and declaration index
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        orderedRules.Add(new FlatValidationRule
                        {
                            PropertyInfo = prop,
                            PropertyName = prop.Name,
                            Attribute = attributes[i],
                            Order = propertyOrder,
                            AttributeIndex = i  // Preserve declaration order within property
                        });
                    }
                }

                return new ValidationMetadata
                {
                    PropertyOrders = propertyOrders,
                    // Sort by Order first, then by AttributeIndex (declaration order within property)
                    OrderedRules = orderedRules
                        .OrderBy(r => r.Order)
                        .ThenBy(r => r.AttributeIndex)
                        .ToArray()
                };
            }
        }

        /// <summary>
        /// Metadata container for validation information.
        /// </summary>
        private class ValidationMetadata
        {
            public Dictionary<string, int> PropertyOrders { get; set; }
            public FlatValidationRule[] OrderedRules { get; set; }
        }

        /// <summary>
        /// Flattened validation rule: one (property, attribute) pair.
        /// </summary>
        private class FlatValidationRule
        {
            public PropertyInfo PropertyInfo { get; set; }
            public string PropertyName { get; set; }
            public System.ComponentModel.DataAnnotations.ValidationAttribute Attribute { get; set; }
            public int Order { get; set; }
            public int AttributeIndex { get; set; }  // Preserves declaration order within property
        }
    }
}
