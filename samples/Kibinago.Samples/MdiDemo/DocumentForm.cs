using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MdiDemo
{
    public partial class DocumentForm : Form, IDocumentView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public DocumentForm()
        {
            InitializeComponent();
            _binder.Add(DocumentActions.Process, _processButton);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { set => Text = value; }

        public void ShowStatus(string text) => _status.Text = text;
    }
}
