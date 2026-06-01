using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector
{
    /// <summary>
    /// Static action keys for ProductSelector
    /// </summary>
    public static class ProductSelectorActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("ProductSelector");

        public static readonly ViewAction AddToOrder = Factory.Create("AddToOrder");
    }
}
