using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Main demo view for WindowNavigator features.
    /// </summary>
    public interface INavigatorDemoView : IWindowView
    {
        /// <summary>
        /// Update the log/output area
        /// </summary>
        void AppendLog(string message);
    }
}
