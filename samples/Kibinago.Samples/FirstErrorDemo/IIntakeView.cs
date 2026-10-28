using System;
using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.FirstErrorDemo
{
    /// <summary>
    /// "Message only" style: the view does not implement IValidatableView, so nothing is drawn next to
    /// the controls; a failed submit shows the first error and moves the focus there.
    /// </summary>
    public interface IIntakeView : IActionableView
    {
        IntakeModel ReadModel();

        /// <summary>Moves the focus to the control of a field (model property name).</summary>
        void FocusError(string field);

        /// <summary>For the demo: every current error, in the screen's order.</summary>
        void ShowErrors(IReadOnlyList<(string Field, string Message)> errors);
    }
}
