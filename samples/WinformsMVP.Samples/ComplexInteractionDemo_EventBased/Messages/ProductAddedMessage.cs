using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages
{
    /// <summary>
    /// Message published when a product is added to the order.
    /// </summary>
    public class ProductAddedMessage
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }

        public ProductAddedMessage(Product product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }
    }
}
