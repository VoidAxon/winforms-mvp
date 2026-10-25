using Kibinago.ViewActions;

namespace Kibinago.Services
{
    /// <summary>
    /// Optional global hook applied to every presenter's <see cref="ViewActionDispatcher"/> on
    /// first access. Register one in the service provider to install application-wide middleware
    /// (audit, authorization, telemetry) that must run for every dispatch in every presenter.
    /// When no <c>IDispatcherConfigurer</c> is registered, no global configuration is applied.
    /// Register this interface in the service provider to apply global dispatcher configuration.
    /// </summary>
    public interface IDispatcherConfigurer
    {
        /// <summary>
        /// Configures a presenter's dispatcher, typically by adding middleware with
        /// <c>Use</c>. Called once per presenter, on the dispatcher's first use.
        /// </summary>
        /// <param name="dispatcher">The presenter's dispatcher.</param>
        void Configure(ViewActionDispatcher dispatcher);
    }
}
