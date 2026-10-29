using System;
using Kibinago.ViewActions;

namespace Kibinago.Configuration
{
    /// <summary>
    /// Application-wide settings of every presenter's view-action dispatcher (<c>mvp.Actions</c>):
    /// the validation mode and the optional <see cref="IActionObserver"/>.
    /// </summary>
    public sealed class ActionsBuilder
    {
        private readonly KibinagoBuilder _owner;

        internal ActionsBuilder(KibinagoBuilder owner) { _owner = owner; }

        internal ActionDispatcherOptions Options { get; } = new ActionDispatcherOptions();

        /// <summary>
        /// Makes wiring mistakes (unregistered key, wrong payload type) throw on dispatch instead of
        /// being logged and ignored. Typically enabled in Debug builds.
        /// </summary>
        public ActionsBuilder StrictValidation()
        {
            _owner.EnsureNotBuilt();
            Options.ValidationMode = DispatchValidationMode.Strict;
            return this;
        }

        /// <summary>
        /// Reports every action handler that ran — what, with which payload, how long, how it ended — to
        /// <paramref name="observer"/> (operation logs, timing). One per application; a later call replaces
        /// it. Caller-owned: never disposed by the framework.
        /// </summary>
        /// <param name="observer">The observer.</param>
        public ActionsBuilder Observe(IActionObserver observer)
        {
            _owner.SetInstance(typeof(IActionObserver), observer ?? throw new ArgumentNullException(nameof(observer)));
            return this;
        }

        /// <summary>Like <see cref="Observe(IActionObserver)"/>, built on first use with access to other services.</summary>
        /// <param name="factory">Creates the observer (once; it must not return null).</param>
        public ActionsBuilder Observe(Func<IServiceProvider, IActionObserver> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IActionObserver), sp => factory(sp)
                ?? throw new InvalidOperationException("The action observer factory returned null."));
            return this;
        }
    }
}
