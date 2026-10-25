using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kibinago.Common
{
    /// <summary>
    /// Convenience factory for <see cref="Debouncer"/> and <see cref="Throttler"/> — shorter than
    /// the constructors and, because the action is a parameter, a method group binds directly
    /// (no <c>(Action)</c> cast):
    /// <code>
    /// var search = RateLimit.Debounce(300, OnSearchSettled).DisposeWith(Disposables);
    /// textBox.TextChanged += (s, e) =&gt; search.Signal();
    /// </code>
    /// </summary>
    public static class RateLimit
    {
        /// <summary>Creates a <see cref="Debouncer"/> that fires <paramref name="action"/> once, <paramref name="milliseconds"/> after the last signal.</summary>
        public static Debouncer Debounce(int milliseconds, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            => new Debouncer(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext);

        /// <summary>Creates a <see cref="Debouncer"/> that fires <paramref name="action"/> once, <paramref name="delay"/> after the last signal.</summary>
        public static Debouncer Debounce(TimeSpan delay, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            => new Debouncer(delay, action, onError, synchronizationContext);

        /// <summary>Creates a <see cref="Throttler"/> that fires <paramref name="action"/> at most once per <paramref name="milliseconds"/> (leading + trailing).</summary>
        public static Throttler Throttle(int milliseconds, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            => new Throttler(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext);

        /// <summary>Creates a <see cref="Throttler"/> that fires <paramref name="action"/> at most once per <paramref name="interval"/> (leading + trailing).</summary>
        public static Throttler Throttle(TimeSpan interval, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            => new Throttler(interval, action, onError, synchronizationContext);

        /// <summary>Creates a <see cref="Debouncer"/> for an async action (<c>async () =&gt; ...</c>); without a token a stale run cannot be stopped early; use the token overload for that.</summary>
        public static Debouncer Debounce(int milliseconds, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Debouncer(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Debouncer"/> for an async action that takes a token: a newer fire cancels the run in flight (unless <paramref name="cancelPrevious"/> is false), disposal cancels it.</summary>
        public static Debouncer Debounce(int milliseconds, Func<CancellationToken, Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Debouncer(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Throttler"/> for an async action (<c>async () =&gt; ...</c>).</summary>
        public static Throttler Throttle(int milliseconds, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Throttler(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Throttler"/> for an async action that takes a token: a newer fire cancels the run in flight (unless <paramref name="cancelPrevious"/> is false), disposal cancels it.</summary>
        public static Throttler Throttle(int milliseconds, Func<CancellationToken, Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Throttler(TimeSpan.FromMilliseconds(milliseconds), action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Debouncer"/> for an async action that takes a token (see the millisecond overload).</summary>
        public static Debouncer Debounce(TimeSpan delay, Func<CancellationToken, Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Debouncer(delay, action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Debouncer"/> for an async action (<c>async () =&gt; ...</c>).</summary>
        public static Debouncer Debounce(TimeSpan delay, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Debouncer(delay, action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Throttler"/> for an async action that takes a token (see the millisecond overload).</summary>
        public static Throttler Throttle(TimeSpan interval, Func<CancellationToken, Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Throttler(interval, action, onError, synchronizationContext, cancelPrevious);

        /// <summary>Creates a <see cref="Throttler"/> for an async action (<c>async () =&gt; ...</c>).</summary>
        public static Throttler Throttle(TimeSpan interval, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            => new Throttler(interval, action, onError, synchronizationContext, cancelPrevious);
    }
}
