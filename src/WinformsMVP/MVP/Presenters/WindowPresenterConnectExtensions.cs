using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Adopted hosting: connect a window Presenter to a Form you create and <c>Show</c> yourself
    /// (legacy migration, the application shell, or a Form that owns its own Presenter). Does the
    /// same wiring as <c>WindowNavigator</c> — attach + initialize + Pull bridge + Push sink +
    /// result converging — but the caller owns the <c>Show</c>. Idempotent: a Presenter the caller
    /// already attached/initialized is only given the close controller.
    /// </summary>
    /// <remarks>
    /// Single-owner rule: a Form connected here must NOT also be shown through
    /// <see cref="Services.IWindowNavigator"/>. The <c>view is Form</c> check happens in the
    /// controller constructor (the framework's one runtime view-to-Form boundary).
    /// </remarks>
    public static class WindowPresenterConnectExtensions
    {
        /// <summary>
        /// Connects a no-param presenter to an already-created Form view it does not return a
        /// typed result. The caller owns <c>Show</c>; this method owns attach + initialize + close
        /// wiring. Idempotent when called after a manual <c>AttachView</c>/<c>Initialize</c>.
        /// </summary>
        public static void Connect<TView>(this WindowPresenterBase<TView> presenter,
            TView view, Action<InteractionResult> onClosed = null) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(),
                (res, status) => onClosed?.Invoke(BuildResult(status)), disposeForm: true);

        /// <summary>
        /// Connects a no-param presenter that returns a typed result. The caller owns <c>Show</c>.
        /// Idempotent when called after a manual <c>AttachView</c>/<c>Initialize</c>.
        /// </summary>
        public static void Connect<TView, TResult>(this WindowPresenterBase<TView> presenter,
            TView view, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(),
                (res, status) => onClosed?.Invoke(BuildResult<TResult>(res, status)), disposeForm: true);

        /// <summary>
        /// Connects a parameterized presenter that does not return a typed result.
        /// The caller owns <c>Show</c>. Idempotent when already attached/initialized.
        /// </summary>
        public static void Connect<TView, TParam>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult> onClosed = null) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(param),
                (res, status) => onClosed?.Invoke(BuildResult(status)), disposeForm: true);

        /// <summary>
        /// Connects a parameterized presenter that returns a typed result.
        /// The caller owns <c>Show</c>. Idempotent when already attached/initialized.
        /// </summary>
        public static void Connect<TView, TParam, TResult>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(param),
                (res, status) => onClosed?.Invoke(BuildResult<TResult>(res, status)), disposeForm: true);

        private static void ConnectCore(object presenter, IWindowView view,
            Action initialize, Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            // Construct first: validates `view is Form` before any side effect (no half-attach).
            var controller = new WindowLifecycleController(
                view, (ICloseParticipant)presenter, onClosed, disposeForm);

            var attachable = (IViewAttachable)presenter;
            if (!attachable.IsViewAttached)
            {
                attachable.AttachView(view);
                controller.BindSink();   // sink before Initialize
                initialize();
            }
            else
            {
                controller.BindSink();   // already attached/initialized: just add the sink
            }
            controller.WireFormEvents();
        }

        private static InteractionResult BuildResult(InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult.Ok();
                case InteractionStatus.Error: return InteractionResult.Error("Operation failed");
                default: return InteractionResult.Cancel();
            }
        }

        private static InteractionResult<TResult> BuildResult<TResult>(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult<TResult>.Ok(result is TResult typed ? typed : default(TResult));
                case InteractionStatus.Error: return InteractionResult<TResult>.Error("Operation failed");
                default: return InteractionResult<TResult>.Cancel();
            }
        }
    }
}
