using Kibinago.ViewActions;

namespace MyMvpApp
{
    public static class MainActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Main");

        public static readonly ViewAction SayHello = Factory.Create(nameof(SayHello));
        public static readonly ViewAction Exit = Factory.Create(nameof(Exit));
    }
}
