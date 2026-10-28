using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.CustomizationDemo
{
    public partial class CustomizationDemoForm : Form, ICustomizationDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public CustomizationDemoForm()
        {
            InitializeComponent();

            _binder.Add(CustomizationDemoActions.Info, _infoButton);
            _binder.Add(CustomizationDemoActions.Confirm, _confirmButton);
            _binder.Add(CustomizationDemoActions.Toast, _toastButton);
            _binder.Add(CustomizationDemoActions.CheckChanges, _checkButton);
            _binder.Add(CustomizationDemoActions.Busy, _busyButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CustomerName
        {
            get => _name.Text;
            set => _name.Text = value;
        }

        public void ShowStatus(string text) => _status.Text = text;
    }
}
