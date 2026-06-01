using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.WindowClosingDemo
{
    /// <summary>
    /// Form implementation of <see cref="IWindowClosingDemoView"/>.
    /// </summary>
    /// <remarks>
    /// The interesting bits live in the <c>IWindowView Closing</c> region at the bottom of
    /// the class — that is the minimum boilerplate every Form needs in order to participate
    /// in the framework's close pipeline.
    /// </remarks>
    public class WindowClosingDemoForm : Form, IWindowClosingDemoView
    {
        private TextBox _textBox;
        private Button _saveButton;
        private Button _cancelButton;
        private Label _statusLabel;
        private ViewActionBinder _binder;

        public WindowClosingDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Window Closing Demo";
            this.Size = new Size(500, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(400, 220);

            var headerLabel = new Label
            {
                Text = "Edit the text, then Save / Cancel / close the window.\n" +
                       "Closing with unsaved changes triggers a confirm prompt.",
                Location = new Point(12, 12),
                Size = new Size(460, 40),
            };

            _textBox = new TextBox
            {
                Location = new Point(12, 60),
                Size = new Size(460, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _textBox.TextChanged += (s, e) => EditChanged?.Invoke(this, EventArgs.Empty);

            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(316, 100),
                Size = new Size(75, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(397, 100),
                Size = new Size(75, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };

            _statusLabel = new Label
            {
                Location = new Point(12, 150),
                Size = new Size(460, 60),
                ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            };

            this.Controls.AddRange(new Control[]
            {
                headerLabel, _textBox, _saveButton, _cancelButton, _statusLabel
            });
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

        // 'new' is intentional: this Text property is the View-contract text (textbox
        // content), distinct from Form.Text (window title).
        public new string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? string.Empty;
        }

        public string StatusMessage
        {
            set => _statusLabel.Text = value ?? string.Empty;
        }

        public event EventHandler EditChanged;

        // ─── IWindowView Closing ─────────────────────────────────────────────────────
        // Boilerplate that EVERY Form implementing IWindowView needs:
        //   1. Override IWindowView.IsDisposed and IWindowView.Activate (here Form's own
        //      members satisfy them implicitly, so no shims required).
        //   2. Use *explicit* interface implementation for Closing and OnClosing to avoid
        //      name collisions with Form's deprecated 1.x Closing event / protected OnClosing.
        //   3. Forward OnClosing(args) to the explicit-interface Closing event.

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => this.Activate();

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
    }
}
