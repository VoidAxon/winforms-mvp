using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Per-window controller that owns the entire WinForms close bridge for one Form: it is the
    /// Push sink, the Pull gate's bridge to <c>FormClosing</c>, and the result-converging
    /// <c>FormClosed</c> handler. The only place in the framework that knows <see cref="Form"/>.
    /// One instance per window, so all suppress/defer state is plain fields, no shared tables.
    /// </summary>
    internal sealed class WindowLifecycleController : ICloseSink
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
        private bool _gatePending;             // async CanClose has not answered yet
        private bool _gateRunning;             // CanClose is running synchronously right now

        // A push made while the gate is deciding (X -> "Save changes?" -> Yes -> saved) is the
        // result of that close: held here and applied only if the gate allows.
        private bool _hasGateResult;
        private object _gateResult;
        private InteractionStatus _gateStatus;

        /// <param name="disposeForm">True when this controller owns the Form lifetime (Managed
        /// modal, Adopted). False for non-modal Managed, where WinForms disposes on close.</param>
        internal WindowLifecycleController(IWindowView view, ICloseParticipant presenter,
            Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            if (!(view is Form form))
                throw new ArgumentException(
                    "Window closing requires a Form-backed view.", nameof(view));
            _form = form;
            _presenter = presenter;
            _onClosed = onClosed;
            _disposeForm = disposeForm;
            _logger = WinformsMVP.Services.ServiceLocator.Current
                .ResolveRequired<WinformsMVP.Logging.ILoggerFactory>()
                .CreateLogger(typeof(WindowLifecycleController));
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
            if (_gateRunning || _gatePending)
            {
                // Not a second close: the gate's answer decides whether this close happens.
                _hasGateResult = true;
                _gateResult = result;
                _gateStatus = status;
                return;
            }

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

            var winFormsReason = e.CloseReason;
            var reason = CloseReasonMap.From(winFormsReason);

            if (_gatePending)
            {
                // An async CanClose is still deciding. A system-level close is never blocked (the
                // late answer then finds the form disposed and is ignored); anything else keeps the
                // window open without asking again (a second X click must not raise a second
                // prompt / save).
                e.Cancel = !(reason == WinformsMVP.Common.CloseReason.SystemShutdown
                          || reason == WinformsMVP.Common.CloseReason.TaskManager);
                return;
            }
            // A fresh close attempt starts from the default (user X => Cancel): a result an earlier
            // gate allowed must not leak in when that close was cancelled elsewhere (another
            // FormClosing handler, another window during Application.Exit).
            _pendingResult = null;
            _pendingStatus = InteractionStatus.Cancel;

            var gate = new object();
            bool sync = true;
            bool? decision = null;
            _gateRunning = true;
            try
            {
                _presenter.CanCloseGate(reason, ok =>
                {
                    lock (gate)
                    {
                        if (sync) { decision = ok; return; }                // synchronous answer
                    }
                    OnAsyncDecision(ok, winFormsReason);                    // answered later
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CanClose threw; blocking the close as a safe default.");
                // Never block system-level shutdowns even when CanClose throws.
                lock (gate)
                {
                    decision = reason == WinformsMVP.Common.CloseReason.SystemShutdown
                            || reason == WinformsMVP.Common.CloseReason.TaskManager;
                }
            }
            finally { _gateRunning = false; }

            bool? answered;
            lock (gate)
            {
                sync = false;
                answered = decision;
            }

            if (answered.HasValue)
            {
                e.Cancel = !answered.Value;
                TakeGateResult(answered.Value);
            }
            else { e.Cancel = true; _gatePending = true; }
        }

        /// <summary>
        /// A deferred CanClose answer. May arrive on any thread; the form is only touched on its
        /// UI thread. An "allow" re-issues the original close intent: the gate cancelled the whole
        /// operation (an owner / MDI parent close or <c>Application.Exit</c>), not just this form.
        /// </summary>
        private void OnAsyncDecision(bool allow, System.Windows.Forms.CloseReason originalReason)
        {
            if (_form.IsDisposed) return;
            if (_form.InvokeRequired)
            {
                try { _form.BeginInvoke(new Action(() => OnAsyncDecision(allow, originalReason))); }
                catch (InvalidOperationException) { /* handle gone: the window is closing anyway */ }
                return;
            }

            _gatePending = false;
            TakeGateResult(allow);
            if (!allow || _form.IsDisposed) return;   // blocked: leave the window open

            _suppressGate = true;
            switch (originalReason)
            {
                case System.Windows.Forms.CloseReason.FormOwnerClosing when _form.Owner != null:
                    var owner = _form.Owner;
                    _form.Close();
                    owner.Close();
                    break;
                case System.Windows.Forms.CloseReason.MdiFormClosing when _form.MdiParent != null:
                    var parent = _form.MdiParent;
                    _form.Close();
                    parent.Close();
                    break;
                case System.Windows.Forms.CloseReason.ApplicationExitCall:
                    _form.Close();
                    Application.Exit();
                    break;
                default:
                    _form.Close();
                    break;
            }
        }

        /// <summary>An allowed close carries the result pushed during the gate; a blocked close
        /// drops it (the next close starts clean).</summary>
        private void TakeGateResult(bool allowed)
        {
            if (allowed && _hasGateResult)
            {
                _pendingResult = _gateResult;
                _pendingStatus = _gateStatus;
            }
            _hasGateResult = false;
            _gateResult = null;
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
                try { (_presenter as IDisposable)?.Dispose(); }
                finally { if (_disposeForm) _form.Dispose(); }
            }
        }
    }
}
