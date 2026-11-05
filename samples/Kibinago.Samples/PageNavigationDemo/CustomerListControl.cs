using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.PageNavigationDemo
{
    public partial class CustomerListControl : UserControl, ICustomerListView
    {
        public CustomerListControl()
        {
            InitializeComponent();
            _list.DoubleClick += (s, e) => OpenSelected();
            _openButton.Click += (s, e) => OpenSelected();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => null;

        public event EventHandler<int> CustomerOpened;

        public void ShowCustomers(IReadOnlyList<CustomerRow> rows)
        {
            var selectedId = _list.SelectedItems.Count > 0 ? (int?)((CustomerRow)_list.SelectedItems[0].Tag).Id : null;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(new[] { row.Name, row.Phone, row.TotalQuantity.ToString() }) { Tag = row };
                _list.Items.Add(item);
                if (row.Id == selectedId) item.Selected = true;   // the selection survives a refresh
            }
            _list.EndUpdate();
        }

        private void OpenSelected()
        {
            var row = _list.SelectedItems.Cast<ListViewItem>().Select(i => (CustomerRow)i.Tag).FirstOrDefault();
            if (row != null) CustomerOpened?.Invoke(this, row.Id);
        }
    }
}
