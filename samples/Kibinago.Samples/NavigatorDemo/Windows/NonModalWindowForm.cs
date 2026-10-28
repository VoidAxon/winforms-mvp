using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class NonModalWindowForm : Form, INonModalWindowView
    {
        private ViewActionBinder _binder;

        public NonModalWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetCounter(int value)
        {
            _counterLabel.Text = $"Counter: {value}";
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (NonModalWindowActions.Increment, _incrementButton),
                (NonModalWindowActions.Close, _closeButton)
            );
        }

    }
}
