using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    public static class AnchoredMessageDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("AnchoredMessageDemo");

        public static readonly ViewAction Save = Factory.Create("Save");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction GridTouch = Factory.Create("GridTouch");
    }

    /// <summary>
    /// View for the anchored-message demo. Note there is no feedback method here — anchored
    /// toast/message-box come from the framework's IViewBase extensions (View.ShowToast, ...).
    /// </summary>
    public interface IAnchoredMessageDemoView : IWindowView
    {
        void ShowHint(string message);
    }
}
