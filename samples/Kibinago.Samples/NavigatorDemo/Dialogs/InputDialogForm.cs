using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class InputDialogForm : Form, IInputDialogView
    {
        private ViewActionBinder _binder;

        public InputDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetPrompt(string prompt)
        {
            _promptLabel.Text = prompt;
        }

        public string GetInput()
        {
            return _inputTextBox.Text;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (InputDialogActions.Ok, _okButton),
                (InputDialogActions.Cancel, _cancelButton)
            );
        }

    }
}
