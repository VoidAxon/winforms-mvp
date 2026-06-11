using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Factory for ad-hoc disposables. The main use is turning a <c>+=</c> event subscription
    /// into something a <see cref="CompositeDisposable"/> can manage:
    /// <code>
    /// View.SelectionChanged += handler;
    /// Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);
    /// </code>
    /// </summary>
    public static class Disposable
    {
        /// <summary>Creates a disposable that invokes <paramref name="onDispose"/> at most once
        /// (zero times if never disposed).</summary>
        public static IDisposable Create(Action onDispose)
        {
            if (onDispose == null) throw new ArgumentNullException(nameof(onDispose));
            return new AnonymousDisposable(onDispose);
        }

        private sealed class AnonymousDisposable : IDisposable
        {
            private Action _onDispose;
            public AnonymousDisposable(Action onDispose) { _onDispose = onDispose; }

            public void Dispose()
            {
                var action = _onDispose;
                _onDispose = null;   // idempotent: the action runs at most once
                if (action != null) action();
            }
        }
    }
}
