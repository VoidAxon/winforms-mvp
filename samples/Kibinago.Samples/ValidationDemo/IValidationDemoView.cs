using System;
using Kibinago.Views;

namespace Kibinago.Samples.ValidationDemo
{
    /// <summary>
    /// Registration form demonstrating the standard validation flow. The view only exposes the
    /// entered values and one change event; errors reach it through
    /// <see cref="IValidatableView.ValidationErrors"/>, which the framework connects to the
    /// presenter's <c>Validation</c> state. The view decides how (ErrorProvider) and when (after
    /// the user leaves a field, or everything after Submit) errors are shown.
    /// </summary>
    public interface IValidationDemoView : IActionableView, IValidatableView
    {
        string UserName { get; set; }
        string Email { get; set; }
        string Password { get; set; }
        string ConfirmPassword { get; set; }

        /// <summary>0 when not entered.</summary>
        int Age { get; set; }

        string PhoneNumber { get; set; }

        /// <summary>Raised when any field value changes.</summary>
        event EventHandler DataChanged;
    }
}
