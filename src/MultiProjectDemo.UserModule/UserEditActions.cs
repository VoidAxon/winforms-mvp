using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.UserModule
{
    public static class UserEditActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("UserEdit");

        public static readonly ViewAction Save   = Factory.Create("Save");
        public static readonly ViewAction Cancel = Factory.Create("Cancel");
    }
}
