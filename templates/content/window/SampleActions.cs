using WinformsMVP.MVP.ViewActions;

namespace MvpNamespace
{
    public static class SampleActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Sample");

        public static readonly ViewAction Ok = Factory.Create(nameof(Ok));
        public static readonly ViewAction Cancel = Factory.Create(nameof(Cancel));
    }
}
