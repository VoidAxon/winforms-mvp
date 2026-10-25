using System;
using System.Windows.Forms;
using Kibinago.Configuration;
using Kibinago.Services;

namespace Kibinago
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
    /// MvpApplication.Run(mainForm);   // Application.Run, then Shutdown()
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
            Action<KibinagoBuilder> framework = null,
            Action<IServiceRegistry> services = null)
        {
            var provider = new DefaultServiceProvider();
            ServiceLocator.RegisterBuiltIns(provider);

            if (framework != null)
            {
                var builder = new KibinagoBuilder();
                framework(builder);
                builder.Apply(provider);
            }

            services?.Invoke(provider);
            ServiceLocator.Install(provider, owned: true);
            return provider;
        }

        /// <summary>
        /// Runs the message loop for <paramref name="mainForm"/> (<see cref="Application.Run(Form)"/>)
        /// and calls <see cref="Shutdown"/> when it ends, so the framework's services (toast
        /// service, event aggregator, your factory-created singletons) are disposed at exit.
        /// </summary>
        public static void Run(Form mainForm)
        {
            if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
            try
            {
                Application.Run(mainForm);
            }
            finally
            {
                Shutdown();
            }
        }

        /// <summary>
        /// Disposes the provider installed by <see cref="Configure"/> (or the lazily created default)
        /// and clears <see cref="ServiceLocator.Current"/>. A provider you assigned yourself (e.g. an
        /// M.E.DI container via <c>UseKibinago</c>) is left alone — dispose it where you built it.
        /// </summary>
        public static void Shutdown() => ServiceLocator.Install(null, owned: false);
    }
}
