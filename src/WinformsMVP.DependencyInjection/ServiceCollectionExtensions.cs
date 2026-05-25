using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WinformsMVP.Services;

namespace WinformsMVP.DependencyInjection
{
    /// <summary>
    /// Fluent helpers for wiring the WinformsMVP framework into a
    /// <see cref="IServiceCollection"/> at application startup.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the framework's own services so that Presenters can ask for
        /// <see cref="IPresenterFactory"/> and <see cref="IViewMappingRegister"/> via
        /// constructor injection.
        /// </summary>
        /// <param name="services">The container under construction.</param>
        /// <param name="viewMappingRegister">
        /// The shared View registry. Registered as a singleton instance so that the
        /// same map is visible to <c>WindowNavigator</c> and any code that queries it.
        /// </param>
        /// <returns><paramref name="services"/> for fluent chaining.</returns>
        /// <remarks>
        /// Uses <c>TryAdd</c> semantics — host applications that want to swap in a
        /// custom <see cref="IPresenterFactory"/> can register their own implementation
        /// before calling this method.
        /// </remarks>
        public static IServiceCollection AddWinformsMVP(
            this IServiceCollection services,
            IViewMappingRegister viewMappingRegister)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (viewMappingRegister == null) throw new ArgumentNullException(nameof(viewMappingRegister));

            services.TryAddSingleton(viewMappingRegister);
            services.TryAddSingleton<IPresenterFactory, ServiceProviderPresenterFactory>();

            return services;
        }

        /// <summary>
        /// Invokes both <c>RegisterViews</c> and <c>RegisterServices</c> on every
        /// supplied <see cref="IModuleRegistrar"/>, threading the same
        /// <see cref="IViewMappingRegister"/> and <see cref="IServiceCollection"/>
        /// through each module.
        /// </summary>
        /// <returns><paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="services"/>, <paramref name="viewMappingRegister"/>,
        /// or <paramref name="modules"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="modules"/> contains a null element. Misconfigured
        /// startup wiring should fail loudly rather than skipping silently.
        /// </exception>
        public static IServiceCollection RegisterModules(
            this IServiceCollection services,
            IViewMappingRegister viewMappingRegister,
            params IModuleRegistrar[] modules)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (viewMappingRegister == null) throw new ArgumentNullException(nameof(viewMappingRegister));
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (module == null)
                {
                    throw new ArgumentException(
                        $"Modules array contains a null element at index {i}.",
                        nameof(modules));
                }

                module.RegisterViews(viewMappingRegister);
                module.RegisterServices(services);
            }

            return services;
        }
    }
}
