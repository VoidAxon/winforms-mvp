using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using WinformsMVP.Logging;

namespace WinformsMVP.Common.EventAggregator
{
    /// <summary>
    /// High-performance, thread-safe, and memory-friendly event aggregator.
    ///
    /// <para><b>Core Features:</b></para>
    /// <list type="bullet">
    ///   <item><b>Weak Reference Subscriptions:</b> Automatically cleans up when subscribers are GC'd, preventing memory leaks</item>
    ///   <item><b>UI Thread Marshaling:</b> Automatically marshals messages from background threads to UI thread (required for WinForms)</item>
    ///   <item><b>High Performance:</b> Uses expression tree compiled delegates to avoid reflection overhead (10-100x faster than reflection)</item>
    ///   <item><b>Exception Isolation:</b> One subscriber's exception doesn't affect others</item>
    ///   <item><b>Thread Safety:</b> Supports concurrent publishing and subscribing</item>
    /// </list>
    ///
    /// <para><b>⚠️ Weak Reference Considerations — pass an instance method, not a capturing lambda:</b></para>
    /// <para>
    /// Subscriptions hold the handler's target via a weak reference. Pass an <b>instance method</b>
    /// (e.g. <c>Subscribe&lt;T&gt;(OnProductAdded)</c>): the target is the Presenter itself, so the subscription
    /// lives as long as the Presenter is referenced as a field — the recommended pattern.
    /// </para>
    /// <para>
    /// Passing a lambda that captures <c>this</c> (e.g. <c>Subscribe&lt;T&gt;(msg =&gt; View.Update(msg))</c>) is a
    /// silent trap: the handler's target is a compiler-generated closure object that nothing else strongly
    /// references, so the next GC collects it and the subscription expires with no notification (the classic
    /// WPF <c>WeakEventManager</c> pitfall). A <c>filter</c> predicate is fine as a lambda — it is evaluated
    /// synchronously per message and is not retained as the subscription target.
    /// </para>
    ///
    /// <example>
    /// <b>Typical Usage (Presenter Subscription):</b>
    /// <code>
    /// public class OrderSummaryPresenter : ControlPresenterBase&lt;IOrderSummaryView&gt;
    /// {
    ///     private readonly IEventAggregator _eventAggregator;
    ///     private IDisposable _subscription;
    ///
    ///     public OrderSummaryPresenter(IEventAggregator eventAggregator)
    ///     {
    ///         _eventAggregator = eventAggregator;
    ///     }
    ///
    ///     protected override void OnViewAttached()
    ///     {
    ///         // ✅ Presenter has strong reference, subscription stays alive
    ///         _subscription = _eventAggregator.Subscribe&lt;ProductAddedMessage&gt;(OnProductAdded);
    ///     }
    ///
    ///     private void OnProductAdded(ProductAddedMessage msg)
    ///     {
    ///         // ✅ If message published from background thread, automatically marshaled to UI thread
    ///         View.AddItem(msg.Product, msg.Quantity);
    ///     }
    ///
    ///     protected override void Cleanup()
    ///     {
    ///         _subscription?.Dispose();  // Explicit cleanup (recommended)
    ///     }
    /// }
    /// </code>
    ///
    /// <b>Background Thread Publishing (Automatic UI Thread Marshaling):</b>
    /// <code>
    /// Task.Run(() =>
    /// {
    ///     var data = LoadDataFromDatabase();
    ///
    ///     // ✅ Background thread publishes, subscribers execute on UI thread
    ///     _eventAggregator.Publish(new DataLoadedMessage { Data = data });
    /// });
    /// </code>
    ///
    /// <b>Subscription with Filter (handler is an instance method; only the filter is a lambda):</b>
    /// <code>
    /// // Only receive messages for a specific product ID
    /// _subscription = _eventAggregator.Subscribe&lt;ProductUpdatedMessage&gt;(
    ///     OnProductUpdated,                                  // ✅ instance method — stays alive
    ///     filter: msg => msg.Product.Id == _targetProductId  // ✅ lambda filter is fine
    /// );
    ///
    /// private void OnProductUpdated(ProductUpdatedMessage msg) => View.UpdateProduct(msg.Product);
    /// </code>
    /// </example>
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, List<Subscription>> _subscriptions =
            new Dictionary<Type, List<Subscription>>();

        // Capture synchronization context at construction time (usually UI thread)
        private readonly SynchronizationContext _context;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates an EventAggregator instance.
        ///
        /// <para>
        /// <b>Important:</b> Must be created on the UI thread to capture the correct SynchronizationContext.
        /// Typically created in Program.Main() or main form constructor.
        /// </para>
        /// </summary>
        public EventAggregator() : this(null)
        {
        }

        /// <summary>
        /// Creates an EventAggregator instance with a logger factory.
        ///
        /// <para>
        /// Filter exceptions, handler exceptions, and UI-thread Post failures are reported through the
        /// logger created from <paramref name="loggerFactory"/>. Passing <c>null</c> falls back to
        /// <see cref="NullLoggerFactory.Instance"/>, preserving the existing silent behaviour.
        /// </para>
        /// </summary>
        /// <param name="loggerFactory">Logger factory used to create the aggregator's logger. May be null.</param>
        public EventAggregator(ILoggerFactory loggerFactory)
        {
            _context = SynchronizationContext.Current;
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(typeof(EventAggregator).FullName);
        }

        /// <summary>
        /// Logger used by subscriptions to report filter exceptions, handler exceptions, and Post failures.
        /// </summary>
        internal ILogger Logger => _logger;

        /// <summary>
        /// Subscribe to messages of the specified type.
        ///
        /// <para>
        /// Subscriptions use weak references; subscribers are automatically cleaned up when GC'd.
        /// The returned IDisposable can be used to manually unsubscribe.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Message handler (will execute on UI thread)</param>
        /// <returns>Subscription token; Dispose to unsubscribe</returns>
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            return Subscribe(handler, null);
        }

        /// <summary>
        /// Subscribe to messages of the specified type with a filter.
        ///
        /// <para>
        /// Subscriptions use weak references; subscribers are automatically cleaned up when GC'd.
        /// The returned IDisposable can be used to manually unsubscribe.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Message handler (will execute on UI thread)</param>
        /// <param name="filter">Filter predicate (executes on publisher's thread)</param>
        /// <returns>Subscription token; Dispose to unsubscribe</returns>
        public IDisposable Subscribe<T>(Action<T> handler, Func<T, bool> filter)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            // Create weak reference subscription
            var subscription = new WeakSubscription<T>(handler, filter, this, _context);

            lock (_lock)
            {
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
        /// Publish a message to all subscribers.
        ///
        /// <para>
        /// Can be called from any thread.
        /// </para>
        ///
        /// <para><b>Delivery semantics (important):</b></para>
        /// <list type="bullet">
        ///   <item><b>Same thread as the captured context</b> (typically publishing from the UI thread):
        ///   handlers run <b>synchronously</b> — they have all completed by the time <c>Publish</c> returns.</item>
        ///   <item><b>A different thread</b> (e.g. publishing from a background thread): each handler is
        ///   marshaled to the UI thread via <c>SynchronizationContext.Post</c> — <b>fire-and-forget</b>.
        ///   <c>Publish</c> returns immediately and the handlers run later, asynchronously, with no ordering
        ///   guarantee relative to the publisher. There is no synchronous <c>Send</c> option. Do not assume a
        ///   handler has finished (or its side effects are visible) just because <c>Publish</c> has returned.</item>
        ///   <item>If no <c>SynchronizationContext</c> was captured at construction, handlers run synchronously
        ///   on the publishing thread with no marshaling.</item>
        /// </list>
        ///
        /// <para>
        /// Dead subscriptions (subscribers already GC'd) are pruned lazily here, during publish. A message type
        /// that is subscribed often but published rarely may retain already-collected entries until its next
        /// publish; this is bounded and harmless. Dispose subscriptions explicitly for prompt removal.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="message">Message to publish</param>
        public void Publish<T>(T message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            List<Subscription> snapshot;

            // 1. Get snapshot (lock held for minimal time)
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(typeof(T), out var list) || list.Count == 0)
                    return;

                snapshot = list.ToList();
            }

            var deadSubscriptions = new List<Subscription>();

            // 2. Execute handlers (outside lock to avoid deadlocks)
            foreach (var sub in snapshot)
            {
                // Invoke returns false if object has been GC'd
                if (!sub.Invoke(message))
                {
                    deadSubscriptions.Add(sub);
                }
            }

            // 3. Clean up GC'd subscriptions (lazy cleanup)
            if (deadSubscriptions.Count > 0)
            {
                lock (_lock)
                {
                    if (_subscriptions.TryGetValue(typeof(T), out var list))
                    {
                        foreach (var dead in deadSubscriptions)
                        {
                            list.Remove(dead);
                        }
                        if (list.Count == 0)
                        {
                            _subscriptions.Remove(typeof(T));
                        }
                    }
                }
            }
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
        /// Clear all subscriptions (mainly for testing scenarios).
        ///
        /// <para>
        /// <b>Warning:</b> This method immediately clears all subscriptions and may cause unprocessed messages to be lost.
        /// Production environments should rely on automatic weak reference cleanup.
        /// </para>
        /// </summary>
        public void ClearSubscriptions()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }

        #region Internal Classes

        /// <summary>
        /// Subscription base class.
        /// </summary>
        internal abstract class Subscription : IDisposable
        {
            protected readonly EventAggregator _parent;
            public abstract Type MessageType { get; }

            /// <summary>
            /// Invoke the subscription handler.
            /// </summary>
            /// <returns>Returns false if subscriber is dead and needs cleanup</returns>
            public abstract bool Invoke(object message);

            protected Subscription(EventAggregator parent)
            {
                _parent = parent;
            }

            public void Dispose()
            {
                _parent.Unsubscribe(this);
            }
        }

        /// <summary>
        /// Weak reference subscription implementation.
        ///
        /// <para>
        /// Uses weak references to hold subscribers, avoiding memory leaks.
        /// Uses expression tree compiled delegates to avoid reflection overhead.
        /// Supports automatic UI thread marshaling.
        /// </para>
        /// </summary>
        internal class WeakSubscription<T> : Subscription
        {
            private readonly WeakReference _targetWeakReference;
            private readonly Func<T, bool> _filter;
            private readonly SynchronizationContext _syncContext;

            // Optimization: Compiled strongly-typed delegate, replaces reflection Invoke
            private readonly Action<object, T> _openHandler;

            public override Type MessageType => typeof(T);

            public WeakSubscription(Action<T> handler, Func<T, bool> filter,
                EventAggregator parent, SynchronizationContext context)
                : base(parent)
            {
                _filter = filter;
                _syncContext = context;

                if (handler.Target != null)
                {
                    // Instance method: Hold weak reference + compile open delegate
                    _targetWeakReference = new WeakReference(handler.Target);
                    _openHandler = CreateOpenDelegate(handler.Method);
                }
                else
                {
                    // Static method: No target object
                    _targetWeakReference = null;
                    _openHandler = (_, msg) => handler(msg);
                }
            }

            public override bool Invoke(object message)
            {
                // 1. First liveness check
                var target = _targetWeakReference?.Target;
                if (_targetWeakReference != null && target == null)
                {
                    return false; // Object has been GC'd
                }

                if (message is not T typedMessage)
                    return true;

                // 2. Execute filter (note: executes on publisher's thread)
                if (!ApplyFilter(typedMessage))
                {
                    return true; // Filtered out, consider successfully handled
                }

                // 3. Thread marshaling decision
                if (_syncContext != null && _syncContext != SynchronizationContext.Current)
                {
                    try
                    {
                        _syncContext.Post(_ =>
                        {
                            // 4. Second liveness check (handles object dying during queuing)
                            var currentTarget = _targetWeakReference?.Target;
                            if (_targetWeakReference != null && currentTarget == null)
                                return;

                            SafeExecute(currentTarget, typedMessage);
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
                    // Already on correct thread, execute directly
                    SafeExecute(target, typedMessage);
                }

                return true;
            }

            private bool ApplyFilter(T message)
            {
                if (_filter == null) return true;
                try
                {
                    return _filter(message);
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

            private void SafeExecute(object target, T message)
            {
                try
                {
                    _openHandler(target, message);
                }
                catch (Exception ex)
                {
                    // Exception isolation: Log but don't throw, preventing publish loop interruption.
                    _parent.Logger.LogError(
                        ex,
                        "EventAggregator: handler for message {MessageType} threw {ExceptionType}",
                        typeof(T).FullName,
                        ex.GetType().Name);
                }
            }

            /// <summary>
            /// Use expression trees to create high-performance open delegates, bypassing reflection overhead.
            ///
            /// <para>
            /// Compiled delegates are 10-100x faster than MethodInfo.Invoke().
            /// </para>
            /// </summary>
            private static Action<object, T> CreateOpenDelegate(MethodInfo method)
            {
                try
                {
                    // Dynamic methods or anonymous types may not have DeclaringType, fallback to reflection
                    if (method.DeclaringType == null)
                    {
                        return (target, msg) => method.Invoke(target, new object[] { msg });
                    }

                    var targetParam = Expression.Parameter(typeof(object), "target");
                    var messageParam = Expression.Parameter(typeof(T), "message");

                    // (Type)target
                    var targetCast = Expression.Convert(targetParam, method.DeclaringType);

                    // ((Type)target).Method(message)
                    var methodCall = Expression.Call(targetCast, method, messageParam);

                    // (target, message) => ((Type)target).Method(message)
                    return Expression.Lambda<Action<object, T>>(
                        methodCall, targetParam, messageParam
                    ).Compile();
                }
                catch (Exception)
                {
                    // Compilation failed (e.g., private method permission restrictions) fallback to reflection
                    return (target, msg) => method.Invoke(target, new object[] { msg });
                }
            }
        }

        #endregion
    }
}
