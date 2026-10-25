using System;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Services.Implementations;
using Xunit;
using CloseReason = Kibinago.Common.CloseReason;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Presenters
{
    public class ConnectTests
    {
        public interface IFakeView : IWindowView { }

        private sealed class FakeView : IFakeView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class FakePresenter : WindowPresenterBase<IFakeView>
        {
            protected override void OnViewAttached() { }
        }

        [Fact]
        public void IsViewAttached_FalseBeforeAttach_TrueAfter()
        {
            var p = new FakePresenter();
            Assert.False(((IViewAttachable)p).IsViewAttached);

            ((IViewAttacher<IFakeView>)p).AttachView(new FakeView());

            Assert.True(((IViewAttachable)p).IsViewAttached);
        }

        private sealed class FakeWindowForm : Form, IFakeView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class ResultPresenter : WindowPresenterBase<IFakeView>
        {
            public bool Initialized;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => Initialized = true;
            public void PushDone(string r) => RequestClose(r, InteractionStatus.Ok);
        }

        [Fact]
        public void Connect_AttachesAndInitializes_WhenNotYetAttached()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();

            presenter.Connect<IFakeView, string>(form, _ => { });

            Assert.True(((IViewAttachable)presenter).IsViewAttached);
            Assert.True(presenter.Initialized);
            form.Dispose();
        }

        [Fact]
        public void Connect_IsIdempotent_DoesNotReinitialize()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();
            // Drives the framework contract directly (no recording sink), as the hosts do.
            ((IViewAttacher<IFakeView>)presenter).AttachView(form);
            ((IInitializable)presenter).Initialize();
            presenter.Initialized = false;

            presenter.Connect<IFakeView, string>(form, _ => { });

            Assert.False(presenter.Initialized);
            form.Dispose();
        }

        [Fact]
        public void Connect_Push_DeliversResultToOnClosed()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();
            InteractionResult<string> captured = null;
            presenter.Connect<IFakeView, string>(form, r => captured = r);

            form.Show();
            presenter.PushDone("hi");

            Assert.NotNull(captured);
            Assert.True(captured.IsOk);
            Assert.Equal("hi", captured.Value);
        }

        [Fact]
        public void Connect_NonFormView_Throws()
        {
            var presenter = new ResultPresenter();
            Assert.Throws<ArgumentException>(
                () => presenter.Connect<IFakeView, string>(new FakeView(), _ => { }));
        }

        // ---- cancelling the opening (RequestClose during initialization) ----

        private sealed class RefusingPresenter : WindowPresenterBase<IFakeView>
        {
            public int GateCalls;
            public bool Disposed;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => RequestClose(InteractionStatus.Cancel);
            protected override bool CanClose(CloseReason reason) { GateCalls++; return false; }
            protected override void Cleanup() => Disposed = true;
        }

        [Fact]
        public void Connect_CancelledDuringInitialization_ClosesInLoad_HiddenAndWithoutTheGate()
        {
            var form = new FakeWindowForm();
            var presenter = new RefusingPresenter();
            InteractionResult closed = null;
            bool visibleWhenClosing = true;
            form.FormClosing += (s, e) => visibleWhenClosing = form.Opacity > 0;

            presenter.Connect(form, r => closed = r);
            Assert.Null(closed);                      // nothing happens before the caller shows it

            form.Show();                              // the caller's usual code: Load cancels the opening

            Assert.NotNull(closed);
            Assert.True(closed.IsCancelled);
            Assert.False(visibleWhenClosing);         // made invisible before closing
            Assert.Equal(0, presenter.GateCalls);
            Assert.True(presenter.Disposed);
            Assert.True(form.IsDisposed);
        }

        [Fact]
        public void Connect_ToAFormAlreadyShown_AHeldRequestIsANormalGatedClose()
        {
            var form = new FakeWindowForm();
            var presenter = new RefusingPresenter();
            form.Show();

            presenter.Connect(form);

            Assert.Equal(1, presenter.GateCalls);     // the gate refused
            Assert.True(form.Visible);
            form.Dispose();
        }

        private sealed class ClosingPresenter : WindowPresenterBase<IFakeView>
        {
            private static readonly ViewAction Close = ViewAction.Create("ConnectTests.Close");
            public int GateCalls;
            protected override void OnViewAttached() { }
            protected override void RegisterViewActions() => Dispatcher.Register(Close, () => RequestClose(InteractionStatus.Cancel));
            protected override bool CanClose(CloseReason reason) { GateCalls++; return true; }
            internal static ViewAction CloseAction => Close;
        }

        [Fact]
        public void Connect_ToAFormShownThenHidden_IsStillOpening_ClosedWhenShownAgain_NoGate()
        {
            var form = new FakeWindowForm();
            form.Show();
            form.Hide();                               // Load already ran; it will not run again
            var presenter = new ClosingPresenter();
            InteractionResult closed = null;
            presenter.Connect(form, r => closed = r);

            ((CapturingActionBinder)form.ActionBinder).Dispatch(ClosingPresenter.CloseAction);
            Assert.Null(closed);                       // not visible: cancelling the opening, nothing shown

            form.Show();                               // becomes visible: closed at once, hidden

            Assert.Equal(0, presenter.GateCalls);
            Assert.NotNull(closed);
        }

        [Fact]
        public void Connect_HandleCreatedButNeverShown_IsStillOpening()
        {
            var form = new FakeWindowForm();
            var _ = form.Handle;                       // created before Connect, never shown
            var presenter = new RefusingPresenter();
            InteractionResult closed = null;

            presenter.Connect(form, r => closed = r);
            form.Show();

            Assert.Equal(0, presenter.GateCalls);
            Assert.True(closed.IsCancelled);
        }

        private sealed class RefusingWithResultPresenter : WindowPresenterBase<IFakeView>
        {
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => RequestClose("gone", InteractionStatus.Error);
        }

        [Fact]
        public void Connect_CancelledDuringInitialization_Modal_ClosesWithTheResult_NotShown()
        {
            var form = new FakeWindowForm();
            InteractionResult<string> closed = null;
            new RefusingWithResultPresenter().Connect<IFakeView, string>(form, r => closed = r);

            form.ShowDialog();                         // returns once the cancelled opening closed it

            Assert.NotNull(closed);
            Assert.True(closed.IsError);
            Assert.Equal("gone", closed.ErrorMessage);
            Assert.True(form.IsDisposed);
        }

        [Fact]
        public void Connect_CancelledDuringInitialization_AnotherHandlerRefuses_TheFormIsShown()
        {
            var form = new FakeWindowForm();
            form.FormClosing += (s, e) => e.Cancel = true;   // e.g. a legacy handler that refuses to close
            var presenter = new RefusingPresenter();
            presenter.Connect(form);

            form.Show();

            Assert.False(form.IsDisposed);
            Assert.Equal(1.0, form.Opacity);                  // made visible again
            form.Dispose();
        }
    }
}
