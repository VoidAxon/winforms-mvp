using System.Collections.Generic;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// View-side sink for validation errors, implemented by the view layer (the WinForms
    /// package ships <c>ErrorProviderDisplay</c>). The presenter's <see cref="ValidationState"/>
    /// pushes every change here; the display decides which fields to show and how.
    /// </summary>
    public interface IValidationErrorDisplay
    {
        /// <summary>
        /// Replaces what is displayed. <paramref name="errors"/> holds every current error by field
        /// name (<see cref="ValidationState.FormLevel"/> for errors not tied to a field); a field
        /// that is absent has no error. When <paramref name="revealAll"/> is false the display shows
        /// only fields the user has already visited; when true (after a submit attempt) it shows all.
        /// </summary>
        void ShowErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, bool revealAll);
    }
}
