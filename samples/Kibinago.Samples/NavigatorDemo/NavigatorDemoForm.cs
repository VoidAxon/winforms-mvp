using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    /// <summary>
    /// Main demo form for WindowNavigator features.
    /// </summary>
    public partial class NavigatorDemoForm : Form, INavigatorDemoView
    {
        private ViewActionBinder _viewActionBinder;

        public NavigatorDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        #region INavigatorDemoView Implementation

        public void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logTextBox.AppendText($"[{timestamp}] {message}\r\n");
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();

            _viewActionBinder.AddRange(
                (NavigatorDemoActions.ShowModalSimple, _modalSimpleButton),
                (NavigatorDemoActions.ShowModalWithResult, _modalWithResultButton),
                (NavigatorDemoActions.ShowModalWithParams, _modalWithParamsButton),
                (NavigatorDemoActions.ShowNonModalSimple, _nonModalSimpleButton),
                (NavigatorDemoActions.ShowNonModalWithCallback, _nonModalWithCallbackButton),
                (NavigatorDemoActions.ShowSingletonWindow, _singletonWindowButton),
                (NavigatorDemoActions.ClearLog, _clearLogButton)
            );

        }

        #endregion

    }
}
