using System;

namespace WinformsMVP.Common
{
    /// <summary>Fluent registration into a <see cref="CompositeDisposable"/>.</summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// Registers <paramref name="disposable"/> into <paramref name="bag"/> and returns it,
        /// so the lifetime is declared on the same line that creates the subscription.
        /// </summary>
        public static T DisposeWith<T>(this T disposable, CompositeDisposable bag) where T : IDisposable
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            bag.Add(disposable);
            return disposable;
        }
    }
}
