using System.ComponentModel;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;

namespace MvpNamespace
{
    /// <summary>Binds controls to actions and shows what the presenter sets. No business logic.</summary>
    public partial class SampleControl : UserControl, ISampleView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public SampleControl()
        {
            InitializeComponent();
            _binder.Add(SampleActions.Refresh, btnRefresh);
        }

        public IViewActionBinder ActionBinder => _binder;

        // View data, not a designer property: keep it out of the .Designer.cs file.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Summary
        {
            set => lblSummary.Text = value;
        }
    }
}
