using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Testing
{
    /// <summary>
    /// Hosts a presenter in a unit test: attaches a (mock) view of any kind — no Form needed —
    /// runs the real initialization (<c>RegisterViewActions</c>, action-binder auto-bind,
    /// <c>OnInitialize</c>) and records what the presenter pushes through <c>RequestClose</c>.
    /// </summary>
    /// <remarks>
    /// Production code hosts presenters with <c>WindowNavigator</c> or <c>Connect</c>; the
    /// presenter's own <c>AttachView</c>/<c>Initialize</c> are not part of its public surface.
    /// <code>
    /// var view = new MockOrderView();
    /// using (var host = PresenterTestHost.Start(new OrderPresenter(repo), view))
    /// {
    ///     view.Binder.Dispatch(CommonActions.Save);
    ///     Assert.True(host.CloseRequested);
    ///     Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
    /// }
    /// </code>
    /// </remarks>
    public sealed class PresenterTestHost : IDisposable
    {
        private readonly IPresenter _presenter;

        private PresenterTestHost(IPresenter presenter)
        {
            _presenter = presenter;
        }

        /// <summary>Attaches <paramref name="view"/> to a parameterless presenter and initializes it.</summary>
        public static PresenterTestHost Start<TPresenter, TView>(TPresenter presenter, TView view)
            where TPresenter : IPresenter, IViewAttacher<TView>, IInitializable
            where TView : IViewBase
        {
            var host = Attach(presenter, view);
            Run(presenter, presenter.Initialize);
            return host;
        }

        /// <summary>Attaches <paramref name="view"/> to a parameterized presenter and initializes it with <paramref name="parameters"/>.</summary>
        public static PresenterTestHost Start<TPresenter, TView, TParam>(TPresenter presenter, TView view, TParam parameters)
            where TPresenter : IPresenter, IViewAttacher<TView>, IInitializable<TParam>
            where TView : IViewBase
        {
            var host = Attach(presenter, view);
            Run(presenter, () => presenter.Initialize(parameters));
            return host;
        }

        private static PresenterTestHost Attach<TPresenter, TView>(TPresenter presenter, TView view)
            where TPresenter : IPresenter, IViewAttacher<TView>
            where TView : IViewBase
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (view == null) throw new ArgumentNullException(nameof(view));

            var host = new PresenterTestHost(presenter);
            presenter.AttachView(view);
            // Bound before Initialize, as the framework does, so a RequestClose made in
            // OnInitialize is recorded too.
            (presenter as ICloseParticipant)?.BindCloseSink(new RecordingSink(host));
            return host;
        }

        private static void Run(IPresenter presenter, Action initialize)
        {
            try { initialize(); }
            catch
            {
                presenter.Dispose();
                throw;
            }
        }

        /// <summary>True once the presenter called <c>RequestClose</c>.</summary>
        public bool CloseRequested => CloseCount > 0;

        /// <summary>How many times the presenter called <c>RequestClose</c>.</summary>
        public int CloseCount { get; private set; }

        /// <summary>The status of the last <c>RequestClose</c>, or null if none.</summary>
        public InteractionStatus? CloseStatus { get; private set; }

        /// <summary>The result of the last <c>RequestClose</c> (null for a no-result close).</summary>
        public object CloseResult { get; private set; }

        /// <summary>
        /// Asks the presenter whether the window may close, through the same gate the framework
        /// uses on X / Alt+F4. Returns null while an asynchronous <c>CanClose</c> has not answered.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter is not a window presenter.</exception>
        public bool? CanClose(CloseReason reason = CloseReason.Normal)
        {
            var participant = _presenter as ICloseParticipant
                ?? throw new InvalidOperationException(
                    _presenter.GetType().Name + " is not a window presenter; only window presenters have a close gate.");
            bool? decision = null;
            participant.CanCloseGate(reason, ok => decision = ok);
            return decision;
        }

        /// <summary>Disposes the presenter (runs <c>Cleanup</c> and releases <c>Disposables</c>).</summary>
        public void Dispose() => _presenter.Dispose();

        private sealed class RecordingSink : ICloseSink
        {
            private readonly PresenterTestHost _host;
            public RecordingSink(PresenterTestHost host) { _host = host; }

            public void Close(object result, InteractionStatus status)
            {
                _host.CloseCount++;
                _host.CloseResult = result;
                _host.CloseStatus = status;
            }
        }
    }
}
