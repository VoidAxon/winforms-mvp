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

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class ConnectTests
    {
        public interface IFakeView : IWindowView { }

        private sealed class FakeView : IFakeView
        {
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
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

            p.AttachView(new FakeView());

            Assert.True(((IViewAttachable)p).IsViewAttached);
        }

        private sealed class FakeWindowForm : Form, IFakeView
        {
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            bool IWindowView.IsDisposed => base.IsDisposed;
            void IWindowView.Activate() => base.Activate();
        }

        private sealed class ResultPresenter : WindowPresenterBase<IFakeView>, IRequestClose<string>
        {
            public bool Initialized;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => Initialized = true;
            public void PushDone(string r) => this.RequestClose(r, InteractionStatus.Ok);
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
            presenter.AttachView(form);
            presenter.Initialize();
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
    }
}
