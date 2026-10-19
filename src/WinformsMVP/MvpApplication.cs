using System;
using WinformsMVP.Configuration;
using WinformsMVP.Services;

namespace WinformsMVP
{
    /// <summary>
    /// The application's startup entry point for the framework (without M.E.DI): one call that
    /// customizes the framework and registers business services, then installs the result as
    /// <see cref="ServiceLocator.Current"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// MvpApplication.Configure(
    ///     framework: mvp =>
    ///     {
    ///         mvp.Messages.UseDialogs(new CompanyStyleDialogs());
    ///         mvp.Toasts.Duration = 4500;
    ///     },
    ///     services: reg =>
    ///     {
    ///         BillingModule.Register(reg);
    ///     });
    /// </code>
    /// </example>
    public static class MvpApplication
    {
        /// <summary>
        /// Builds a fresh provider: framework defaults, then <paramref name="framework"/>'s choices,
        /// then <paramref name="services"/> (a service registered there wins over a framework
        /// choice). Installs it as <see cref="ServiceLocator.Current"/> and returns it. A provider the
        /// framework built earlier (the lazy default or a previous configure) is disposed, so call
        /// this at startup before presenters or views resolve services.
        /// </summary>
        /// <param name="framework">Framework customization; omit to keep every default.</param>
        /// <param name="services">Business service registrations.</param>
        public static IServiceProvider Configure(
            Action<WinformsMvpBuilder> framework = null,
            Action<IServiceRegistry> services = null)
        {
            var provider = new DefaultServiceProvider();
            ServiceLocator.RegisterBuiltIns(provider);

            if (framework != null)
            {
                var builder = new WinformsMvpBuilder();
                framework(builder);
                builder.Apply(provider);
            }

            services?.Invoke(provider);
            ServiceLocator.Install(provider, owned: true);
            return provider;
        }
    }
}
