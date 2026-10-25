using System;
using Kibinago.Common.EventAggregator;

namespace Kibinago.Configuration
{
    /// <summary>The event aggregator (<c>mvp.Events</c>). A replacement must keep the
    /// <see cref="IEventAggregator"/> semantics.</summary>
    public sealed class EventsBuilder
    {
        private readonly KibinagoBuilder _owner;

        internal EventsBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Uses <paramref name="events"/> as the application's event aggregator.</summary>
        public EventsBuilder Use(IEventAggregator events)
        {
            _owner.SetInstance(typeof(IEventAggregator), events ?? throw new ArgumentNullException(nameof(events)));
            return this;
        }

        /// <summary>Like <see cref="Use(IEventAggregator)"/>, built on first use with access to other services.</summary>
        public EventsBuilder Use(Func<IServiceProvider, IEventAggregator> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IEventAggregator), sp => factory(sp));
            return this;
        }
    }
}
