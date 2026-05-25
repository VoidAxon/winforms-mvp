using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.OrderModule
{
    public class OrderListForm : Form, IOrderListView
    {
        private ListView _listView;
        private Button _refreshButton;
        private ViewActionBinder _binder;

        public OrderListForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Text = "Orders";
            Size = new Size(540, 360);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            _listView = new ListView
            {
                Location = new Point(12, 12),
                Size = new Size(500, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            _listView.Columns.Add("#",           40, HorizontalAlignment.Right);
            _listView.Columns.Add("Customer Id", 80, HorizontalAlignment.Right);
            _listView.Columns.Add("Amount",      90, HorizontalAlignment.Right);
            _listView.Columns.Add("Description",  280, HorizontalAlignment.Left);

            _refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(422, 275),
                Size = new Size(90, 30),
            };

            Controls.AddRange(new Control[] { _listView, _refreshButton });
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(OrderListActions.Refresh, _refreshButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        public void SetOrders(IReadOnlyList<Order> orders)
        {
            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (var o in orders)
            {
                var item = new ListViewItem(o.Id.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(o.CustomerUserId.ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(o.Amount.ToString("0.00", CultureInfo.InvariantCulture));
                item.SubItems.Add(o.Description ?? string.Empty);
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();
        }

        #region IWindowView plumbing

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => Activate();

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);

        #endregion
    }
}
