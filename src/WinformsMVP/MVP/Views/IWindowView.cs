using System;
using System.Windows.Forms;
using WinformsMVP.Common.Events;

namespace WinformsMVP.Core.Views
{
    /// <summary>
    /// Interface for views that represent top-level windows (Forms / dialogs).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Closing flow:</b> The framework bridges WinForms <c>Form.FormClosing</c> to
    /// <see cref="OnClosing"/>, which Form implementations should forward to the
    /// <see cref="Closing"/> event. Presenters subscribe to <see cref="Closing"/> to
    /// decide whether the window may close (e.g. prompt on unsaved changes).
    /// </para>
    /// <para>
    /// <b>Form implementation pattern</b> (use explicit interface implementation so
    /// business code cannot call <see cref="OnClosing"/> by accident):
    /// </para>
    /// <code>
    /// public class MyForm : Form, IMyView
    /// {
    ///     public event EventHandler&lt;WindowClosingEventArgs&gt; Closing;
    ///     void IWindowView.OnClosing(WindowClosingEventArgs args)
    ///         => Closing?.Invoke(this, args);
    /// }
    /// </code>
    /// </remarks>
    public interface IWindowView : IActionableView, IWin32Window
    {
        bool IsDisposed { get; }
        void Activate();

        /// <summary>
        /// Raised when the window is about to close. Presenters subscribe to this event
        /// to perform dirty-data checks and optionally cancel the close by setting
        /// <see cref="WindowClosingEventArgs.Cancel"/> to <c>true</c>.
        /// </summary>
        event EventHandler<WindowClosingEventArgs> Closing;

        /// <summary>
        /// Framework contract method, invoked by <c>WindowNavigator</c> when the underlying
        /// Form is closing. Implementations should forward to the <see cref="Closing"/> event.
        /// Use an explicit interface implementation so this is not part of the View's public API.
        /// </summary>
        /// <param name="args">Event arguments. Set <see cref="WindowClosingEventArgs.Cancel"/>
        /// inside handlers to prevent the close.</param>
        void OnClosing(WindowClosingEventArgs args);
    }
}
