using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Optional global hook applied to every presenter's <see cref="ViewActionDispatcher"/> on
    /// first access. Register one in the service provider to install application-wide middleware
    /// (audit, authorization, telemetry) that must run for every dispatch in every presenter.
    /// When no <c>IDispatcherConfigurer</c> is registered, no global configuration is applied.
    /// This replaces the former <c>IPlatformServices.ConfigureDispatcher</c> callback.
    /// </summary>
    public interface IDispatcherConfigurer
    {
        void Configure(ViewActionDispatcher dispatcher);
    }
}
