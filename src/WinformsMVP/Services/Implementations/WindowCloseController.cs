using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Per-window controller that owns the entire WinForms close bridge for one Form: it is the
    /// Push sink, the Pull gate's bridge to <c>FormClosing</c>, and the result-converging
    /// <c>FormClosed</c> handler. The only place in the framework that knows <see cref="Form"/>.
    /// One instance per window, so all suppress/defer state is plain fields, no shared tables.
    /// </summary>
    internal sealed class WindowCloseController : ICloseSink
    {
        private readonly Form _form;
        private readonly ICloseParticipant _presenter;
        private readonly Action<object, InteractionStatus> _onClosed;
        private readonly bool _disposeForm;
        private readonly ILogger _logger;

        private object _pendingResult;
        private InteractionStatus _pendingStatus = InteractionStatus.Cancel; // user X => Cancel
        private bool _suppressGate;            // self-initiated / re-close: skip the gate once
        private bool _closeRequestedBeforeShow;

        /// <param name="disposeForm">True when this controller owns the Form lifetime (Managed
        /// modal, Adopted). False for non-modal Managed, where WinForms disposes on close.</param>
        internal WindowCloseController(IWindowView view, ICloseParticipant presenter,
            Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            if (!(view is Form form))
                throw new ArgumentException(
                    "Window closing requires a Form-backed view.", nameof(view));
            _form = form;
            _presenter = presenter;
            _onClosed = onClosed;
            _disposeForm = disposeForm;
            _logger = PlatformServices.Default.LoggerFactory.CreateLogger(typeof(WindowCloseController));
        }

        /// <summary>Injects the Push sink. Call BEFORE <c>Initialize</c> so a Presenter can
        /// <c>RequestClose</c> from <c>OnInitialize</c>.</summary>
        internal void BindSink() => _presenter.BindCloseSink(this);

        /// <summary>Wires the Pull bridge and result converger. Call AFTER <c>Initialize</c>.</summary>
        internal void WireFormEvents()
        {
            _form.FormClosing += OnFormClosing;
            _form.FormClosed += OnFormClosed;
            _form.Shown += OnFormShown;
        }

        /// <summary>True if a Push arrived before the form had a handle (e.g. RequestClose in
        /// OnInitialize). Managed callers check this and skip ShowDialog, calling
        /// <see cref="ConvergeWithoutShow"/> instead.</summary>
        internal bool CloseRequestedBeforeShow => _closeRequestedBeforeShow;

        /// <summary>Converge the pending result without ever showing the form. Managed-only path
        /// for close-before-show.</summary>
        internal void ConvergeWithoutShow()
        {
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.Shown -= OnFormShown;
            Converge();
        }

        // Push (ICloseSink)
        public void Close(object result, InteractionStatus status)
        {
            _pendingResult = result;
            _pendingStatus = status;
            _suppressGate = true;

            if (!_form.IsHandleCreated && !_form.Visible)
            {
                _closeRequestedBeforeShow = true;
                return;
            }
            _form.Close();
        }

        private void OnFormShown(object sender, EventArgs e)
        {
            if (_closeRequestedBeforeShow)
                _form.Close(); // _suppressGate already true => gate skipped, converges normally
        }

        // Pull (FormClosing)
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_suppressGate) { _suppressGate = false; return; }

            var reason = CloseReasonMap.From(e.CloseReason);
            bool sync = true;
            bool? decision = null;
            try
            {
                _presenter.CanCloseGate(reason, ok =>
                {
                    if (sync) decision = ok;                                  // synchronous answer
                    else if (ok) { _suppressGate = true; _form.Close(); }     // async allow => re-close
                    // async block: leave the window open, nothing to reset
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CanClose threw; blocking the close as a safe default.");
                decision = false;
            }
            sync = false;

            if (decision.HasValue) e.Cancel = !decision.Value;
            else e.Cancel = true;
        }

        // Converge (FormClosed)
        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.Shown -= OnFormShown;
            Converge();
        }

        private void Converge()
        {
            try { _onClosed?.Invoke(_pendingResult, _pendingStatus); }
            finally
            {
                (_presenter as IDisposable)?.Dispose();
                if (_disposeForm) _form.Dispose();
            }
        }
    }
}
