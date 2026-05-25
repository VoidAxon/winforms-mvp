using System;
using WinformsMVP.Services;

namespace WinformsMVP.Modules
{
    /// <summary>
    /// Fluent helpers for invoking a batch of <see cref="IViewModuleRegistrar"/>
    /// instances against a single <see cref="IViewMappingRegister"/>.
    /// </summary>
    public static class ModuleRegistrationExtensions
    {
        /// <summary>
        /// Calls <see cref="IViewModuleRegistrar.RegisterViews"/> on each module in order,
        /// passing the supplied <paramref name="registry"/>.
        /// </summary>
        /// <returns>The same <paramref name="registry"/> for fluent chaining (e.g. with
        /// <c>WithServiceProvider</c>).</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="registry"/> or <paramref name="modules"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="modules"/> contains a null element. Silent-skip is
        /// avoided so that misconfigured startup wiring fails loudly.
        /// </exception>
        public static IViewMappingRegister RegisterModules(
            this IViewMappingRegister registry,
            params IViewModuleRegistrar[] modules)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
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

                module.RegisterViews(registry);
            }

            return registry;
        }
    }
}
