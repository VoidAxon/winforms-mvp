using System;

namespace Kibinago.Events
{
    /// <summary>
    /// Pub-sub messaging between presenters / modules. The framework registers one
    /// <see cref="EventAggregator"/> per service provider; replace it by registering your own
    /// implementation (<c>mvp.Events.Use(...)</c> or a direct registration).
    /// </summary>
    /// <remarks>
    /// A replacement must keep these semantics, which presenters rely on:
    /// <list type="bullet">
    ///   <item>Subscriptions are held strongly until the returned token is disposed; disposing the
    ///   token stops delivery and releases the handler and filter.</item>
    ///   <item>Each subscription delivers on the <see cref="System.Threading.SynchronizationContext"/>
    ///   current when it subscribed (presenters subscribe on the UI thread): synchronously when
    ///   publishing on that context, otherwise posted (fire-and-forget). With no context, handlers run
    ///   on the publishing thread.</item>
    ///   <item>The filter runs on the publishing thread.</item>
    ///   <item>A throwing handler or filter never reaches the publisher or other subscribers.</item>
    ///   <item>A message queued for a subscription that is disposed before it runs is not delivered.</item>
    /// </list>
    /// </remarks>
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
    }
}
