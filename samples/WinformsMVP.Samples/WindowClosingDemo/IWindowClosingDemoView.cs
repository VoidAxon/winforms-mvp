using System;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.WindowClosingDemo
{
    /// <summary>
    /// Minimal view contract for the Window Closing demo.
    /// </summary>
    /// <remarks>
    /// Demonstrates the framework's two-direction close pattern with the smallest possible
    /// surface area. The view exposes:
    /// <list type="bullet">
    ///   <item><description>A <see cref="Text"/> property the user edits.</description></item>
    ///   <item><description>An <see cref="EditChanged"/> event so the Presenter can detect dirty state.</description></item>
    ///   <item><description>A <see cref="StatusMessage"/> setter the Presenter updates with feedback.</description></item>
    /// </list>
    /// The window-closing API (<see cref="IWindowView.Closing"/> / <see cref="IWindowView.OnClosing"/>)
    /// is inherited from <see cref="IWindowView"/>.
    /// </remarks>
    public interface IWindowClosingDemoView : IWindowView
    {
        string Text { get; set; }
        string StatusMessage { set; }

        /// <summary>
        /// Raised by the View whenever the user edits <see cref="Text"/>. The Presenter uses
        /// this signal to update its dirty-state flag.
        /// </summary>
        event EventHandler EditChanged;
    }
}
