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
        /// <remarks>
        /// Each presenter is resolved from its own <see cref="IServiceScope"/>, and the scope is
        /// disposed together with the presenter. Resolving from the root provider instead would make
        /// the container keep every transient (disposable) presenter — and its View and transient
        /// dependencies — alive until the application exits. Scoped services therefore live per
        /// presenter (per window).
        /// </remarks>
        public TPresenter Create<TPresenter>() where TPresenter : IPresenter
        {
            var scopeFactory = _serviceProvider.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
                return _serviceProvider.GetRequiredService<TPresenter>();   // non-scoping provider

            var scope = scopeFactory.CreateScope();
            try
            {
                var presenter = scope.ServiceProvider.GetRequiredService<TPresenter>();
                if (presenter is IPresenterResourceOwner owner)
                    owner.OwnUntilDisposed(scope);
                else
                    scope = null;   // custom IPresenter: cannot observe its disposal; leave the scope to the GC
                return presenter;
            }
            catch
            {
                scope?.Dispose();
                throw;
            }
        }
    }
}
