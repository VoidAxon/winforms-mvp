using System;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Test implementation of <see cref="IDispatcherConfigurer"/> that delegates to an
    /// <see cref="Action{ViewActionDispatcher}"/>.  Used to replace the former
    /// <c>MockPlatformServices.ConfigureDispatcher</c> callback in tests.
    /// </summary>
    public sealed class DelegateDispatcherConfigurer : IDispatcherConfigurer
    {
        private readonly Action<ViewActionDispatcher> _configure;

        public DelegateDispatcherConfigurer(Action<ViewActionDispatcher> configure)
        {
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        }

        public void Configure(ViewActionDispatcher dispatcher) => _configure(dispatcher);
    }
}
