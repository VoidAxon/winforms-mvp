using System.Collections.Generic;
using System.Linq;

namespace MultiProjectDemo.OrderModule
{
    public class InMemoryOrderRepository : IOrderRepository
    {
        private readonly Dictionary<int, Order> _orders = new Dictionary<int, Order>();
        private int _nextId = 1;

        public InMemoryOrderRepository()
        {
            // Seed orders referencing user ids (the demo UserRepository starts at 1).
            Save(new Order { CustomerUserId = 1, Amount = 120.50m, Description = "Subscription"      });
            Save(new Order { CustomerUserId = 1, Amount =  45.00m, Description = "Add-on pack"        });
            Save(new Order { CustomerUserId = 2, Amount = 999.00m, Description = "Annual licence"     });
            Save(new Order { CustomerUserId = 3, Amount =  15.95m, Description = "Spare part: cable"  });
        }

        public IReadOnlyList<Order> GetAll()
            => _orders.Values.OrderBy(o => o.Id).Select(Clone).ToList();

        public void Save(Order order)
        {
            if (order.Id == 0) order.Id = _nextId++;
            _orders[order.Id] = Clone(order);
        }

        private static Order Clone(Order o) => new Order
        {
            Id = o.Id,
            CustomerUserId = o.CustomerUserId,
            Amount = o.Amount,
            Description = o.Description,
        };
    }
}
