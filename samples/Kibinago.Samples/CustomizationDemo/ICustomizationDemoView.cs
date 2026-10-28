using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.CustomizationDemo
{
    public static class CustomizationDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("CustomizationDemo");

        public static readonly ViewAction Info = Factory.Create("Info");
        public static readonly ViewAction Confirm = Factory.Create("Confirm");
        public static readonly ViewAction Toast = Factory.Create("Toast");
        public static readonly ViewAction CheckChanges = Factory.Create("CheckChanges");
        public static readonly ViewAction Busy = Factory.Create("Busy");
    }

    /// <summary>
    /// The demo view. Nothing here knows about the customization: the presenter calls the usual
    /// <c>Messages</c> / <c>Toasts</c> / <c>TrackChanges</c>, and what the user sees is decided
    /// once at startup in <see cref="CustomizationDemoSetup"/>.
    /// </summary>
    public interface ICustomizationDemoView : IWindowView
    {
        string CustomerName { get; set; }
        void ShowStatus(string text);
    }
}
