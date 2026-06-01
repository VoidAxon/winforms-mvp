using System.Collections.Generic;
using WinformsMVP.MVP.Views;

namespace MultiProjectDemo.OrderModule
{
    public interface IOrderListView : IWindowView
    {
        void SetOrders(IReadOnlyList<Order> orders);
    }
}
