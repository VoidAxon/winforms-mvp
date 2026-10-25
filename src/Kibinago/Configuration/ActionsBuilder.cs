using System;
using System.Collections.Generic;
using Kibinago.ViewActions;
using Kibinago.Services;

namespace Kibinago.Configuration
{
    /// <summary>
    /// Global configuration applied to every presenter's view-action dispatcher (<c>mvp.Actions</c>),
    /// registered as one <see cref="IDispatcherConfigurer"/>.
    /// </summary>
    public sealed class ActionsBuilder
    {
        private readonly KibinagoBuilder _owner;
        private readonly List<Action<ViewActionDispatcher>> _steps = new List<Action<ViewActionDispatcher>>();

        internal ActionsBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Adds a configuration step (e.g. <c>d =&gt; d.Use(middleware)</c>). Steps run in the order added.</summary>
        public ActionsBuilder Configure(Action<ViewActionDispatcher> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _owner.EnsureNotBuilt();
            _steps.Add(configure);
            return this;
        }

        /// <summary>
        /// Makes wiring mistakes (unregistered key, wrong payload type) throw on dispatch instead of
        /// being logged and ignored. Typically enabled in Debug builds.
        /// </summary>
        public ActionsBuilder StrictValidation()
            => Configure(d => d.ValidationMode = DispatchValidationMode.Strict);

        internal IDispatcherConfigurer BuildConfigurer()
        {
            if (_steps.Count == 0) return null;
            var steps = _steps.ToArray();
            return new ActionDispatcherConfigurer(d =>
            {
                foreach (var step in steps) step(d);
            });
        }
    }
}
