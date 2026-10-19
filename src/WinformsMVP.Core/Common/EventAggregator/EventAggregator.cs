using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WinformsMVP.Logging;

namespace WinformsMVP.Common.EventAggregator
{
    /// <summary>
    /// Thread-safe publish/subscribe hub for cross-presenter messaging.
    ///
    /// <para><b>Core Features:</b></para>
    /// <list type="bullet">
    ///   <item><b>Explicit lifetime:</b> a subscription lives until its token is disposed — register it
    ///   with <c>.DisposeWith(Disposables)</c> so it ends with the presenter. Any handler shape works
    ///   (method group, capturing lambda); nothing is collected behind your back.</item>
    ///   <item><b>UI Thread Marshaling:</b> messages published from a background thread are posted to the
    ///   subscriber's UI thread (required for WinForms)</item>
    ///   <item><b>Exception Isolation:</b> one subscriber's exception doesn't affect others</item>
    ///   <item><b>Thread Safety:</b> supports concurrent publishing and subscribing</item>
    /// </list>
    ///
    /// <para>
    /// Subscriptions are strong references: a token that is never disposed keeps its handler (and
    /// whatever the handler captures, typically the presenter) alive for the aggregator's lifetime.
    /// Analyzer MVP017 warns when a <c>Subscribe</c> result is discarded.
    /// </para>
    ///
    /// <example>
    /// <b>Typical Usage (Presenter Subscription):</b>
    /// <code>
    /// public class OrderSummaryPresenter : ControlPresenterBase&lt;IOrderSummaryView&gt;
    /// {
    ///     private readonly IEventAggregator _events;
    ///
    ///     public OrderSummaryPresenter(IEventAggregator events) { _events = events; }
    ///
    ///     protected override void OnViewAttached()
    ///     {
    ///         _events.Subscribe&lt;ProductAddedMessage&gt;(OnProductAdded).DisposeWith(Disposables);
    ///
    ///         var productId = _targetProductId;            // capturing lambdas are fine
    ///         _events.Subscribe&lt;ProductUpdatedMessage&gt;(
    ///             msg =&gt; View.UpdateProduct(msg.Product),
    ///             filter: msg =&gt; msg.Product.Id == productId).DisposeWith(Disposables);
    ///     }
    ///
    ///     // Runs on the UI thread even when the message was published from a worker.
    ///     private void OnProductAdded(ProductAddedMessage msg) =&gt; View.AddItem(msg.Product, msg.Quantity);
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public class EventAggregator : IEventAggregator, IDisposable
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, List<Subscription>> _subscriptions =
            new Dictionary<Type, List<Subscription>>();

        private readonly ILogger _logger;
        private volatile bool _disposed;

        /// <summary>
        /// Creates an EventAggregator instance. It can be created on any thread: each subscription
        /// captures the <see cref="SynchronizationContext"/> of the thread that calls
        /// <c>Subscribe</c> — normally a presenter on the UI thread.
        /// </summary>
        public EventAggregator() : this(null)
        {
        }

        /// <summary>
        /// Creates an EventAggregator instance with a logger factory, used to report handler and
        /// filter exceptions (which are isolated and never propagate to the publisher).
        /// </summary>
        public EventAggregator(ILoggerFactory loggerFactory)
        {
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(typeof(EventAggregator).FullName);
        }

        internal ILogger Logger => _logger;

        /// <inheritdoc />
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            return Subscribe(handler, null);
        }

        /// <inheritdoc />
        public IDisposable Subscribe<T>(Action<T> handler, Func<T, bool> filter)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_disposed) throw new ObjectDisposedException(nameof(EventAggregator));

            var subscription = new Subscription<T>(handler, filter, this, SynchronizationContext.Current);

            lock (_lock)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    throw new ObjectDisposedException(nameof(EventAggregator));
                }
                var messageType = typeof(T);
                if (!_subscriptions.TryGetValue(messageType, out var list))
                {
                    list = new List<Subscription>();
                    _subscriptions[messageType] = list;
                }
                list.Add(subscription);
            }

            return subscription;
        }

        /// <summary>
        /// Publishes a message to all subscribers of type <typeparamref name="T"/>.
        ///
        /// <para><b>Delivery semantics (important):</b></para>
        /// <list type="bullet">
        ///   <item><b>Same thread as the subscriber's context</b> (typically publishing from the UI thread):
        ///   handlers run <b>synchronously</b> — they have all completed by the time <c>Publish</c> returns.</item>
        ///   <item><b>A different thread</b> (e.g. publishing from a background thread): each handler is
        ///   marshaled to the UI thread via <c>SynchronizationContext.Post</c> — <b>fire-and-forget</b>.
        ///   <c>Publish</c> returns immediately and the handlers run later, asynchronously, with no ordering
        ///   guarantee relative to the publisher. There is no synchronous <c>Send</c> option. Do not assume a
        ///   handler has finished (or its side effects are visible) just because <c>Publish</c> has returned.</item>
        ///   <item>The target context is the one current when the handler subscribed. With none, handlers
        ///   run synchronously on the publishing thread.</item>
        ///   <item>A subscription disposed after a message was posted to it does not receive that message.</item>
        /// </list>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="message">Message to publish</param>
        public void Publish<T>(T message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            List<Subscription> snapshot;

            // Snapshot under the lock; handlers run outside it to avoid deadlocks and to let them
            // subscribe / unsubscribe freely.
            lock (_lock)
            {
                if (_disposed) return;
                if (!_subscriptions.TryGetValue(typeof(T), out var list) || list.Count == 0)
                    return;

                snapshot = list.ToList();
            }

            foreach (var sub in snapshot)
                sub.Deliver(message);
        }

        /// <summary>
        /// Unsubscribe (internal use).
        /// </summary>
        internal void Unsubscribe(Subscription subscription)
        {
            lock (_lock)
            {
                var type = subscription.MessageType;
                if (_subscriptions.TryGetValue(type, out var list))
                {
                    list.Remove(subscription);
                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(type);
                    }
                }
            }
        }

        /// <summary>
        /// Shuts the aggregator down: every subscription is disposed (queued deliveries are
        /// dropped, handlers released), later <see cref="Publish{T}"/> calls deliver nothing, and
        /// <see cref="Subscribe{T}(Action{T}, Func{T, bool})"/> throws <see cref="ObjectDisposedException"/>.
        /// The service provider that created the aggregator calls this when it is disposed.
        /// Idempotent.
        /// </summary>
        public void Dispose()
        {
            List<Subscription> all;
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                all = _subscriptions.Values.SelectMany(l => l).ToList();
                _subscriptions.Clear();
            }
            foreach (var subscription in all) subscription.Dispose();
        }

        #region Internal Classes

        internal abstract class Subscription : IDisposable
        {
            protected readonly EventAggregator _parent;

            // Set by Dispose; checked again inside a posted callback so a message queued before the
            // subscriber was disposed (e.g. its window closed) is not delivered to it afterwards.
            private volatile bool _disposed;
            protected bool IsDisposed => _disposed;

            public abstract Type MessageType { get; }

            public abstract void Deliver(object message);

            protected Subscription(EventAggregator parent)
            {
                _parent = parent;
            }

            public void Dispose()
            {
                _disposed = true;
                _parent.Unsubscribe(this);
                ReleaseHandler();
            }

            // A caller may keep the disposed token around; it must not keep the subscriber alive.
            protected abstract void ReleaseHandler();
        }

        internal sealed class Subscription<T> : Subscription
        {
            private Action<T> _handler;      // cleared on Dispose
            private Func<T, bool> _filter;   // cleared on Dispose
            private readonly SynchronizationContext _syncContext;

            public override Type MessageType => typeof(T);

            public Subscription(Action<T> handler, Func<T, bool> filter,
                EventAggregator parent, SynchronizationContext context)
                : base(parent)
            {
                _handler = handler;
                _filter = filter;
                _syncContext = context;
            }

            protected override void ReleaseHandler()
            {
                Volatile.Write(ref _handler, null);
                Volatile.Write(ref _filter, null);
            }

            public override void Deliver(object message)
            {
                if (IsDisposed || !(message is T typedMessage))
                    return;

                // Filter runs on the publisher's thread.
                if (!ApplyFilter(typedMessage))
                    return;

                if (_syncContext != null && _syncContext != SynchronizationContext.Current)
                {
                    try
                    {
                        _syncContext.Post(_ =>
                        {
                            if (IsDisposed) return;
                            SafeExecute(typedMessage);
                        }, null);
                    }
                    catch (Exception ex)
                    {
                        _parent.Logger.LogError(
                            ex,
                            "EventAggregator: SynchronizationContext.Post failed for message {MessageType}",
                            typeof(T).FullName);
                    }
                }
                else
                {
                    SafeExecute(typedMessage);
                }
            }

            private bool ApplyFilter(T message)
            {
                var filter = Volatile.Read(ref _filter);
                if (filter == null) return !IsDisposed;
                try
                {
                    return filter(message);
                }
                catch (Exception ex)
                {
                    _parent.Logger.LogError(
                        ex,
                        "EventAggregator: filter for message {MessageType} threw {ExceptionType}. " +
                        "Treating as filtered out.",
                        typeof(T).FullName,
                        ex.GetType().Name);
                    return false; // Filter error should not trigger handler
                }
            }

            private void SafeExecute(T message)
            {
                var handler = Volatile.Read(ref _handler);
                if (handler == null) return;   // disposed concurrently
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    // Exception isolation: log but don't throw, preventing publish loop interruption.
                    _parent.Logger.LogError(
                        ex,
                        "EventAggregator: handler for message {MessageType} threw {ExceptionType}",
                        typeof(T).FullName,
                        ex.GetType().Name);
                }
            }
        }

        #endregion
    }
}
