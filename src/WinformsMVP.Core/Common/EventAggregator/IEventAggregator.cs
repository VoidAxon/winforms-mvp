using System;

namespace WinformsMVP.Common.EventAggregator
{
    /// <summary>
    /// Event aggregator for decoupled pub-sub messaging between components.
    /// Thread-safe implementation suitable for production use.
    /// </summary>
    public interface IEventAggregator
    {
        /// <summary>
        /// Subscribes to messages of type T
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Handler to invoke when message is published</param>
        /// <returns>Subscription token for unsubscribing</returns>
        IDisposable Subscribe<T>(Action<T> handler);

        /// <summary>
        /// Subscribes to messages of type T with a filter predicate
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Handler to invoke when message is published</param>
        /// <param name="filter">Filter predicate - handler only invoked if returns true</param>
        /// <returns>Subscription token for unsubscribing</returns>
        IDisposable Subscribe<T>(Action<T> handler, Func<T, bool> filter);

        /// <summary>
        /// Publishes a message to all subscribers of type T
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="message">Message to publish</param>
        void Publish<T>(T message);

        /// <summary>
        /// Clears all subscriptions (useful for testing)
        /// </summary>
        void ClearSubscriptions();
    }
}
