using System;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Presenters
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
        // Found once by name, like the WinForms platform services; a no-op when the Kibinago
        // assembly is not deployed (presenter tests with plain fake views).
        private const string ObserverType =
            "Kibinago.Services.Implementations.ControlDisposalObserver, Kibinago";

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

    /// <summary>
    /// Which views a live embedded presenter (<c>AttachChild</c> or control <c>Connect</c>) uses, so two
    /// live presenters never share a view (they would overwrite and unbind each other's action binder).
    /// Weak keys: a forgotten view does not stay alive. UI-thread use; the table itself is thread-safe.
    /// </summary>
    internal static class ViewOwnership
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IViewBase, object> Live =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IViewBase, object>();

        internal static bool IsClaimed(IViewBase view) => Live.TryGetValue(view, out _);

        /// <summary>Claims <paramref name="view"/> for <paramref name="owner"/>; the returned subscription
        /// releases it (only if still claimed by that owner).</summary>
        internal static IDisposable Claim(IViewBase view, object owner)
        {
            if (IsClaimed(view))
                throw new InvalidOperationException(
                    "This view is already used by another live presenter; each presenter needs its own view.");
            Live.Add(view, owner);
            return Disposable.Create(() =>
            {
                if (Live.TryGetValue(view, out var current) && ReferenceEquals(current, owner)) Live.Remove(view);
            });
        }
    }
}
