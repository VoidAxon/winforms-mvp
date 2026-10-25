using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace MyMvpApp
{
    /// <summary>
    /// The main window. It binds its buttons to actions and shows what the presenter sets;
    /// it contains no business logic and no closing logic.
    /// </summary>
    public partial class MainForm : Form, IMainView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public MainForm()
        {
            InitializeComponent();
            _binder.Add(MainActions.SayHello, btnSayHello);
            _binder.Add(MainActions.Exit, btnExit);
        }

        public IViewActionBinder ActionBinder => _binder;

        // View data, not a designer property: keep it out of the .Designer.cs file.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string StatusText
        {
            set => lblStatus.Text = value;
        }
    }
}
