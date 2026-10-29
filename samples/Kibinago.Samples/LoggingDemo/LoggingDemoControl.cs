using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.LoggingDemo
{
    public partial class LoggingDemoControl : UserControl, ILoggingDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public LoggingDemoControl()
        {
            InitializeComponent();
            _binder.Add(LoggingActions.LogDebug, _btnDebug);
            _binder.Add(LoggingActions.LogInfo, _btnInfo);
            _binder.Add(LoggingActions.LogWarning, _btnWarning);
            _binder.Add(LoggingActions.LogError, _btnError);
            _binder.Add(LoggingActions.LogStructured, _btnStructured);
            _binder.Add(LoggingActions.LogException, _btnException);
            _binder.Add(LoggingActions.ClearLog, _btnClear);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LogOutput
        {
            get => _txtLogOutput.Text;
            set => _txtLogOutput.Text = value?.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string UserName
        {
            get => _txtUserName.Text;
            set => _txtUserName.Text = value;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ProcessedCount
        {
            get => int.TryParse(_lblProcessedCount.Text, out var count) ? count : 0;
            set => _lblProcessedCount.Text = value.ToString();
        }
    }
}
