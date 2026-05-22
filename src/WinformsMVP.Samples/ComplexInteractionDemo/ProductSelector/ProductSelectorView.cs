using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector
{
    /// <summary>
    /// UserControl for selecting products and adding them to an order
    /// </summary>
    public partial class ProductSelectorView : UserControl, IProductSelectorView
    {
        private ViewActionBinder _binder;
        private List<Product> _products = new List<Product>();

        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler<ProductAddedEventArgs> ProductAdded;
        public event EventHandler SelectionChanged;

        public ProductSelectorView()
        {
            InitializeComponent();
            InitializeActionBindings();

            // Wire up events
            listProducts.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(ProductSelectorActions.AddToOrder, btnAddToOrder);
            // Framework will automatically bind
        }

        public IList<Product> Products
        {
            get => _products;
            set
            {
                _products = value?.ToList() ?? new List<Product>();
                RefreshProductList();
            }
        }

        public Product SelectedProduct
        {
            get => listProducts.SelectedItem as Product;
        }

        public int Quantity
        {
            get => (int)numQuantity.Value;
            set => numQuantity.Value = Math.Max(1, Math.Min(value, numQuantity.Maximum));
        }

        public bool HasSelection => listProducts.SelectedItem != null;

        public bool HasValidQuantity => numQuantity.Value > 0;

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void RaiseProductAdded(Product product, int quantity)
        {
            ProductAdded?.Invoke(this, new ProductAddedEventArgs(product, quantity));
        }

        private void RefreshProductList()
        {
            listProducts.Items.Clear();
            if (_products != null)
            {
                foreach (var product in _products)
                {
                    listProducts.Items.Add(product);
                }
            }
        }
    }
}
