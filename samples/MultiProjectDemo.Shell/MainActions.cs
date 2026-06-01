using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.Shell
{
    public static class MainActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Main");

        public static readonly ViewAction OpenUsers  = Factory.Create("OpenUsers");
        public static readonly ViewAction OpenOrders = Factory.Create("OpenOrders");
        public static readonly ViewAction Exit       = Factory.Create("Exit");
    }
}
