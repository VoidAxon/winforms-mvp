using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.UserModule
{
    public static class UserListActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("UserList");

        public static readonly ViewAction Add     = Factory.Create("Add");
        public static readonly ViewAction Edit    = Factory.Create("Edit");
        public static readonly ViewAction Refresh = Factory.Create("Refresh");
    }
}
