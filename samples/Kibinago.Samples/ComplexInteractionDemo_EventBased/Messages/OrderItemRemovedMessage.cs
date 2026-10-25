using Kibinago.Samples.ComplexInteractionDemo.Models;

namespace Kibinago.Samples.ComplexInteractionDemo_EventBased.Messages
{
    /// <summary>
    /// Message published when an order item is removed.
    /// </summary>
    public class OrderItemRemovedMessage
    {
        public OrderItem Item { get; set; }

        public OrderItemRemovedMessage(OrderItem item)
        {
            Item = item;
        }
    }
}
