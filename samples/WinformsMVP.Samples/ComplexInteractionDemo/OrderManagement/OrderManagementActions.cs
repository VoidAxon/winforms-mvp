using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement
{
    /// <summary>
    /// Static action keys for OrderManagement
    /// </summary>
    public static class OrderManagementActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("OrderManagement");

        public static readonly ViewAction SaveOrder = Factory.Create("SaveOrder");
        public static readonly ViewAction ClearOrder = Factory.Create("ClearOrder");
    }
}
