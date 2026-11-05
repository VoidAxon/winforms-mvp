using System;
using System.Collections.Generic;
using System.Linq;

namespace Kibinago.Samples.PageNavigationDemo
{
    public sealed class DemoCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
    }

    public sealed class DemoOrder
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public string Note { get; set; }

        public DemoOrder Copy() => (DemoOrder)MemberwiseClone();
    }

    /// <summary>In-memory data for the demo (a real application injects a repository).</summary>
    public sealed class DemoCustomerStore
    {
        private readonly List<DemoCustomer> _customers = new List<DemoCustomer>
        {
            new DemoCustomer { Id = 1, Name = "山田 太郎", Phone = "03-1111-2222" },
            new DemoCustomer { Id = 2, Name = "佐藤 花子", Phone = "06-3333-4444" },
            new DemoCustomer { Id = 3, Name = "鈴木 一郎", Phone = "052-555-6666" },
        };

        private readonly List<DemoOrder> _orders = new List<DemoOrder>
        {
            new DemoOrder { Id = 101, CustomerId = 1, Product = "コピー用紙 A4", Quantity = 10, Note = "" },
            new DemoOrder { Id = 102, CustomerId = 1, Product = "ボールペン 黒", Quantity = 50, Note = "至急" },
            new DemoOrder { Id = 201, CustomerId = 2, Product = "ファイル", Quantity = 20, Note = "" },
            new DemoOrder { Id = 301, CustomerId = 3, Product = "付箋", Quantity = 30, Note = "" },
        };

        /// <summary>Raised after an order was saved (the pages that show it refresh).</summary>
        public event EventHandler<DemoOrder> OrderSaved;

        public IReadOnlyList<DemoCustomer> Customers => _customers;

        public DemoCustomer Customer(int id) => _customers.First(c => c.Id == id);

        /// <summary>Copies, so a page edits its own object.</summary>
        public IReadOnlyList<DemoOrder> OrdersOf(int customerId)
            => _orders.Where(o => o.CustomerId == customerId).Select(o => o.Copy()).ToList();

        public DemoOrder Order(int id) => _orders.First(o => o.Id == id).Copy();

        public int TotalQuantity(int customerId) => _orders.Where(o => o.CustomerId == customerId).Sum(o => o.Quantity);

        public void Save(DemoOrder order)
        {
            var index = _orders.FindIndex(o => o.Id == order.Id);
            _orders[index] = order.Copy();
            OrderSaved?.Invoke(this, order.Copy());
        }
    }
}
