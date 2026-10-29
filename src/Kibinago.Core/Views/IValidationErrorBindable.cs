using Kibinago.Validation;

namespace Kibinago.Views
{
    /// <summary>
    /// A view that shows validation errors next to its controls. The framework connects the presenter's
    /// <c>Validation</c> state to <see cref="ValidationErrors"/> when the view is attached. Add it to a
    /// view interface when needed (<c>IOrderEditView : IWindowView, IValidationErrorBindable</c>);
    /// validation itself works without it (message-only screens).
    /// </summary>
    public interface IValidationErrorBindable
    {
        /// <summary>
        /// Shows the presenter's validation errors, e.g. an <c>ErrorProviderDisplay</c> mapping field
        /// names to controls.
        /// </summary>
        IValidationErrorDisplay ValidationErrors { get; }
    }
}
