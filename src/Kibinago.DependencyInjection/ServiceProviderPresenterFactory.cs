using System;
using Microsoft.Extensions.DependencyInjection;
using Kibinago.Presenters;
using Kibinago.Services;

namespace Kibinago.DependencyInjection
{
    /// <summary>
    /// <see cref="IPresenterFactory"/> implementation that delegates to an
    /// <see cref="IServiceProvider"/>. With this in place, a parent Presenter can ask
    /// for a child Presenter by type without ever referencing a specific container.
    /// </summary>
    public class ServiceProviderPresenterFactory : IPresenterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>Creates a factory that resolves presenters from <paramref name="serviceProvider"/>.</summary>
        /// <param name="serviceProvider">The container presenters (and their per-presenter scopes) come from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <c>null</c>.</exception>
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
        /// presenter (per window). The presenter's framework services (<c>Messages</c>,
        /// <c>Navigator</c>, <c>Logger</c>, ...) are resolved from that same scope, not from
        /// <c>ServiceLocator.Current</c>, so register them in the container (<c>AddKibinago</c>).
        /// A presenter that uses them in its constructor is rejected (use them from
        /// <c>OnViewAttached</c> / <c>OnInitialize</c>, or inject the service). This applies to
        /// presenters deriving from the framework presenter bases; other <see cref="IPresenter"/>
        /// implementations resolve from the provider directly.
        /// </remarks>
        public TPresenter Create<TPresenter>() where TPresenter : IPresenter
        {
            // A per-presenter scope needs a presenter that can own it (the framework presenter
            // bases). Anything else — a non-scoping provider, a custom IPresenter — resolves from the
            // provider as before, so the container stays responsible for disposing its dependencies.
            var scopeFactory = _serviceProvider.GetService<IServiceScopeFactory>();
            if (scopeFactory == null || !typeof(IPresenterResourceOwner).IsAssignableFrom(typeof(TPresenter)))
                return _serviceProvider.GetRequiredService<TPresenter>();

            var scope = scopeFactory.CreateScope();
            try
            {
                var presenter = scope.ServiceProvider.GetRequiredService<TPresenter>();
                // Framework services from the same scope as the constructor dependencies. A failure
                // (the presenter used them in its constructor) disposes the scope, and with it the
                // presenter the scope created.
                if (presenter is IPresenterServicesBinding binding)
                    binding.BindServices(scope.ServiceProvider);
                ((IPresenterResourceOwner)presenter).OwnUntilDisposed(scope);
                return presenter;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }
    }
}
