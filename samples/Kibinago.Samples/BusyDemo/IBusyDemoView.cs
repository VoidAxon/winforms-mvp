using Kibinago.Views;

namespace Kibinago.Samples.BusyDemo
{
    /// <summary>Three buttons that each run a few seconds of work, and a line for the outcome.</summary>
    public interface IBusyDemoView : IControlView
    {
        /// <summary>Shows what the last run did.</summary>
        void ShowResult(string text);
    }
}
