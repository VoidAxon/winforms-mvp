using System;
using Kibinago.Views;

namespace Kibinago.Services
{
    /// <summary>
    /// Shows that a view is busy. Presenters use it through <c>ShowBusy()</c>
    /// (<c>using (ShowBusy()) { ... }</c>); the WinForms implementation shows the wait cursor, keeps the
    /// view's window from taking input and, when configured (<c>mvp.Busy.UseOverlay()</c>), lays an overlay
    /// over the view. Replace it with <c>mvp.Busy.Use(...)</c> for a different presentation.
    /// </summary>
    public interface IBusyIndicator
    {
        /// <summary>
        /// Marks <paramref name="view"/> busy until the returned object is disposed. Requests may nest and
        /// overlap and end in any order; disposing twice does nothing.
        /// </summary>
        /// <param name="view">The busy view (a Form or a UserControl in the WinForms implementation).</param>
        /// <param name="message">A short text for overlays that show one, or <c>null</c>. The built-in overlay shows none.</param>
        /// <returns>Ends this request when disposed.</returns>
        IDisposable Begin(IView view, string message);
    }

    /// <summary>The Core default when no UI implementation is registered: shows nothing.</summary>
    internal sealed class NullBusyIndicator : IBusyIndicator
    {
        public static readonly NullBusyIndicator Instance = new NullBusyIndicator();

        private NullBusyIndicator() { }

        public IDisposable Begin(IView view, string message) => NoOp.Instance;

        private sealed class NoOp : IDisposable
        {
            public static readonly NoOp Instance = new NoOp();
            public void Dispose() { }
        }
    }
}
