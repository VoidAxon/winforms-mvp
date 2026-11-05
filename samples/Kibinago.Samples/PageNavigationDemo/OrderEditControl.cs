using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.PageNavigationDemo
{
    public partial class OrderEditControl : UserControl, IOrderEditView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public OrderEditControl()
        {
            InitializeComponent();
            _binder.Add(PageNavigationDemoActions.SaveOrder, _saveButton);
            _product.TextChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
            _note.TextChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
            _quantity.ValueChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler Edited;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Product
        {
            get => _product.Text;
            set { if (_product.Text != value) _product.Text = value; }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Quantity
        {
            get => (int)_quantity.Value;
            set { if ((int)_quantity.Value != value) _quantity.Value = value; }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Note
        {
            get => _note.Text;
            set { if (_note.Text != value) _note.Text = value; }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsDirty
        {
            set => _dirtyLabel.Visible = value;
        }
    }
}
