using System.ComponentModel;
using System.Windows.Forms;

namespace Kibinago.Samples.CascadeDemo
{
    public partial class CascadeDemoControl : UserControl, ICascadeDemoView
    {
        public CascadeDemoControl()
        {
            InitializeComponent();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<Category> Categories => _categoryControl;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<SubCategory> SubCategories => _subCategoryControl;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<Product> Products => _productControl;
    }
}
