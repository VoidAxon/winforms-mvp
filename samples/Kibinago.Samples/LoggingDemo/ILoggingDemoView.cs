using Kibinago.Views;

namespace Kibinago.Samples.LoggingDemo
{
    public interface ILoggingDemoView : IControlView
    {
        string LogOutput { get; set; }
        string UserName { get; set; }
        int ProcessedCount { get; set; }
    }
}
