using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Kibinago.Samples.ValidationDemo
{
    /// <summary>
    /// The registration data and its rules, as DataAnnotations. The presenter builds one from the
    /// view and hands it to <c>Validation.Validate</c> / <c>Validation.Check</c>; every error is
    /// reported under the property name, which the form maps to the control that edits it.
    /// </summary>
    /// <remarks>
    /// Rules that need more than one message per property (password complexity) or another
    /// property (password confirmation) use <see cref="CustomValidationAttribute"/> methods that
    /// return the error under <c>context.MemberName</c>. They are property-level on purpose:
    /// <c>IValidatableObject</c> only runs once every property rule passes, and
    /// <see cref="CompareAttribute"/> on .NET Framework reports its error without a member name
    /// (it would land on the form-level slot instead of the ConfirmPassword field).
    /// </remarks>
    public class RegistrationModel
    {
        [Required(ErrorMessage = "User name is required.")]
        [MinLength(3, ErrorMessage = "User name must be at least 3 characters.")]
        [MaxLength(50, ErrorMessage = "User name must not exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "User name can only contain letters, numbers, and underscores.")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [CustomValidation(typeof(RegistrationModel), nameof(HasUppercase))]
        [CustomValidation(typeof(RegistrationModel), nameof(HasLowercase))]
        [CustomValidation(typeof(RegistrationModel), nameof(HasDigit))]
        [CustomValidation(typeof(RegistrationModel), nameof(HasSpecialCharacter))]
        public string Password { get; set; }

        [Required(ErrorMessage = "Password confirmation is required.")]
        [CustomValidation(typeof(RegistrationModel), nameof(MatchesPassword))]
        public string ConfirmPassword { get; set; }

        /// <summary>Null when not entered (the age box shows 0).</summary>
        [Required(ErrorMessage = "Age is required.")]
        [Range(18, int.MaxValue, ErrorMessage = "You must be at least 18 years old.")]
        [CustomValidation(typeof(RegistrationModel), nameof(NotOver120))]
        public int? Age { get; set; }

        /// <summary>Optional. Accepts (123) 456-7890, 123-456-7890, 1234567890.</summary>
        [RegularExpression(@"^(\(\d{3}\)\s?|\d{3}[-.]?)?\d{3}[-.]?\d{4}$",
            ErrorMessage = "Phone number format is invalid. Use (123) 456-7890 or 123-456-7890.")]
        public string PhoneNumber { get; set; }

        // --- CustomValidation methods (public static, as the attribute requires) ---

        public static ValidationResult HasUppercase(string value, ValidationContext context)
            => MustContain(value, "[A-Z]", "Password must contain at least one uppercase letter.", context);

        public static ValidationResult HasLowercase(string value, ValidationContext context)
            => MustContain(value, "[a-z]", "Password must contain at least one lowercase letter.", context);

        public static ValidationResult HasDigit(string value, ValidationContext context)
            => MustContain(value, "[0-9]", "Password must contain at least one digit.", context);

        public static ValidationResult HasSpecialCharacter(string value, ValidationContext context)
            => MustContain(value, @"[!@#$%^&*(),.?""':{}|<>]", "Password must contain at least one special character.", context);

        public static ValidationResult MatchesPassword(string value, ValidationContext context)
        {
            var model = (RegistrationModel)context.ObjectInstance;
            return value == model.Password
                ? ValidationResult.Success
                : new ValidationResult("Passwords do not match.", new[] { context.MemberName });
        }

        public static ValidationResult NotOver120(int? value, ValidationContext context)
            => value > 120
                ? new ValidationResult("Age must not exceed 120 years.", new[] { context.MemberName })
                : ValidationResult.Success;

        // Empty values pass: [Required] reports them.
        private static ValidationResult MustContain(string value, string pattern, string message, ValidationContext context)
            => string.IsNullOrEmpty(value) || Regex.IsMatch(value, pattern)
                ? ValidationResult.Success
                : new ValidationResult(message, new[] { context.MemberName });
    }
}
