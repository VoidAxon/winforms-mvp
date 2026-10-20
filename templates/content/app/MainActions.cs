using WinformsMVP.MVP.ViewActions;

namespace MyMvpApp
{
    public static class MainActions
    {
        public static readonly ViewAction SayHello = ViewAction.Create("Main.SayHello");
        public static readonly ViewAction Exit = ViewAction.Create("Main.Exit");
    }
}
