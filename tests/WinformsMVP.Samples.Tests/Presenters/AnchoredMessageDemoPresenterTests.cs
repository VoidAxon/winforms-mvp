using WinformsMVP.Common;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.AnchoredMessageDemo;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Demonstrates testing the feedback division of labor: corner toasts are asserted through
    /// the injected MockServices' message service; position-meaningful feedback goes through
    /// semantic view methods, asserted directly on the mock view. No global state involved.
    /// </summary>
    public class AnchoredMessageDemoPresenterTests
    {
        private sealed class StubView : IAnchoredMessageDemoView
        {
            public string LastHint;
            public bool ConfirmDeleteResult;
            public int ConfirmDeleteCalls;
            public int ShowRowTouchedCalls;

            public IViewActionBinder ActionBinder => null; // explicit: no auto-binding in tests
            public string ItemName { get; set; } = "Alpha";
            public bool NotificationsEnabled { get; set; }
            public void ShowHint(string message) => LastHint = message;
            public bool ConfirmDelete() { ConfirmDeleteCalls++; return ConfirmDeleteResult; }
            public void ShowRowTouched() => ShowRowTouchedCalls++;
        }

        private readonly MockServices _services = new MockServices();
        private readonly StubView _view = new StubView();
        private readonly AnchoredMessageDemoPresenter _presenter;

        public AnchoredMessageDemoPresenterTests()
        {
            _presenter = new AnchoredMessageDemoPresenter().WithServiceProvider(_services.Provider);
            _presenter.AttachView(_view);
            _presenter.Initialize();
        }

        [Fact]
        public void Save_ShowsCornerToast_WithItemName()
        {
            _presenter.Dispatch(AnchoredMessageDemoActions.Save);
            Assert.Contains(_services.MessageService.Calls,
                c => c.Type == MessageType.Toast && c.Message.Contains("Saved 'Alpha'"));
        }

        [Fact]
        public void Delete_Confirmed_ShowsDeletedToast()
        {
            _view.ConfirmDeleteResult = true;
            _presenter.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Equal(1, _view.ConfirmDeleteCalls);
            Assert.Contains(_services.MessageService.Calls,
                c => c.Type == MessageType.Toast && c.Message.Contains("Deleted"));
        }

        [Fact]
        public void Delete_Declined_ShowsNoToast()
        {
            _view.ConfirmDeleteResult = false;
            _presenter.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.DoesNotContain(_services.MessageService.Calls,
                c => c.Type == MessageType.Toast && c.Message.Contains("Deleted"));
            Assert.Equal("Delete cancelled.", _view.LastHint);
        }

        [Fact]
        public void GridTouch_UsesSemanticViewMethod()
        {
            _presenter.Dispatch(AnchoredMessageDemoActions.GridTouch);
            Assert.Equal(1, _view.ShowRowTouchedCalls);
        }

        [Fact]
        public void ToggleNotify_ReflectsCheckedState()
        {
            _view.NotificationsEnabled = true;
            _presenter.Dispatch(AnchoredMessageDemoActions.ToggleNotify);
            Assert.Contains(_services.MessageService.Calls,
                c => c.Type == MessageType.Toast && c.Message.Contains("enabled"));
        }
    }
}
