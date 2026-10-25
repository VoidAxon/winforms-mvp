using System;
using System.Threading.Tasks;
using Kibinago.Logging;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Shared base for window presenters carrying the close machinery: the Pull gate
    /// (<see cref="CanClose(CloseReason)"/> / <see cref="CanCloseAsync(CloseReason)"/>) and the Push sink
    /// (<see cref="RequestClose(InteractionStatus)"/>). Do not inherit directly — use
    /// <see cref="WindowPresenterBase{TView}"/> or <see cref="WindowPresenterBase{TView, TParam}"/>.
    /// </summary>
    /// <remarks>
    /// Closing is the Presenter's policy, so it lives here, not on the View. The framework calls
    /// <see cref="ICloseParticipant.CanCloseGate"/> (Pull) and injects the sink via
    /// <see cref="ICloseParticipant.BindCloseSink"/> (Push) — there are no events to subscribe to.
    /// </remarks>
    public abstract class WindowPresenterBaseCore<TView> : PresenterBase<TView>, ICloseParticipant
        where TView : IWindowView
    {
        private ICloseSink _closeSink;

        /// <summary>
        /// Pull gate (synchronous). Override to veto a close. Return <c>true</c> to allow.
        /// Inspect <paramref name="reason"/> — never block <see cref="CloseReason.SystemShutdown"/>
        /// or <see cref="CloseReason.TaskManager"/> with a modal prompt. Default allows.
        /// To report a result for this close (e.g. "Save changes?" -> Yes -> saved), call
        /// <c>RequestClose(result, status)</c> here and return <c>true</c>: while the gate is
        /// deciding, <c>RequestClose</c> sets the result of this close instead of closing again,
        /// and the result is dropped if the gate blocks.
        /// </summary>
        protected virtual bool CanClose(CloseReason reason) => true;

        /// <summary>
        /// Pull gate (asynchronous). Override when the decision needs to await something (a save,
        /// a server check, an async confirmation). Default returns the synchronous
        /// <see cref="CanClose(CloseReason)"/>. While the task is pending the window stays open and
        /// further close attempts are held off, except system shutdown / Task Manager closes. If
        /// the task faults, the close is blocked (system-level closes are still allowed) and the
        /// error is logged. A <c>RequestClose</c> made before the task completes sets the result
        /// of this close, as for <see cref="CanClose(CloseReason)"/>.
        /// </summary>
        protected virtual Task<bool> CanCloseAsync(CloseReason reason)
            => Task.FromResult(CanClose(reason));

        /// <summary>Push a close with no business result.</summary>
        protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)
            => PushClose(null, status);

        /// <summary>
        /// Push a close carrying a typed business result. <typeparamref name="TResult"/> is inferred
        /// from <paramref name="result"/>, so the call stays compile-time typed without the result
        /// type occupying a class type parameter (which would collide with <c>TParam</c>). The result
        /// is boxed to <see cref="object"/> and cast back to the concrete type at the show / Connect
        /// boundary. Note: <c>RequestClose(InteractionStatus.Cancel)</c> resolves to the no-result
        /// overload above (C# prefers the non-generic candidate).
        /// </summary>
        protected void RequestClose<TResult>(TResult result, InteractionStatus status = InteractionStatus.Ok)
            => PushClose(result, status);

        // A close requested before any host bound a sink (e.g. RequestClose in OnInitialize when
        // the caller Initialized before Connect) is held and delivered on bind instead of dropped.
        private bool _hasPendingClose;
        private object _pendingCloseResult;
        private InteractionStatus _pendingCloseStatus;

        private void PushClose(object result, InteractionStatus status)
        {
            if (_closeSink != null)
            {
                _closeSink.Close(result, status);
                return;
            }
            _hasPendingClose = true;
            _pendingCloseResult = result;
            _pendingCloseStatus = status;
        }

        void ICloseParticipant.BindCloseSink(ICloseSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_closeSink != null)
                throw new InvalidOperationException(
                    "A close sink is already bound. Connect must not be called more than once on the same presenter.");
            _closeSink = sink;

            if (_hasPendingClose)
            {
                _hasPendingClose = false;
                var result = _pendingCloseResult;
                _pendingCloseResult = null;
                sink.Close(result, _pendingCloseStatus);
            }
        }
        void ICloseParticipant.CanCloseGate(CloseReason reason, Action<bool> proceed)
        {
            // A system-level close is never blocked and never prompted, so the presenter is not asked:
            // CanClose / CanCloseAsync need no reason check of their own.
            if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
            {
                proceed(true);
                return;
            }

            // A synchronous throw from CanCloseAsync itself propagates: the framework blocks the
            // close and logs it. A faulted task is answered here, the same way, whenever it faults.
            var decision = CanCloseAsync(reason) ?? Task.FromResult(true);
            if (decision.IsCompleted)
            {
                proceed(Decide(decision, reason));
                return;
            }

            // The framework marshals proceed back to the UI thread.
            decision.ContinueWith(t => proceed(Decide(t, reason)), TaskScheduler.Default);
        }

        private bool Decide(Task<bool> completed, CloseReason reason)
        {
            if (completed.Status == TaskStatus.RanToCompletion)
                return completed.Result;
            if (completed.IsFaulted)
                Logger.LogError(completed.Exception, "{Presenter}.CanCloseAsync failed; blocking the close", GetType().Name);
            // Faulted or cancelled: block, but never a system-level close.
            return reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager;
        }
    }
}
