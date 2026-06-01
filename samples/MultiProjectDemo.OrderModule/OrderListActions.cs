using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.OrderModule
{
    public static class OrderListActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("OrderList");

        public static readonly ViewAction Refresh = Factory.Create("Refresh");
    }
}
