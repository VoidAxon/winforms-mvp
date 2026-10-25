using System;
using System.Threading.Tasks;
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
            host.EndOpening();
            return host;
        }

        /// <summary>Attaches <paramref name="view"/> to a parameterized presenter and initializes it with <paramref name="parameters"/>.</summary>
        public static PresenterTestHost Start<TPresenter, TView, TParam>(TPresenter presenter, TView view, TParam parameters)
            where TPresenter : IPresenter, IViewAttacher<TView>, IInitializable<TParam>
            where TView : IViewBase
        {
            var host = Attach(presenter, view);
            Run(presenter, () => presenter.Initialize(parameters));
            host.EndOpening();
            return host;
        }

        /// <summary>
        /// Like <see cref="Start{TPresenter, TView}(TPresenter, TView)"/>, but the presenter resolves
        /// its framework services (<c>Messages</c>, <c>Navigator</c>, <c>Logger</c>, ...) from
        /// <paramref name="services"/> instead of <c>ServiceLocator.Current</c>, so tests stay
        /// independent of each other. Start from <see cref="TestServices.Create"/> and register the
        /// fakes the presenter uses; anything not registered is an error, not a fallback to the
        /// global services. <paramref name="services"/> stays owned by the caller.
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="services"/> has no
        /// <c>ILoggerFactory</c>, or the presenter already resolved framework services (e.g. in its
        /// constructor).</exception>
        public static PresenterTestHost Start<TPresenter, TView>(IServiceProvider services, TPresenter presenter, TView view)
            where TPresenter : IPresenter, IViewAttacher<TView>, IInitializable
            where TView : IViewBase
        {
            BindServices(services, presenter, view);
            return Start(presenter, view);
        }

        /// <summary>The parameterized form of
        /// <see cref="Start{TPresenter, TView}(IServiceProvider, TPresenter, TView)"/>.</summary>
        public static PresenterTestHost Start<TPresenter, TView, TParam>(IServiceProvider services, TPresenter presenter, TView view, TParam parameters)
            where TPresenter : IPresenter, IViewAttacher<TView>, IInitializable<TParam>
            where TView : IViewBase
        {
            BindServices(services, presenter, view);
            return Start(presenter, view, parameters);
        }

        // Everything is checked before the presenter is touched, so a rejected call leaves it unstarted.
        private static void BindServices<TPresenter, TView>(IServiceProvider services, TPresenter presenter, TView view)
            where TPresenter : IPresenter
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (!(presenter is IPresenterServicesBinding binding))
                throw new ArgumentException(
                    presenter.GetType().Name + " does not derive from a framework presenter base, so it cannot be given services.",
                    nameof(presenter));
            if (services.GetService(typeof(WinformsMVP.Logging.ILoggerFactory)) == null)
                throw new InvalidOperationException(
                    "The services have no ILoggerFactory, which every presenter needs. " +
                    "Start from TestServices.Create() (it registers a silent one) or register an ILoggerFactory.");

            binding.BindServices(services);
        }

        private static PresenterTestHost Attach<TPresenter, TView>(TPresenter presenter, TView view)
            where TPresenter : IPresenter, IViewAttacher<TView>
            where TView : IViewBase
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (view == null) throw new ArgumentNullException(nameof(view));

            // A presenter that is already hosted is rejected before the dispose-on-failure guard:
            // it may still be serving another view.
            if (presenter is IViewAttachable attachable && attachable.IsViewAttached)
                throw new InvalidOperationException(
                    presenter.GetType().Name + " already has a view attached; start a new presenter instance.");

            var host = new PresenterTestHost(presenter);
            Run(presenter, () =>
            {
                presenter.AttachView(view);
                // Bound before Initialize, as the framework does, so a RequestClose made in
                // OnInitialize is recorded too.
                (presenter as ICloseParticipant)?.BindCloseSink(new RecordingSink(host));
            });
            return host;
        }

        // A presenter whose attach/initialize failed can never run: dispose it and rethrow.
        private static void Run(IPresenter presenter, Action step)
        {
            try { step(); }
            catch
            {
                presenter.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Completes when the presenter's <c>OnInitializeAsync</c> has finished (failures are
        /// logged by the presenter, not faulted). <c>await host.Initialized;</c> before asserting
        /// on data loaded asynchronously.
        /// </summary>
        public Task Initialized => (_presenter as IViewAttachable)?.InitializationTask ?? Task.CompletedTask;

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
            var decision = RunGate(AsCloseParticipant(), reason);
            return decision.IsCompleted ? decision.Result : (bool?)null;
        }

        /// <summary>
        /// Asks the presenter whether the window may close and awaits an asynchronous
        /// <c>CanCloseAsync</c> answer.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter is not a window presenter.</exception>
        public Task<bool> CanCloseAsync(CloseReason reason = CloseReason.Normal)
            => RunGate(AsCloseParticipant(), reason);

        private ICloseParticipant AsCloseParticipant()
            => _presenter as ICloseParticipant
                ?? throw new InvalidOperationException(
                    _presenter.GetType().Name + " is not a window presenter; only window presenters have a close gate.");

        // Mirrors the real window: while an asynchronous gate is pending, another close attempt is
        // held off (it gets the pending answer, the presenter is not asked again) except system
        // shutdown / Task Manager, which go through. A RequestClose made while the gate is
        // deciding (X -> "Save changes?" -> Yes -> saved) is the result of that close: recorded
        // only if the gate allows.
        // The deferred gate answer arrives on a thread-pool thread, so gate state is locked.
        private readonly object _gateLock = new object();
        private TaskCompletionSource<bool> _pendingGate;
        private bool _hasGateResult;
        private object _gateResult;
        private InteractionStatus _gateStatus;

        private Task<bool> RunGate(ICloseParticipant participant, CloseReason reason)
            => RunGate(participant, reason, hasSeed: false, seedResult: null, seedStatus: InteractionStatus.Cancel);

        // seed: the result a RequestClose supplied when it started this close attempt; recorded if the
        // gate allows and did not supply its own.
        private Task<bool> RunGate(ICloseParticipant participant, CloseReason reason,
            bool hasSeed, object seedResult, InteractionStatus seedStatus)
        {
            var gate = new TaskCompletionSource<bool>();
            lock (_gateLock)
            {
                if (_pendingGate != null)
                {
                    return reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager
                        ? Task.FromResult(true)
                        : _pendingGate.Task;
                }
                _pendingGate = gate;
            }

            try
            {
                participant.CanCloseGate(reason, ok =>
                {
                    lock (_gateLock)
                    {
                        if (_pendingGate != gate) return;
                        _pendingGate = null;
                        if (ok && _hasGateResult) Record(_gateResult, _gateStatus);
                        else if (ok && hasSeed) Record(seedResult, seedStatus);
                        ClearGateResult();
                    }
                    gate.TrySetResult(ok);
                });
            }
            catch
            {
                lock (_gateLock)
                {
                    if (_pendingGate == gate) _pendingGate = null;
                    ClearGateResult();
                }
                throw;
            }
            return gate.Task;
        }

        // Callers hold _gateLock.
        private void ClearGateResult()
        {
            _hasGateResult = false;
            _gateResult = null;
        }

        private void Record(object result, InteractionStatus status)
        {
            CloseCount++;
            CloseResult = result;
            CloseStatus = status;
        }

        /// <summary>Disposes the presenter (runs <c>Cleanup</c> and releases <c>Disposables</c>).</summary>
        /// <summary>
        /// Completes when the close attempt a <c>RequestClose</c> started has been decided — at once when
        /// nothing is pending. A <c>RequestClose</c> goes through <c>CanClose</c> / <c>CanCloseAsync</c>,
        /// and dispatching the action that called it does not wait for an asynchronous gate:
        /// <code>
        /// view.Binder.Dispatch(EditActions.Close);
        /// gate.SetResult(true);                      // the presenter's CanCloseAsync answers
        /// await host.WaitForCloseDecisionAsync();
        /// Assert.True(host.CloseRequested);
        /// </code>
        /// </summary>
        public Task WaitForCloseDecisionAsync()
        {
            lock (_gateLock) return (Task)_pendingGate?.Task ?? Task.CompletedTask;
        }

        // Until the synchronous part of Start returns, the presenter is opening: a RequestClose then
        // cancels the opening (no gate, as the real host does before showing the window).
        private bool _opening = true;

        private void EndOpening()
        {
            lock (_gateLock) _opening = false;
        }

        public void Dispose() => _presenter.Dispose();

        private sealed class RecordingSink : ICloseSink
        {
            private readonly PresenterTestHost _host;
            public RecordingSink(PresenterTestHost host) { _host = host; }

            public void Close(object result, InteractionStatus status)
            {
                lock (_host._gateLock)
                {
                    if (_host._pendingGate != null)
                    {
                        // The gate is deciding: this is the result of that close, not a second one.
                        _host._hasGateResult = true;
                        _host._gateResult = result;
                        _host._gateStatus = status;
                        return;
                    }
                    if (_host._opening)
                    {
                        // Cancelling the opening: no gate; repeated requests are one cancellation.
                        _host.CloseCount = 1;
                        _host.CloseResult = result;
                        _host.CloseStatus = status;
                        return;
                    }
                }
                // A RequestClose on an open window goes through the gate, like X.
                var participant = _host._presenter as ICloseParticipant;
                if (participant == null) { lock (_host._gateLock) _host.Record(result, status); return; }
                // A gate that throws synchronously (e.g. an unscripted fake confirmation) fails the test
                // through the RequestClose call — a real window would log it and stay open.
                _host.RunGate(participant, CloseReason.Normal, hasSeed: true, seedResult: result, seedStatus: status);
            }
        }
    }
}
