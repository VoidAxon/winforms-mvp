using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.CascadeDemo
{
    public partial class CascadeDemoControl : UserControl, ICascadeDemoView
    {
        public CascadeDemoControl()
        {
            InitializeComponent();
        }

        // No actions: the view raises events instead (IControlView has an ActionBinder; null = none to bind).
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => null;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<Category> Categories => _categoryControl;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<SubCategory> SubCategories => _subCategoryControl;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISelectListView<Product> Products => _productControl;
    }
}
