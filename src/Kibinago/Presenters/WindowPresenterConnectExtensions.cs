using System;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Services;

namespace Kibinago.Presenters
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
            => ConnectCore(presenter, view, () => ((IInitializable)presenter).Initialize(),
                (res, status) => onClosed?.Invoke(CloseResults.Build(res, status)), disposeForm: true);

        /// <summary>
        /// Connects a no-param presenter that returns a typed result. The caller owns <c>Show</c>.
        /// Idempotent when called after a manual <c>AttachView</c>/<c>Initialize</c>.
        /// </summary>
        public static void Connect<TView, TResult>(this WindowPresenterBase<TView> presenter,
            TView view, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => ((IInitializable)presenter).Initialize(),
                (res, status) => onClosed?.Invoke(CloseResults.Build<TResult>(res, status)), disposeForm: true);

        /// <summary>
        /// Connects a parameterized presenter that does not return a typed result.
        /// The caller owns <c>Show</c>. Idempotent when already attached/initialized.
        /// </summary>
        public static void Connect<TView, TParam>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult> onClosed = null) where TView : IWindowView
            => ConnectCore(presenter, view, () => ((IInitializable<TParam>)presenter).Initialize(param),
                (res, status) => onClosed?.Invoke(CloseResults.Build(res, status)), disposeForm: true);

        /// <summary>
        /// Connects a parameterized presenter that returns a typed result.
        /// The caller owns <c>Show</c>. Idempotent when already attached/initialized.
        /// </summary>
        public static void Connect<TView, TParam, TResult>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => ((IInitializable<TParam>)presenter).Initialize(param),
                (res, status) => onClosed?.Invoke(CloseResults.Build<TResult>(res, status)), disposeForm: true);

        private static void ConnectCore(object presenter, IWindowView view,
            Action initialize, Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (view == null) throw new ArgumentNullException(nameof(view));

            var attachable = (IViewAttachable)presenter;
            if (attachable.IsViewAttached && !ReferenceEquals(attachable.AttachedView, view))
                throw new InvalidOperationException(
                    presenter.GetType().Name + " is already attached to a different view; " +
                    "Connect it to the view it was attached to.");

            // Construct first: validates `view is Form` before any side effect (no half-attach).
            var controller = new WindowLifecycleController(
                view, (ICloseParticipant)presenter, onClosed, disposeForm);

            // Each step is skipped when the caller already did it by hand, so Connect stays
            // idempotent for attached-only and attached+initialized presenters alike. A failed
            // attach/initialize leaves a presenter that can never run, so it is released (the
            // caller owns the Form).
            // The Form's events are wired first, so a close delivered from here on (held before Connect, or
            // requested during initialization) is converged; until the Form's Load it cancels the opening:
            // the window is made invisible and closed there, so the caller's Show / Application.Run shows
            // nothing.
            controller.WireFormEvents(adopted: true);
            try
            {
                if (!attachable.IsViewAttached)
                    DisposeOnFailure(presenter, () => attachable.AttachView(view));

                // Throws for a second Connect of an already-connected presenter. Deliberately outside
                // the dispose-on-failure guard: that presenter is still serving its window.
                controller.BindSink();   // before Initialize; also delivers a close requested earlier

                if (!attachable.IsInitialized)
                    DisposeOnFailure(presenter, initialize);
            }
            catch
            {
                controller.UnwireFormEvents();
                throw;
            }
        }

        private static void DisposeOnFailure(object presenter, Action step)
        {
            try { step(); }
            catch
            {
                (presenter as IDisposable)?.Dispose();
                throw;
            }
        }
    }
}
