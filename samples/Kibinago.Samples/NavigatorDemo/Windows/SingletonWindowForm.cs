using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public partial class SingletonWindowForm : Form, ISingletonWindowView
    {
        private ViewActionBinder _binder;

        public SingletonWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        public void SetInstanceId(int id)
        {
            _titleLabel.Text = $"Instance #{id}";
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
                (SingletonWindowActions.ShowInfo, _infoButton),
                (SingletonWindowActions.Close, _closeButton)
            );
        }

    }
}
