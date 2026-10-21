using WinformsMVP.MVP.ViewActions;

namespace MvpNamespace
{
    public static class SampleActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Sample");

        public static readonly ViewAction Refresh = Factory.Create(nameof(Refresh));
    }
}
