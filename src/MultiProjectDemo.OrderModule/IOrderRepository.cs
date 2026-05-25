using System.Collections.Generic;

namespace MultiProjectDemo.OrderModule
{
    public interface IOrderRepository
    {
        IReadOnlyList<Order> GetAll();
        void Save(Order order);
    }
}
