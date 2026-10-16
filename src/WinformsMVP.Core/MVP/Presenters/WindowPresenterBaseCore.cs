using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Shared base for window presenters carrying the close machinery: the Pull gate
    /// (<see cref="CanClose(CloseReason)"/> / its callback overload) and the Push sink
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
        /// </summary>
        protected virtual bool CanClose(CloseReason reason) => true;

        /// <summary>
        /// Pull gate (asynchronous). Override when the decision needs a callback (async save /
        /// server check). Call <paramref name="proceed"/>(true) to allow, (false) to block — from
        /// inside a continuation, on any thread (the framework marshals back to the UI thread).
        /// While an answer is pending, further close attempts are held off rather than asking
        /// again. Default forwards to the synchronous <see cref="CanClose(CloseReason)"/>.
        /// </summary>
        protected virtual void CanClose(CloseReason reason, Action<bool> proceed)
            => proceed(CanClose(reason));

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
            => CanClose(reason, proceed);
    }
}
