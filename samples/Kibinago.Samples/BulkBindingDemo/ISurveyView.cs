using System;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.BulkBindingDemo
{
    /// <summary>
    /// View interface for survey/questionnaire demo.
    /// Demonstrates bulk binding for many RadioButtons.
    /// </summary>
    public interface ISurveyView : IActionableView
    {
        /// <summary>
        /// Update the status message
        /// </summary>
        void UpdateStatus(string message);

        /// <summary>
        /// Show survey results
        /// </summary>
        void ShowResults(string results);
    }
}
