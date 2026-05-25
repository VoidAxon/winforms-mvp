using System.Collections.Generic;
using WinformsMVP.Core.Views;

namespace MultiProjectDemo.OrderModule
{
    public interface IOrderListView : IWindowView
    {
        void SetOrders(IReadOnlyList<Order> orders);
    }
}
