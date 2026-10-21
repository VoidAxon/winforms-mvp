using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;
using CloseReason = WinformsMVP.Common.CloseReason;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Regression tests for presenter / window lifecycle edge cases: misuse that used to fail
    /// silently (parameterized presenter shown without parameters, close requested before a sink
    /// was bound, mismatched result types) and failure paths that used to leak.
    /// </summary>
    public class LifecycleHardeningTests
    {
        #region Test doubles

        public interface IHardView : IWindowView { }

        private sealed class HardForm : Form, IHardView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class NonFormView : IHardView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class ParamPresenter : WindowPresenterBase<IHardView, int>
        {
            public bool Disposed;
            public bool Initialized;
            protected override void OnViewAttached() { }
            protected override void OnInitialize(int parameters) => Initialized = true;
            protected override void Cleanup() => Disposed = true;
        }

        /// <summary>Another screen on the same view type (its window keys are its own).</summary>
        private sealed class OtherPresenter : WindowPresenterBase<IHardView>
        {
            public int InitCount;
            public bool Disposed;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => InitCount++;
            protected override void Cleanup() => Disposed = true;
        }

        /// <summary>Runs a configurable callback from OnInitialize.</summary>
        private sealed class ScriptedPresenter : WindowPresenterBase<IHardView>
        {
            public Action<ScriptedPresenter> OnInit;
            public Action OnCleanup;
            public int InitCount;
            public int CleanupCount;
            public bool Disposed => CleanupCount > 0;

            protected override void OnViewAttached() { }
            protected override void OnInitialize()
            {
                InitCount++;
                OnInit?.Invoke(this);
            }
            protected override void Cleanup()
            {
                CleanupCount++;
                OnCleanup?.Invoke();
            }

            public void Close<T>(T result, InteractionStatus status) => RequestClose(result, status);
            public void CloseNoResult(InteractionStatus status) => RequestClose(status);
            public CompositeDisposable Bag => Disposables;
            public void DisposeFromInside() => Dispose();
        }

        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object Result, InteractionStatus Status)> Closes = new List<(object, InteractionStatus)>();
            public void Close(object result, InteractionStatus status) => Closes.Add((result, status));
        }

        private static WindowNavigator NewNavigator()
        {
            var registry = new ViewMappingRegister();
            registry.Register<IHardView>(() => new HardForm());
            return new WindowNavigator(registry);
        }

        #endregion

        #region Navigator: parameterized presenter through a parameterless overload

        [Fact]
        public void ShowWindowAsModal_ParamOnlyPresenterWithoutParams_ThrowsAndDisposes()
        {
            var presenter = new ParamPresenter();

            var ex = Assert.Throws<InvalidOperationException>(
                () => NewNavigator().ShowWindowAsModal<ParamPresenter, object>(presenter));

            Assert.Contains("WithParam", ex.Message);
            Assert.True(presenter.Disposed);
            Assert.False(presenter.Initialized);
        }

        [Fact]
        public void ShowWindow_ParamOnlyPresenterWithoutParams_ThrowsAndDisposes()
        {
            var presenter = new ParamPresenter();

            Assert.Throws<InvalidOperationException>(
                () => NewNavigator().ShowWindow(presenter));

            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void ShowWindow_OwnerNotFormBacked_ThrowsBeforeCreatingWindowAndDisposes()
        {
            var presenter = new ScriptedPresenter();

            Assert.Throws<ArgumentException>(
                () => NewNavigator().ShowWindow(presenter, owner: new NonFormView()));

            Assert.True(presenter.Disposed);
            Assert.Equal(0, presenter.InitCount);
        }

        #endregion

        #region Navigator: result conversion (close-before-show path, no dialog is shown)

        private static InteractionResult<TResult> ShowModalClosingInInit<TResult>(Action<ScriptedPresenter> close)
        {
            var presenter = new ScriptedPresenter { OnInit = close };
            return NewNavigator().ShowWindowAsModal<ScriptedPresenter, TResult>(presenter);
        }

        [Fact]
        public void Modal_MatchingResultType_IsOk()
        {
            var result = ShowModalClosingInInit<int>(p => p.Close(5, InteractionStatus.Ok));

            Assert.True(result.IsOk);
            Assert.Equal(5, result.Value);
        }

        [Fact]
        public void Modal_MismatchedResultType_IsErrorNotOkDefault()
        {
            var result = ShowModalClosingInInit<int>(p => p.Close("five", InteractionStatus.Ok));

            Assert.True(result.IsError);
            Assert.Contains("String", result.ErrorMessage);
            Assert.Contains("Int32", result.ErrorMessage);
        }

        [Fact]
        public void Modal_OkWithoutResult_ForValueTypeResult_IsError()
        {
            var result = ShowModalClosingInInit<int>(p => p.CloseNoResult(InteractionStatus.Ok));

            Assert.True(result.IsError);
        }

        [Fact]
        public void Modal_OkWithoutResult_ForReferenceTypeResult_IsOkNull()
        {
            var result = ShowModalClosingInInit<string>(p => p.CloseNoResult(InteractionStatus.Ok));

            Assert.True(result.IsOk);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Modal_ErrorWithStringResult_UsesItAsErrorMessage()
        {
            var result = ShowModalClosingInInit<int>(p => p.Close("disk full", InteractionStatus.Error));

            Assert.True(result.IsError);
            Assert.Equal("disk full", result.ErrorMessage);
        }

        [Fact]
        public void Modal_ErrorWithException_CarriesException()
        {
            var boom = new InvalidOperationException("boom");
            var result = ShowModalClosingInInit<int>(p => p.Close(boom, InteractionStatus.Error));

            Assert.True(result.IsError);
            Assert.Same(boom, result.Exception);
            Assert.Equal("boom", result.ErrorMessage);
        }

        #endregion

        #region Navigator: singleton-per-key

        [Fact]
        public void ShowWindow_SameKey_ActivatesExistingAndDisposesNewPresenter()
        {
            var navigator = NewNavigator();
            var first = new ScriptedPresenter();
            var second = new ScriptedPresenter();

            var view1 = navigator.ShowWindow(first, keySelector: _ => "doc-1");
            var view2 = navigator.ShowWindow(second, keySelector: _ => "doc-1");

            try
            {
                Assert.Same(view1, view2);
                Assert.True(second.Disposed);
                Assert.Equal(0, second.InitCount);
            }
            finally { ((Form)view1).Close(); }
        }

        [Fact]
        public void ShowWindow_AfterKeyedWindowCloses_OpensNewWindow()
        {
            var navigator = NewNavigator();
            var view1 = navigator.ShowWindow(new ScriptedPresenter(), keySelector: _ => "doc-1");
            ((Form)view1).Close();

            var view2 = navigator.ShowWindow(new ScriptedPresenter(), keySelector: _ => "doc-1");
            try { Assert.NotSame(view1, view2); }
            finally { ((Form)view2).Close(); }
        }

        [Fact]
        public void ShowWindow_ReentrantShowForSameKeyDuringInitialize_ActivatesInsteadOfOpeningTwin()
        {
            var navigator = NewNavigator();
            IWindowView reentrantView = null;
            var inner = new ScriptedPresenter();
            var outer = new ScriptedPresenter
            {
                // e.g. OnInitialize pumped a modal prompt and the user re-triggered the same command
                OnInit = _ => reentrantView = navigator.ShowWindow(inner, keySelector: __ => "doc-1")
            };

            var view = navigator.ShowWindow(outer, keySelector: _ => "doc-1");
            try
            {
                Assert.Same(view, reentrantView);
                Assert.True(inner.Disposed);
            }
            finally { ((Form)view).Close(); }
        }

        [Fact]
        public void ShowWindow_SameKeyInAnotherPresenterType_OpensItsOwnWindow()
        {
            var navigator = NewNavigator();
            var other = new OtherPresenter();
            int editClosed = 0, otherClosed = 0;

            var edit = navigator.ShowWindow<ScriptedPresenter, object>(new ScriptedPresenter(), keySelector: _ => 42, onClosed: _ => editClosed++);
            var second = navigator.ShowWindow<OtherPresenter, object>(other, keySelector: _ => 42, onClosed: _ => otherClosed++);
            try
            {
                Assert.NotSame(edit, second);
                Assert.False(other.Disposed);
                Assert.Equal(1, other.InitCount);
            }
            finally
            {
                ((Form)edit).Close();
                ((Form)second).Close();
            }
            Assert.Equal(1, editClosed);
            Assert.Equal(1, otherClosed);
        }

        [Fact]
        public void ShowWindow_SameRuntimeType_ActivatesExisting_WhateverTheStaticType()
        {
            var navigator = NewNavigator();
            var view = navigator.ShowWindow(new ScriptedPresenter(), keySelector: _ => 42);
            var viaBase = new ScriptedPresenter();
            var viaInterface = new ScriptedPresenter();
            var viaFluent = new ScriptedPresenter();
            var withParam = new ParamPresenter();
            try
            {
                Assert.Same(view, navigator.ShowWindow<WindowPresenterBase<IHardView>>(viaBase, keySelector: _ => 42));
                Assert.Same(view, navigator.ShowWindow<IPresenter>(viaInterface, keySelector: _ => 42));
                Assert.Same(view, navigator.For(viaFluent).ShowWindow(keySelector: _ => 42));
                Assert.True(viaBase.Disposed);
                Assert.True(viaInterface.Disposed);
                Assert.True(viaFluent.Disposed);

                var paramView = navigator.ShowWindow(withParam, 7, keySelector: _ => 42);   // another type: its own window
                try { Assert.NotSame(view, paramView); }
                finally { ((Form)paramView).Close(); }
            }
            finally { ((Form)view).Close(); }
        }

        [Fact]
        public void ShowWindow_ClosingOneScreensWindow_KeepsTheOtherScreensKey()
        {
            var navigator = NewNavigator();
            var edit = navigator.ShowWindow(new ScriptedPresenter(), keySelector: _ => 42);
            var other = navigator.ShowWindow(new OtherPresenter(), keySelector: _ => 42);
            ((Form)other).Close();

            var again = new ScriptedPresenter();
            try
            {
                Assert.Same(edit, navigator.ShowWindow(again, keySelector: _ => 42));
                Assert.True(again.Disposed);
            }
            finally { ((Form)edit).Close(); }
        }

        [Fact]
        public void ShowWindow_InitializeThrows_ReleasesKey()
        {
            var navigator = NewNavigator();
            var failing = new ScriptedPresenter { OnInit = _ => throw new InvalidOperationException("boom") };

            Assert.Throws<InvalidOperationException>(
                () => navigator.ShowWindow(failing, keySelector: _ => "doc-1"));
            Assert.True(failing.Disposed);

            var next = new ScriptedPresenter();
            var view = navigator.ShowWindow(next, keySelector: _ => "doc-1");
            try { Assert.Equal(1, next.InitCount); }
            finally { ((Form)view).Close(); }
        }

        #endregion

        #region Presenter base

        [Fact]
        public void Initialize_WithoutView_ThrowsInvalidOperation()
        {
            var presenter = new ScriptedPresenter();

            var ex = Assert.Throws<InvalidOperationException>(() => ((IInitializable)presenter).Initialize());
            Assert.Contains("no view attached", ex.Message);
        }

        [Fact]
        public void AttachView_Twice_Throws()
        {
            var presenter = new ScriptedPresenter();
            ((IViewAttacher<IHardView>)presenter).AttachView(new NonFormView());

            Assert.Throws<InvalidOperationException>(() => ((IViewAttacher<IHardView>)presenter).AttachView(new NonFormView()));
        }

        [Fact]
        public void Dispose_CleanupThrows_StillSweepsDisposables()
        {
            var presenter = new ScriptedPresenter { OnCleanup = () => throw new InvalidOperationException("cleanup") };
            bool released = false;
            presenter.Bag.Add(Disposable.Create(() => released = true));

            Assert.Throws<InvalidOperationException>(() => presenter.Dispose());

            Assert.True(released);
        }

        [Fact]
        public void Dispose_ReentrantFromCleanup_RunsCleanupOnce()
        {
            ScriptedPresenter presenter = null;
            presenter = new ScriptedPresenter { OnCleanup = () => presenter.DisposeFromInside() };

            presenter.Dispose();

            Assert.Equal(1, presenter.CleanupCount);
        }

        [Fact]
        public void RequestClose_BeforeSinkBound_IsDeliveredOnBind()
        {
            var presenter = new ScriptedPresenter { OnInit = p => p.Close("early", InteractionStatus.Ok) };
            // Drives the framework contract directly (no recording sink), as the hosts do.
            ((IViewAttacher<IHardView>)presenter).AttachView(new NonFormView());
            ((IInitializable)presenter).Initialize();

            var sink = new RecordingSink();
            ((ICloseParticipant)presenter).BindCloseSink(sink);

            Assert.Single(sink.Closes);
            Assert.Equal("early", sink.Closes[0].Result);
        }

        #endregion

        #region Adopted Connect

        [Fact]
        public void Connect_AttachedButNotInitialized_Initializes()
        {
            var presenter = new ScriptedPresenter();
            var form = new HardForm();
            ((IViewAttacher<IHardView>)presenter).AttachView(form);

            presenter.Connect(form);

            Assert.Equal(1, presenter.InitCount);
            form.Dispose();
        }

        [Fact]
        public void Connect_ToDifferentViewThanAttached_Throws()
        {
            var presenter = new ScriptedPresenter();
            ((IViewAttacher<IHardView>)presenter).AttachView(new HardForm());

            using (var other = new HardForm())
                Assert.Throws<InvalidOperationException>(() => presenter.Connect(other));
        }

        [Fact]
        public void Connect_AfterManualInitializeThatRequestedClose_DeliversCloseOnShow()
        {
            var presenter = new ScriptedPresenter { OnInit = p => p.Close("done", InteractionStatus.Ok) };
            var form = new HardForm();
            // Drives the framework contract directly (no recording sink), as the hosts do.
            ((IViewAttacher<IHardView>)presenter).AttachView(form);
            ((IInitializable)presenter).Initialize();

            InteractionResult<string> closed = null;
            presenter.Connect<IHardView, string>(form, r => closed = r);
            form.Show();

            // Form.Shown is posted (BeginInvoke), so pump until the deferred close has run.
            var sw = Stopwatch.StartNew();
            while (closed == null && sw.Elapsed < TimeSpan.FromSeconds(5))
                Application.DoEvents();

            Assert.NotNull(closed);
            Assert.True(closed.IsOk);
            Assert.Equal("done", closed.Value);
        }

        [Fact]
        public void Connect_Twice_ThrowsWithoutDisposingTheConnectedPresenter()
        {
            var presenter = new ScriptedPresenter();
            using (var form = new HardForm())
            {
                presenter.Connect(form);

                Assert.Throws<InvalidOperationException>(() => presenter.Connect(form));
                Assert.False(presenter.Disposed);
            }
        }

        [Fact]
        public void Connect_InitializeThrows_DisposesPresenter()
        {
            var presenter = new ScriptedPresenter { OnInit = _ => throw new InvalidOperationException("boom") };
            using (var form = new HardForm())
            {
                Assert.Throws<InvalidOperationException>(() => presenter.Connect(form));
                Assert.True(presenter.Disposed);
            }
        }

        public interface IHardControlView : IViewBase { }

        private sealed class HardControl : UserControl, IHardControlView { }

        private sealed class ThrowingControlPresenter : ControlPresenterBase<IHardControlView>
        {
            public bool Disposed;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => throw new InvalidOperationException("boom");
            protected override void Cleanup() => Disposed = true;
        }

        [Fact]
        public void ControlConnect_InitializeThrows_DisposesPresenter()
        {
            var presenter = new ThrowingControlPresenter();
            using (var control = new HardControl())
            {
                Assert.Throws<InvalidOperationException>(() => presenter.Connect(control));
                Assert.True(presenter.Disposed);
            }
        }

        #endregion

        #region WindowLifecycleController: async veto

        private sealed class GateParticipant : ICloseParticipant, IDisposable
        {
            public readonly List<Action<bool>> Pending = new List<Action<bool>>();
            public ICloseSink Sink;
            public bool ThrowOnDispose;
            public void BindCloseSink(ICloseSink sink) => Sink = sink;
            public void CanCloseGate(CloseReason reason, Action<bool> proceed) => Pending.Add(proceed);
            public void Dispose()
            {
                if (ThrowOnDispose) throw new InvalidOperationException("dispose");
            }
        }

        private static (HardForm form, GateParticipant gate, List<InteractionStatus> closed) BuildController()
        {
            var form = new HardForm();
            var gate = new GateParticipant();
            var closed = new List<InteractionStatus>();
            var controller = new WindowLifecycleController(form, gate, (r, s) => closed.Add(s), disposeForm: true);
            controller.BindSink();
            controller.WireFormEvents();
            return (form, gate, closed);
        }

        [Fact]
        public void AsyncGate_SecondCloseWhilePending_DoesNotAskAgain()
        {
            var (form, gate, closed) = BuildController();
            form.Show();

            form.Close();
            form.Close();   // e.g. the user clicks X again while the save prompt is still deciding

            Assert.Single(gate.Pending);
            Assert.True(form.Visible);

            gate.Pending[0](true);
            Assert.False(form.Visible);
            Assert.Single(closed);
        }

        [Fact]
        public void AsyncGate_PendingDoesNotBlockSystemShutdown()
        {
            var (form, gate, closed) = BuildController();
            form.Show();
            form.Close();                       // async gate pending
            Assert.Single(gate.Pending);

            var e = new FormClosingEventArgs(System.Windows.Forms.CloseReason.WindowsShutDown, cancel: false);
            typeof(Form).GetMethod("OnFormClosing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(form, new object[] { e });

            Assert.False(e.Cancel);
            Assert.Single(gate.Pending);        // not asked again
            form.Dispose();
        }

        [Fact]
        public void AsyncGate_AllowFromBackgroundThread_ClosesOnUiThread()
        {
            var (form, gate, closed) = BuildController();
            form.Show();
            form.Close();
            Assert.Single(gate.Pending);

            Task.Run(() => gate.Pending[0](true)).Wait();

            // The allow was marshalled with BeginInvoke; pump until it runs.
            var sw = Stopwatch.StartNew();
            while (form.Visible && sw.Elapsed < TimeSpan.FromSeconds(5))
                Application.DoEvents();

            Assert.False(form.Visible);
            Assert.Single(closed);
        }

        [Fact]
        public void Converge_PresenterDisposeThrows_StillDisposesForm()
        {
            // Close-before-show path: converges without a window message, so the exception
            // surfaces here instead of in WinForms' unhandled-exception dialog.
            var form = new HardForm();
            var gate = new GateParticipant { ThrowOnDispose = true };
            var controller = new WindowLifecycleController(form, gate, (r, s) => { }, disposeForm: true);
            controller.BindSink();
            gate.Sink.Close(null, InteractionStatus.Ok);
            Assert.True(controller.CloseRequestedBeforeShow);

            Assert.Throws<InvalidOperationException>(() => controller.ConvergeWithoutShow());

            Assert.True(form.IsDisposed);
        }

        #endregion
    }
}
