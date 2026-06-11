using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.AnchoredMessageDemo;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Demonstrates the testing pattern for cursor-anchored feedback: the View.ShowToast
    /// extensions resolve IAnchoredMessageService from the global ServiceLocator, so the test
    /// installs a recording mock via ServiceLocator.Configure (serialized by the
    /// "ServiceLocator" collection) and resets it on Dispose.
    /// </summary>
    [Collection("ServiceLocator")]
    public class AnchoredMessageDemoPresenterTests : IDisposable
    {
        private sealed class StubView : IAnchoredMessageDemoView
        {
            public string LastHint;
            public IViewActionBinder ActionBinder => null; // explicit: no auto-binding in tests
            public void ShowHint(string message) => LastHint = message;
        }

        private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
        private readonly StubView _view = new StubView();
        private readonly AnchoredMessageDemoPresenter _presenter = new AnchoredMessageDemoPresenter();

        public AnchoredMessageDemoPresenterTests()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IAnchoredMessageService>(_anchored));
            _presenter.AttachView(_view);
            _presenter.Initialize();
        }

        public void Dispose()
        {
            _presenter.Dispose();
            ServiceLocator.Reset();
        }

        [Fact]
        public void Save_ShowsSuccessToast()
        {
            _presenter.Dispatch(AnchoredMessageDemoActions.Save);
            Assert.Single(_anchored.Toasts);
            Assert.Equal("Saved!", _anchored.Toasts[0].Text);
            Assert.Equal(ToastType.Success, _anchored.Toasts[0].Type);
        }

        [Fact]
        public void Delete_Confirmed_ShowsWarningToast()
        {
            _anchored.NextResult = ConfirmResult.Yes;
            _presenter.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Equal(MessageButtons.YesNo, _anchored.Messages[0].Buttons);
            Assert.Single(_anchored.Toasts);
            Assert.Equal(ToastType.Warning, _anchored.Toasts[0].Type);
        }

        [Fact]
        public void Delete_Declined_ShowsNoToast()
        {
            _anchored.NextResult = ConfirmResult.No;
            _presenter.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Empty(_anchored.Toasts);
            Assert.Equal("Delete cancelled.", _view.LastHint);
        }
    }
}
