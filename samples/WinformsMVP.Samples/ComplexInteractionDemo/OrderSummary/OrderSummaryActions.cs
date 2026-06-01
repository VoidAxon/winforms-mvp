using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary
{
    /// <summary>
    /// Static action keys for OrderSummary
    /// </summary>
    public static class OrderSummaryActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("OrderSummary");

        public static readonly ViewAction RemoveItem = Factory.Create("RemoveItem");
        public static readonly ViewAction ClearAll = Factory.Create("ClearAll");
    }
}
