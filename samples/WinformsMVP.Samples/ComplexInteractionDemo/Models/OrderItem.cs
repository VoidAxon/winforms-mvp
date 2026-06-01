using System;

namespace WinformsMVP.Samples.ComplexInteractionDemo.Models
{
    /// <summary>
    /// Represents an item in an order
    /// </summary>
    public class OrderItem : ICloneable
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }

        public decimal Subtotal => Product != null ? Product.Price * Quantity : 0;

        public object Clone()
        {
            return new OrderItem
            {
                Product = this.Product?.Clone() as Product,
                Quantity = this.Quantity
            };
        }

        public override string ToString()
        {
            return $"{Product?.Name} x {Quantity} = ${Subtotal:F2}";
        }
    }
}
