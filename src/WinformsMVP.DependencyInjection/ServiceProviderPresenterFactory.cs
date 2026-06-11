using System;
using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;

namespace WinformsMVP.DependencyInjection
{
    /// <summary>
    /// <see cref="IPresenterFactory"/> implementation that delegates to an
    /// <see cref="IServiceProvider"/>. With this in place, a parent Presenter can ask
    /// for a child Presenter by type without ever referencing a specific container.
    /// </summary>
    public class ServiceProviderPresenterFactory : IPresenterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderPresenterFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Resolves a Presenter from the underlying <see cref="IServiceProvider"/>.
        /// Throws <see cref="InvalidOperationException"/> when the Presenter type is
        /// not registered — matching the standard
        /// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/> contract.
        /// </summary>
        public TPresenter Create<TPresenter>() where TPresenter : IPresenter
            => ServiceProviderServiceExtensions.GetRequiredService<TPresenter>(_serviceProvider);
    }
}
