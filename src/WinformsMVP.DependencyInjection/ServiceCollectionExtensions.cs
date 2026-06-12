using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WinformsMVP.Logging;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

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
        /// <see cref="IPresenterFactory"/>, <see cref="IViewMappingRegister"/>, and
        /// the built-in platform services (<see cref="IMessageService"/>,
        /// <see cref="IDialogProvider"/>, <see cref="IFileService"/>,
        /// <see cref="ILoggerFactory"/>, <see cref="IWindowNavigator"/>) via the
        /// M.E.DI container.
        /// </summary>
        /// <param name="services">The container under construction.</param>
        /// <param name="viewMappingRegister">
        /// The shared View registry. Registered as a singleton instance so that the
        /// same map is visible to <c>WindowNavigator</c> and any code that queries it.
        /// </param>
        /// <returns><paramref name="services"/> for fluent chaining.</returns>
        /// <remarks>
        /// Uses <c>TryAdd</c> semantics — host applications that want to swap in a
        /// custom implementation can register their own before calling this method.
        /// Call <see cref="UseWinformsMVP"/> after <c>BuildServiceProvider()</c> to
        /// point <see cref="ServiceLocator.Current"/> at the built provider so that
        /// presenter convenience accessors resolve through M.E.DI.
        /// </remarks>
        public static IServiceCollection AddWinformsMVP(
            this IServiceCollection services,
            IViewMappingRegister viewMappingRegister)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (viewMappingRegister == null) throw new ArgumentNullException(nameof(viewMappingRegister));

            services.TryAddSingleton(viewMappingRegister);
            services.TryAddSingleton<IPresenterFactory, ServiceProviderPresenterFactory>();

            // Framework built-ins — host may register its own before calling AddWinformsMVP.
            services.TryAddSingleton<IMessageService, MessageService>();
            services.TryAddSingleton<IDialogProvider, DialogProvider>();
            services.TryAddSingleton<IFileService, FileService>();
            services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.TryAddSingleton<IWindowNavigator>(sp =>
                new WindowNavigator(
                    sp.GetRequiredService<IViewMappingRegister>().WithServiceProvider(sp)));

            return services;
        }

        /// <summary>
        /// Points the framework's <see cref="ServiceLocator"/> at this provider so that
        /// presenter convenience accessors (<c>Messages</c>, <c>Dialogs</c>,
        /// <c>Navigator</c>, ...) resolve through M.E.DI.
        /// Call once at startup after <c>BuildServiceProvider()</c>.
        /// </summary>
        /// <param name="provider">The built M.E.DI service provider.</param>
        /// <returns><paramref name="provider"/> for fluent chaining.</returns>
        public static IServiceProvider UseWinformsMVP(this IServiceProvider provider)
        {
            ServiceLocator.Current = provider ?? throw new ArgumentNullException(nameof(provider));
            return provider;
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
