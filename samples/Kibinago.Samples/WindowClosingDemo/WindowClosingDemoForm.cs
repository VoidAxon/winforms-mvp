using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.WindowClosingDemo
{
    /// <summary>
    /// Form implementation of <see cref="IWindowClosingDemoView"/>.
    /// </summary>
    /// <remarks>
    /// The Form writes no closing code at all — the close pipeline is driven by the Presenter
    /// overriding <c>CanClose(CloseReason)</c> (the gate every close goes through) and calling
    /// <c>RequestClose(...)</c> (Push). Buttons are bound to actions; none calls <c>Close()</c>.
    /// </remarks>
    public partial class WindowClosingDemoForm : Form, IWindowClosingDemoView
    {
        private ViewActionBinder _binder;

        public WindowClosingDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (WindowClosingDemoActions.Save, _saveButton),
                (WindowClosingDemoActions.Cancel, _cancelButton)
            );
            // No Bind() here — the framework calls Bind(_dispatcher) automatically
            // after RegisterViewActions() completes (Implicit ViewAction pattern).
        }

        public IViewActionBinder ActionBinder => _binder;

        // ─── IWindowClosingDemoView ──────────────────────────────────────────────────

        // The edited text (not Form.Text, the window title).
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Content
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? string.Empty;
        }

        public string StatusMessage
        {
            set => _statusLabel.Text = value ?? string.Empty;
        }

        public event EventHandler EditChanged;

    }
}
