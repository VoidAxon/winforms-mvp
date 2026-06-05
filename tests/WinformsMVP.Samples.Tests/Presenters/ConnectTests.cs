using System;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

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

            public event EventHandler<WindowClosingEventArgs> Closing;
            void IWindowView.OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
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
    }
}
