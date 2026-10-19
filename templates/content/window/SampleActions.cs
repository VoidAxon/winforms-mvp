using WinformsMVP.MVP.ViewActions;

namespace MvpNamespace
{
    public static class SampleActions
    {
        public static readonly ViewAction Ok = ViewAction.Create("Sample.Ok");
        public static readonly ViewAction Cancel = ViewAction.Create("Sample.Cancel");
    }
}
