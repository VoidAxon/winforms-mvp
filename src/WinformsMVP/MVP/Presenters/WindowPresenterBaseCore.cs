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
        /// inside a continuation if needed. Uses <see cref="Action{T}"/>, never <c>Task</c>, so it
        /// is net40-safe. Default forwards to the synchronous <see cref="CanClose(CloseReason)"/>.
        /// </summary>
        protected virtual void CanClose(CloseReason reason, Action<bool> proceed)
            => proceed(CanClose(reason));

        /// <summary>Push a close with no result. Use the <c>RequestClose(result, status)</c>
        /// extension (from <see cref="IRequestClose{TResult}"/>) when returning a typed result.</summary>
        protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)
            => _closeSink?.Close(null, status);

        void ICloseParticipant.BindCloseSink(ICloseSink sink) => _closeSink = sink;
        void ICloseParticipant.RequestCloseCore(object result, InteractionStatus status)
            => _closeSink?.Close(result, status);
        void ICloseParticipant.CanCloseGate(CloseReason reason, Action<bool> proceed)
            => CanClose(reason, proceed);
    }
}
