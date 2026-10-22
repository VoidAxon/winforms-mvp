using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Tells Core when a view is disposed. Core cannot see WinForms controls, so the WinForms package
    /// supplies the implementation (<c>Control.Disposed</c>); a view that is not a control is never
    /// disposed by the platform.
    /// </summary>
    internal interface IViewDisposalObserver
    {
        /// <summary>Calls <paramref name="onDisposed"/> once when <paramref name="view"/> is disposed.
        /// Disposing the returned subscription stops observing.</summary>
        IDisposable ObserveDisposal(IViewBase view, Action onDisposed);

        /// <summary>True when the view is disposed or being disposed.</summary>
        bool IsDisposed(IViewBase view);
    }

    internal static class ViewDisposal
    {
        // Found once by name, like the WinForms platform services; a no-op when the WinformsMVP
        // assembly is not deployed (presenter tests with plain fake views).
        private const string ObserverType =
            "WinformsMVP.Services.Implementations.ControlDisposalObserver, WinformsMVP";

        private static readonly Lazy<IViewDisposalObserver> Lazy = new Lazy<IViewDisposalObserver>(() =>
            Type.GetType(ObserverType, throwOnError: false)
                ?.GetField("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IViewDisposalObserver
            ?? NoOpObserver.Instance);

        internal static IViewDisposalObserver Observer => Lazy.Value;

        private sealed class NoOpObserver : IViewDisposalObserver
        {
            public static readonly NoOpObserver Instance = new NoOpObserver();
            public IDisposable ObserveDisposal(IViewBase view, Action onDisposed) => Disposable.Create(() => { });
            public bool IsDisposed(IViewBase view) => false;
        }
    }
}
