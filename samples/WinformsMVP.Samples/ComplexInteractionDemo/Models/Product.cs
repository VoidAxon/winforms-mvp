using System;

namespace WinformsMVP.Samples.ComplexInteractionDemo.Models
{
    /// <summary>
    /// Represents a product available for ordering
    /// </summary>
    public class Product : ICloneable
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }

        public object Clone()
        {
            return new Product
            {
                Id = this.Id,
                Name = this.Name,
                Category = this.Category,
                Price = this.Price,
                StockQuantity = this.StockQuantity
            };
        }

        public override string ToString()
        {
            return $"{Name} - ${Price:F2} ({StockQuantity} in stock)";
        }
    }
}
