using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services.Implementations;
using Xunit;
using CloseReason = WinformsMVP.Common.CloseReason;
using WinformsMVP.Testing;

namespace WinformsMVP.Samples.Tests.Presenters
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
    }
}
