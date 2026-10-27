using System;
using Kibinago.Services;

namespace Kibinago.Configuration
{
    /// <summary>
    /// Busy indicator settings (<c>mvp.Busy</c>). By default a busy view shows the wait cursor and its window
    /// takes no input; <see cref="UseOverlay()"/> also covers the view with an overlay.
    /// </summary>
    public sealed class BusyBuilder
    {
        private readonly KibinagoBuilder _owner;
        internal readonly BusyOptions Options = new BusyOptions();

        internal BusyBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Milliseconds before the overlay appears. See <see cref="BusyOptions.Delay"/>.</summary>
        public int Delay
        {
            get => Options.Delay;
            set { _owner.EnsureNotBuilt(); Options.Delay = value; }
        }

        /// <summary>Covers busy views with the built-in overlay (<see cref="HudBusyRenderer"/>: a white square with a ring in the middle).</summary>
        public BusyBuilder UseOverlay() => UseOverlay(new HudBusyRenderer());

        /// <summary>Covers busy views with an overlay painted by <paramref name="renderer"/>.</summary>
        public BusyBuilder UseOverlay(BusyRenderer renderer)
        {
            _owner.SetInstance(typeof(BusyRenderer), renderer ?? throw new ArgumentNullException(nameof(renderer)));
            return this;
        }

        /// <summary>Like <see cref="UseOverlay(BusyRenderer)"/>, built on first use with access to other services.</summary>
        public BusyBuilder UseOverlay(Func<IServiceProvider, BusyRenderer> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(BusyRenderer), sp => factory(sp));
            return this;
        }

        /// <summary>Replaces the whole busy indicator (a status-bar spinner, a third-party overlay, …).</summary>
        public BusyBuilder Use(IBusyIndicator indicator)
        {
            _owner.SetInstance(typeof(IBusyIndicator), indicator ?? throw new ArgumentNullException(nameof(indicator)));
            return this;
        }

        /// <summary>Like <see cref="Use(IBusyIndicator)"/>, built on first use with access to other services.</summary>
        public BusyBuilder Use(Func<IServiceProvider, IBusyIndicator> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IBusyIndicator), sp => factory(sp));
            return this;
        }
    }
}
