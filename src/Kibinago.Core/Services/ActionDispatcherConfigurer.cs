using System;
using Kibinago.ViewActions;

namespace Kibinago.Services
{
    /// <summary>
    /// Adapts an <see cref="Action{ViewActionDispatcher}"/> to <see cref="IDispatcherConfigurer"/>.
    /// Register one to install application-wide dispatcher middleware without writing a class:
    /// <c>ServiceLocator.Configure(reg =&gt; reg.RegisterInstance&lt;IDispatcherConfigurer&gt;(new ActionDispatcherConfigurer(d =&gt; d.Use(...))));</c>
    /// </summary>
    public sealed class ActionDispatcherConfigurer : IDispatcherConfigurer
    {
        private readonly Action<ViewActionDispatcher> _configure;
        /// <summary>Creates a configurer that runs <paramref name="configure"/> on each presenter's dispatcher.</summary>
        /// <param name="configure">The configuration to apply, typically <c>d =&gt; d.Use(...)</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
        public ActionDispatcherConfigurer(Action<ViewActionDispatcher> configure)
            => _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        /// <inheritdoc />
        public void Configure(ViewActionDispatcher dispatcher) => _configure(dispatcher);
    }
}
