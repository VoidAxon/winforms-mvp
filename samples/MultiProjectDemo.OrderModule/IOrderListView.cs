using System.Collections.Generic;
using Kibinago.Views;

namespace MultiProjectDemo.OrderModule
{
    public interface IOrderListView : IWindowView
    {
        void SetOrders(IReadOnlyList<Order> orders);
    }
}
