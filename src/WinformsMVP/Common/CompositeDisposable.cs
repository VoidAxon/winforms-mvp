using System;
using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// A bag of <see cref="IDisposable"/>s released together: disposing the bag disposes every
    /// member in reverse insertion order ("last built, first torn down"). Designed for the
    /// presenter lifecycle — see <c>PresenterBase.Disposables</c> — but usable anywhere.
    /// </summary>
    /// <remarks>
    /// Not thread-safe by design: presenter subscriptions are created and released on the UI
    /// thread. Disposal is idempotent; adding to an already-disposed bag disposes the item
    /// immediately instead of silently leaking it. The names <c>CompositeDisposable</c> and
    /// <c>DisposeWith</c> intentionally mirror the Rx / ReactiveUI idiom (without taking a
    /// dependency); alias the type name if you co-import System.Reactive.
    /// </remarks>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();
        private bool _disposed;

        /// <summary>Adds a disposable to the bag. Null is ignored; if the bag is already
        /// disposed the item is disposed immediately.</summary>
        public void Add(IDisposable disposable)
        {
            if (disposable == null) return;
            if (_disposed) { disposable.Dispose(); return; }
            _items.Add(disposable);
        }

        /// <summary>Disposes all members in reverse insertion order. Idempotent.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _items[i].Dispose();
            }
            _items.Clear();
        }
    }
}
