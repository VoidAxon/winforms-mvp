using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class SimpleDialogForm : Form, ISimpleDialogView
    {
        private ViewActionBinder _binder;

        public SimpleDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (SimpleDialogActions.Ok, _okButton),
                (SimpleDialogActions.Cancel, _cancelButton)
            );
        }

    }
}
