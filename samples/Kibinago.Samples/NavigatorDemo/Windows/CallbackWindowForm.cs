using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class CallbackWindowForm : Form, ICallbackWindowView
    {
        private ViewActionBinder _binder;

        public CallbackWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public string GetText()
        {
            return _textBox.Text;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (CallbackWindowActions.SaveAndClose, _saveButton),
                (CallbackWindowActions.Cancel, _cancelButton)
            );
        }

    }
}
