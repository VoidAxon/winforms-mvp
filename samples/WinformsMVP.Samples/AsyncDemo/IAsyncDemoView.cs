using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.AsyncDemo
{
    /// <summary>
    /// Demonstrates async/await patterns in MVP presenters.
    /// Shows progress tracking, cancellation, and error handling.
    /// </summary>
    public interface IAsyncDemoView : IWindowView
    {
        /// <summary>
        /// Gets or sets the operation status message.
        /// </summary>
        string StatusMessage { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        int ProgressPercentage { get; set; }

        /// <summary>
        /// Shows or hides the progress bar.
        /// </summary>
        bool ShowProgress { get; set; }

        /// <summary>
        /// Gets or sets the result data list.
        /// </summary>
        IEnumerable<string> ResultData { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time display.
        /// </summary>
        string ElapsedTime { get; set; }

        /// <summary>
        /// Gets whether an operation is currently running.
        /// </summary>
        bool IsOperationRunning { get; set; }
    }
}
