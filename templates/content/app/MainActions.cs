using WinformsMVP.MVP.ViewActions;

namespace MvpApp
{
    public static class MainActions
    {
        public static readonly ViewAction SayHello = ViewAction.Create("Main.SayHello");
        public static readonly ViewAction Exit = ViewAction.Create("Main.Exit");
    }
}
