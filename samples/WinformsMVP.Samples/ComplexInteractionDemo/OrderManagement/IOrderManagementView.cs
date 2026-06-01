using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement
{
    /// <summary>
    /// View interface for the main order management window
    /// </summary>
    public interface IOrderManagementView : IWindowView
    {
        /// <summary>
        /// Gets or sets the status message
        /// </summary>
        string StatusMessage { get; set; }

        /// <summary>
        /// Gets or sets the current total amount
        /// </summary>
        decimal CurrentTotal { get; set; }

        /// <summary>
        /// Gets or sets whether the order can be saved
        /// </summary>
        bool CanSaveOrder { get; }

        /// <summary>
        /// Event raised when user requests to save the order
        /// </summary>
        event EventHandler SaveRequested;

        /// <summary>
        /// Shows a success message
        /// </summary>
        void ShowSuccessMessage(string message);

        /// <summary>
        /// Shows an error message
        /// </summary>
        void ShowErrorMessage(string message);
    }
}
