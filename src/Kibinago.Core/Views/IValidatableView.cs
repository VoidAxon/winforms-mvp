using Kibinago.Common.Validation.Core;

namespace Kibinago.Views
{
    /// <summary>
    /// A view that displays validation errors. The presenter's <c>Validation</c> state is
    /// connected to <see cref="ValidationErrors"/> automatically when the view is attached, the
    /// same way <see cref="IActionableView.ActionBinder"/> is bound.
    /// </summary>
    public interface IValidatableView : IViewBase
    {
        IValidationErrorDisplay ValidationErrors { get; }
    }
}
