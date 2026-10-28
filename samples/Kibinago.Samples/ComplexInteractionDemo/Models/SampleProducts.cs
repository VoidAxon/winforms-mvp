using System.Collections.Generic;

namespace Kibinago.Samples.ComplexInteractionDemo.Models
{
    /// <summary>The product catalog of the order demos (both variants).</summary>
    public static class SampleProducts
    {
        public static List<Product> All() => new List<Product>
        {
            new Product { Id = 1, Name = "ノート PC", Price = 999.99m },
            new Product { Id = 2, Name = "マウス", Price = 29.99m },
            new Product { Id = 3, Name = "キーボード", Price = 79.99m },
            new Product { Id = 4, Name = "モニター", Price = 299.99m },
            new Product { Id = 5, Name = "USB ケーブル", Price = 9.99m },
            new Product { Id = 6, Name = "ヘッドホン", Price = 149.99m },
        };
    }
}
