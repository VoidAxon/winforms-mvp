using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.MdiDemo
{
    public partial class MdiHostForm : Form, IMdiHostView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public MdiHostForm()
        {
            InitializeComponent();
            _binder.Add(MdiHostActions.OpenDocument, _openItem);
            _binder.Add(MdiHostActions.OpenFirst, _openFirstItem);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        // Placing the window is the container's job (a docking host would Show(dockPanel, ...) here).
        public void ShowDocument(IWindowView document)
        {
            var form = (Form)document;
            if (form.MdiParent == null)
            {
                form.MdiParent = this;
                form.Show();
            }
            else
            {
                form.Activate();   // the same key: PrepareWindow returned the open one without activating it
            }
        }

        public void ShowStatus(string text) => _status.Text = text;
    }
}
