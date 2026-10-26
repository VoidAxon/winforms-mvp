using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.Views;
using Kibinago.Services;

namespace Kibinago.Services
{
    /// <summary>
    /// Per-window controller that owns the entire WinForms close bridge for one Form: it is the
    /// Push sink, the gate's bridge to <c>FormClosing</c>, and the result-converging
    /// <c>FormClosed</c> handler. The only place in the framework that knows <see cref="Form"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>One gate.</b> Every close of an open window — ✕ / Alt+F4 / owner closing, and the presenter's
    /// <c>RequestClose</c> — goes through the presenter's <c>CanClose</c> / <c>CanCloseAsync</c>
    /// (system shutdown and Task Manager excepted). <c>RequestClose</c> supplies the candidate result of
    /// that close; a <c>RequestClose</c> made while the gate decides replaces it.</para>
    /// <para><b>Opening.</b> Until the window starts showing, a <c>RequestClose</c> cancels the opening
    /// instead: no gate (the user cannot have edited anything) and the window is never seen. Managed:
    /// the navigator does not show it (<see cref="OpeningCancelled"/>, <see cref="ConvergeWithoutShow"/>).
    /// Adopted (<c>Connect</c>, the caller shows the Form): in <c>Load</c> the Form is made invisible
    /// (opacity 0, moved outside every monitor) and closed — measured on Windows not to flash, modal or
    /// modeless.</para>
    /// </remarks>
    internal sealed class WindowLifecycleController : ICloseSink
    {
        private readonly Form _form;
        private readonly ICloseParticipant _presenter;
        private readonly Action<object, InteractionStatus> _onClosed;
        private readonly bool _disposeForm;
        private readonly ILogger _logger;

        // The result of the close attempt in progress (user ✕ => Cancel, no result).
        private object _pendingResult;
        private InteractionStatus _pendingStatus = InteractionStatus.Cancel;

        private bool _bypassGateOnce;          // continuation of an approved attempt / cancelled opening
        private bool _gatePending;             // async CanClose has not answered yet
        private bool _gateRunning;             // CanClose is running synchronously right now
        private bool _closed;                  // FormClosed has run
        private bool _wired;

        // Opening: until the window starts showing a RequestClose cancels the opening.
        private bool _opening = true;
        private bool _openingCancelled;

        // The candidate a RequestClose supplied. Used by the close attempt its own Form.Close() starts;
        // when that attempt is refused before the gate ran (an owned window is still deciding), it is
        // kept for the replay of that same close and dropped by any other close attempt.
        private bool _hasSeed;
        private object _seedResult;
        private InteractionStatus _seedStatus;
        private bool _pushPending;             // a RequestClose started a close whose FormClosing has not run yet
                                               // (a modal dialog runs it later, inside its modal loop)
        private bool _replay;                  // the next FormClosing replays a close an owned window held (consumed there)

        // A RequestClose made while the gate is deciding (X -> "Save changes?" -> Yes -> saved) is the
        // result of that close: held here and applied only if the gate allows.
        private bool _hasGateResult;
        private object _gateResult;
        private InteractionStatus _gateStatus;

        private readonly SessionEndingHook _sessionHook = new SessionEndingHook();

        // Windows asks the application to end the session (WM_QUERYENDSESSION) while this counter is
        // non-zero. WinForms closes owned / MDI child windows first and tells them "FormOwnerClosing",
        // so without it a child would prompt during a shutdown. UI-thread only.
        [ThreadStatic] private static int s_sessionEnding;

        // Lets an owned window's async "allow" mark its owner's re-close as a replay of the owner's close.
        private static readonly ConditionalWeakTable<Form, WindowLifecycleController> Controllers =
            new ConditionalWeakTable<Form, WindowLifecycleController>();

        /// <param name="view">The window's view; must be a <see cref="Form"/>.</param>
        /// <param name="presenter">The presenter whose close gate and close sink this controller drives.</param>
        /// <param name="onClosed">Receives the close result and status once the window has closed.</param>
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
            _logger = Kibinago.Services.ServiceLocator.Current
                .ResolveRequired<Kibinago.Logging.ILoggerFactory>()
                .CreateLogger(typeof(WindowLifecycleController));
        }

        internal void BindSink() => _presenter.BindCloseSink(this);

        /// <param name="adopted">Connect: the caller may hand over a Form that is already on screen, or was
        /// shown and hidden. Managed (false): the navigator ends the opening itself with
        /// <see cref="BeginShowing"/> — a handle created during initialization says nothing about showing.</param>
        internal void WireFormEvents(bool adopted = false)
        {
            if (_wired) return;
            _wired = true;
            if (!Controllers.TryGetValue(_form, out _)) Controllers.Add(_form, this);
            _form.Load += OnFormLoad;
            _form.FormClosing += OnFormClosing;
            _form.FormClosed += OnFormClosed;
            _form.Disposed += OnFormDisposed;
            _form.HandleCreated += OnHandleCreated;
            _form.HandleDestroyed += OnHandleDestroyed;
            if (_form.IsHandleCreated) _sessionHook.AssignHandle(_form.Handle);

            // Connected to a Form that is on screen, or was shown and hidden (Load already ran and will not
            // run again — its handle stays): it is past opening.
            if (adopted)
            {
                // A Form already on screen is past opening. One that is not visible — never shown (even if
                // its handle exists) or shown and hidden (its Load will not run again) — is still opening
                // until it becomes visible: Load or, for a re-shown Form, VisibleChanged ends it.
                if (_form.Visible) EndOpeningAsShown();
                else _form.VisibleChanged += OnFormVisibleChanged;
            }
        }

        /// <summary>Undo <see cref="WireFormEvents"/> (Connect failed after wiring).</summary>
        internal void UnwireFormEvents()
        {
            if (!_wired) return;
            _wired = false;
            if (Controllers.TryGetValue(_form, out var registered) && ReferenceEquals(registered, this))
                Controllers.Remove(_form);
            _form.Load -= OnFormLoad;
            _form.VisibleChanged -= OnFormVisibleChanged;
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.Disposed -= OnFormDisposed;
            _form.HandleCreated -= OnHandleCreated;
            _form.HandleDestroyed -= OnHandleDestroyed;
            _sessionHook.ReleaseHandle();
        }

        private void OnHandleCreated(object sender, EventArgs e) => _sessionHook.AssignHandle(_form.Handle);
        private void OnHandleDestroyed(object sender, EventArgs e) => _sessionHook.ReleaseHandle();

        /// <summary>Counts WM_QUERYENDSESSION on this window's handle, without swallowing the message.</summary>
        private sealed class SessionEndingHook : NativeWindow
        {
            private const int WM_QUERYENDSESSION = 0x0011;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg != WM_QUERYENDSESSION) { base.WndProc(ref m); return; }
                s_sessionEnding++;
                try { base.WndProc(ref m); }
                finally { s_sessionEnding--; }
            }
        }

        // ---------------- Opening ----------------

        /// <summary>True when the presenter asked to close before the window started showing (e.g.
        /// <c>RequestClose</c> in <c>OnInitialize</c>): the managed caller does not show it and calls
        /// <see cref="ConvergeWithoutShow"/>.</summary>
        internal bool OpeningCancelled => _openingCancelled;

        /// <summary>Managed: called right before <c>Show</c> / <c>ShowDialog</c>. From here on a
        /// <c>RequestClose</c> is a close through the gate.</summary>
        internal void BeginShowing() => _opening = false;

        /// <summary>Managed, opening cancelled: deliver the result and dispose presenter and Form without
        /// ever showing the window.</summary>
        internal void ConvergeWithoutShow()
        {
            _opening = false;
            UnwireFormEvents();
            _closed = true;
            try { _onClosed?.Invoke(_pendingResult, _pendingStatus); }
            finally
            {
                try { (_presenter as IDisposable)?.Dispose(); }
                finally { _form.Dispose(); }   // never shown: nobody else will dispose it
            }
        }

        // Adopted: the caller shows the Form. Load runs before the window is first painted.
        private void OnFormLoad(object sender, EventArgs e)
        {
            _form.Load -= OnFormLoad;
            if (!_opening) return;
            _opening = false;
            if (!_openingCancelled) return;
            CancelOpeningInLoad();
        }

        // Adopted Form that was shown and hidden before Connect: its Load will not run again.
        private void OnFormVisibleChanged(object sender, EventArgs e)
        {
            if (!_form.Visible) return;
            _form.VisibleChanged -= OnFormVisibleChanged;
            if (!_opening) return;
            _opening = false;
            if (_openingCancelled) CancelOpeningInLoad();
        }

        private bool _hidden;
        private double _savedOpacity;
        private Point _savedLocation;

        // Measured on Windows (V-34): closing in Load or Shown flashes a modal window; making it invisible
        // first does not — opacity 0 alone, set in Load, still flashes a little, so the window is also moved
        // outside every monitor (a fixed (-9999, -9999) is not enough: monitors left of / above the
        // primary have negative coordinates). Then modal: BeginInvoke(Close); modeless: Close().
        private void CancelOpeningInLoad()
        {
            if (!_form.IsMdiChild)   // opacity has no effect on an MDI child and its location is parent-relative
            {
                _hidden = true;
                _savedOpacity = _form.Opacity;
                _savedLocation = _form.Location;
                try { _form.Opacity = 0; } catch (Exception ex) { _logger?.LogDebug("Opacity unavailable: {Error}", ex.Message); }
                var all = SystemInformation.VirtualScreen;
                _form.Location = new Point(all.Left - _form.Width - 100, all.Top - _form.Height - 100);
            }

            if (_form.Modal)
            {
                try { _form.BeginInvoke(new Action(CloseCancelledOpening)); }
                catch (InvalidOperationException) { CloseCancelledOpening(); }
            }
            else
            {
                CloseCancelledOpening();
            }
        }

        private void CloseCancelledOpening()
        {
            if (_closed || _form.IsDisposed) return;

            // Whether another FormClosing handler refuses is only known at the end of FormClosing — and
            // for a modal dialog that happens later, inside its modal loop. A handler added last sees the
            // final answer; on a refusal it shows the window again (never an invisible live window).
            FormClosingEventHandler outcome = null;
            outcome = (s, e) =>
            {
                _form.FormClosing -= outcome;
                if (e.Cancel) RestoreAfterRefusedCancellation();
            };
            _form.FormClosing += outcome;

            _bypassGateOnce = true;   // cancelling the opening is not a user close: no gate
            _form.Close();
            if (_form.IsDisposed)
            {
                // Closed before it ever had a window message loop: WinForms disposed it without FormClosed,
                // and OnFormDisposed has ended it (the fallback covers a Form disposed while unwired).
                _form.FormClosing -= outcome;
                if (!_closed)
                {
                    UnwireFormEvents();
                    _closed = true;
                    Converge();
                }
            }
            // Otherwise FormClosed converges (modeless: already; modal: when its loop processes the close).
        }

        private void RestoreAfterRefusedCancellation()
        {
            _bypassGateOnce = false;
            _pendingResult = null;
            _pendingStatus = InteractionStatus.Cancel;
            if (!_hidden) return;
            _hidden = false;
            try { _form.Opacity = _savedOpacity; } catch (Exception) { /* opacity unavailable */ }
            _form.Location = _savedLocation;
        }

        private void EndOpeningAsShown()
        {
            _opening = false;
            if (!_openingCancelled) return;
            // The Form was already on screen (Connect after Show): the held request is a normal close.
            _openingCancelled = false;
            var result = _pendingResult;
            var status = _pendingStatus;
            _pendingResult = null;
            _pendingStatus = InteractionStatus.Cancel;
            Close(result, status);
        }

        // ---------------- Push (ICloseSink) ----------------

        public void Close(object result, InteractionStatus status)
        {
            if (_closed) return;

            if (_gateRunning || _gatePending)
            {
                // Not a second close: the gate's answer decides whether this close happens.
                _hasGateResult = true;
                _gateResult = result;
                _gateStatus = status;
                return;
            }

            if (_opening)
            {
                // Cancelling the opening: no gate; the last request's result wins.
                _openingCancelled = true;
                _pendingResult = result;
                _pendingStatus = status;
                return;
            }

            // A close of an open window, through the gate like ✕, seeded with this result.
            _hasSeed = true;
            _seedResult = result;
            _seedStatus = status;
            _pushPending = true;
            _form.Close();
        }

        // ---------------- Gate (FormClosing) ----------------

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_bypassGateOnce) { _bypassGateOnce = false; return; }

            var winFormsReason = e.CloseReason;
            var reason = CloseReasonMap.From(winFormsReason);
            if (reason == Kibinago.Common.CloseReason.ParentClosing && s_sessionEnding > 0)
                reason = Kibinago.Common.CloseReason.SystemShutdown;   // the owner is closing because the session ends
            bool systemClose = reason == Kibinago.Common.CloseReason.SystemShutdown
                            || reason == Kibinago.Common.CloseReason.TaskManager;

            // A close attempt from inside the gate (e.g. a dialog it shows pumps a message) is not asked
            // again — except a system-level close, which is never blocked.
            if (_gateRunning) { e.Cancel = !systemClose; return; }

            // Already refused (an owned window's "Cancel" reaches its owner as e.Cancel = true):
            // there is nothing to ask, and answering "allow" would override that veto. The candidate of
            // the RequestClose (or replay) this close belongs to stays for its replay; any other close
            // attempt drops it, so it can never be delivered for an unrelated close.
            if (e.Cancel)
            {
                if (!_pushPending && !_replay) ClearSeed();
                _pushPending = false;
                _replay = false;
                return;
            }

            if (_gatePending)
            {
                // An async CanClose is still deciding. A system-level close is never blocked (the
                // late answer then finds the form disposed and is ignored); anything else keeps the
                // window open without asking again (a second X click must not raise a second
                // prompt / save).
                e.Cancel = !systemClose;
                return;
            }

            // A fresh close attempt starts from its own candidate: the RequestClose that started it, the
            // replayed close of an owner, or the default (user ✕ => Cancel). Nothing from an earlier,
            // refused attempt leaks in.
            _pendingResult = null;
            _pendingStatus = InteractionStatus.Cancel;
            if (_hasSeed && (_pushPending || _replay))
            {
                _pendingResult = _seedResult;
                _pendingStatus = _seedStatus;
            }
            _replay = false;
            _pushPending = false;
            ClearSeed();

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
                    decision = reason == Kibinago.Common.CloseReason.SystemShutdown
                            || reason == Kibinago.Common.CloseReason.TaskManager;
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

        private void ClearSeed()
        {
            _hasSeed = false;
            _seedResult = null;
        }

        /// <summary>The async gate answered. Allow: re-issue the original close (gate skipped once, the
        /// same attempt's result kept). Block: stay open.</summary>
        private void OnAsyncDecision(bool allow, System.Windows.Forms.CloseReason originalReason)
        {
            if (_form.IsDisposed) return;

            if (_form.InvokeRequired)
            {
                try { _form.BeginInvoke(new Action(() => OnAsyncDecision(allow, originalReason))); }
                catch (InvalidOperationException) { /* handle gone: the window is closing anyway */ }
                return;
            }

            if (!_gatePending) return;   // stale: this attempt was already settled
            _gatePending = false;
            TakeGateResult(allow);
            if (!allow || _form.IsDisposed || _closed) return;   // blocked: leave the window open

            _bypassGateOnce = true;
            switch (originalReason)
            {
                case System.Windows.Forms.CloseReason.FormOwnerClosing when _form.Owner != null:
                    var owner = _form.Owner;
                    _form.Close();
                    ReplayClose(owner);
                    break;
                case System.Windows.Forms.CloseReason.MdiFormClosing when _form.MdiParent != null:
                    var parent = _form.MdiParent;
                    _form.Close();
                    ReplayClose(parent);
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

        // The owner's close was held while this window decided: replay it, keeping the owner's own
        // RequestClose candidate if it had one.
        private static void ReplayClose(Form owner)
        {
            // Consumed by the owner's next FormClosing (a modal owner runs it later, inside its loop).
            if (Controllers.TryGetValue(owner, out var controller)) controller._replay = true;
            owner.Close();
        }

        private void TakeGateResult(bool allowed)
        {
            if (allowed && _hasGateResult)
            {
                _pendingResult = _gateResult;
                _pendingStatus = _gateStatus;
            }
            else if (!allowed)
            {
                _pendingResult = null;
                _pendingStatus = InteractionStatus.Cancel;
            }
            _hasGateResult = false;
            _gateResult = null;
        }

        // ---------------- Converge (FormClosed) ----------------

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            UnwireFormEvents();
            _closed = true;
            ClearSeed();
            Converge();
        }

        // Disposed without FormClosed: the caller dropped a Form it never showed, or a container (a docking
        // host, an MDI parent) disposed the panes it held. Ends like a close — onClosed with the pending
        // result (Cancel unless a RequestClose set one), the presenter disposed — but cannot ask: no gate.
        // After a FormClosed the handler is already unwired, so nothing ends twice.
        private void OnFormDisposed(object sender, EventArgs e)
        {
            if (_closed) return;
            UnwireFormEvents();
            _closed = true;
            ClearSeed();
            Converge(formDisposed: true);
        }

        /// <param name="formDisposed">The Form is being disposed already (no second Dispose from its Disposed event).</param>
        private void Converge(bool formDisposed = false)
        {
            try { _onClosed?.Invoke(_pendingResult, _pendingStatus); }
            finally
            {
                try { (_presenter as IDisposable)?.Dispose(); }
                finally { if (_disposeForm && !formDisposed) _form.Dispose(); }
            }
        }
    }
}
