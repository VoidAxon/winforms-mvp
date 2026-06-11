using System;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Adapts an <see cref="Action{ViewActionDispatcher}"/> to <see cref="IDispatcherConfigurer"/>.
    /// Register one to install application-wide dispatcher middleware without writing a class:
    /// <c>ServiceLocator.Configure(reg =&gt; reg.RegisterInstance&lt;IDispatcherConfigurer&gt;(new ActionDispatcherConfigurer(d =&gt; d.Use(...))));</c>
    /// </summary>
    public sealed class ActionDispatcherConfigurer : IDispatcherConfigurer
    {
        private readonly Action<ViewActionDispatcher> _configure;
        public ActionDispatcherConfigurer(Action<ViewActionDispatcher> configure)
            => _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        public void Configure(ViewActionDispatcher dispatcher) => _configure(dispatcher);
    }
}
