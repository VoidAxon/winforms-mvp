using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class ConfirmDialogForm : Form, IConfirmDialogView
    {
        private ViewActionBinder _binder;

        public ConfirmDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetTitle(string title)
        {
            this.Text = title;
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public void SetDefaultChoice(bool yesIsDefault)
        {
            this.AcceptButton = yesIsDefault ? _yesButton : _noButton;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (ConfirmDialogActions.Yes, _yesButton),
                (ConfirmDialogActions.No, _noButton),
                (ConfirmDialogActions.Cancel, _cancelButton)
            );
        }

    }
}
