using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.BusyDemo
{
    public partial class BusyDemoControl : UserControl, IBusyDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public BusyDemoControl()
        {
            InitializeComponent();

            _binder.Add(BusyDemoActions.RunSynchronously, _syncButton);
            _binder.Add(BusyDemoActions.RunAsync, _asyncButton);
            _binder.Add(BusyDemoActions.RunOnWorker, _workerButton);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public void ShowResult(string text) => _resultLabel.Text = text;
    }
}
