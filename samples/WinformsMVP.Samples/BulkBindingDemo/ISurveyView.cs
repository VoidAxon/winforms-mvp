using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.BulkBindingDemo
{
    /// <summary>
    /// View interface for survey/questionnaire demo.
    /// Demonstrates bulk binding for many RadioButtons.
    /// </summary>
    public interface ISurveyView : IWindowView
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
