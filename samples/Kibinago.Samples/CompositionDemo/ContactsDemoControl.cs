using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.CompositionDemo
{
    public partial class ContactsDemoControl : UserControl, IContactsDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public ContactsDemoControl()
        {
            InitializeComponent();
            _binder.Add(ContactsDemoActions.AddRow, _addButton);
            _binder.Add(ContactsDemoActions.Save, _saveButton);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler<IContactRowView> RowRemoveRequested;

        public IContactRowView AddRow()
        {
            var row = new ContactRowControl();
            row.RemoveClicked += (s, e) => RowRemoveRequested?.Invoke(this, row);
            _rows.Controls.Add(row);
            return row;
        }

        public void RemoveRow(IContactRowView row)
        {
            var control = (Control)row;
            _rows.Controls.Remove(control);
            control.Dispose();
        }

        public void ShowStatus(string text) => _statusLabel.Text = text;
    }
}
