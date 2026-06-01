using System.Collections.Generic;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages
{
    /// <summary>
    /// Request message to get a snapshot of the current order.
    /// Used for save operations.
    /// </summary>
    public class GetOrderSnapshotRequest
    {
        /// <summary>
        /// The response will be set by the subscriber (OrderSummaryPresenter).
        /// </summary>
        public List<OrderItem> OrderItems { get; set; }

        public decimal Total { get; set; }
    }
}
