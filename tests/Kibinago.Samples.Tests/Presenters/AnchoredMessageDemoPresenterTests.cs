using Kibinago.Common;
using Kibinago.ViewActions;
using Kibinago.Samples.AnchoredMessageDemo;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Samples.Tests.TestHelpers;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// Demonstrates testing the feedback division of labor: corner toasts are asserted through
    /// the injected FakeServices' toast service; position-meaningful feedback goes through
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

            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public string ItemName { get; set; } = "Alpha";
            public bool NotificationsEnabled { get; set; }
            public void ShowHint(string message) => LastHint = message;
            public bool ConfirmDelete() { ConfirmDeleteCalls++; return ConfirmDeleteResult; }
            public void ShowRowTouched() => ShowRowTouchedCalls++;
        }

        private readonly FakeServices _services = TestServices.CreateFakes();
        private readonly StubView _view = new StubView();
        private readonly AnchoredMessageDemoPresenter _presenter;

        public AnchoredMessageDemoPresenterTests()
        {
            _presenter = new AnchoredMessageDemoPresenter();
            PresenterTestHost.Start(_services.Provider, _presenter, _view);
        }

        [Fact]
        public void Save_ShowsCornerToast_WithItemName()
        {
            _view.Binder.Dispatch(AnchoredMessageDemoActions.Save);
            Assert.Contains(_services.Toasts.Calls,
                c => c.Text.Contains("Saved 'Alpha'"));
        }

        [Fact]
        public void Delete_Confirmed_ShowsDeletedToast()
        {
            _view.ConfirmDeleteResult = true;
            _view.Binder.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.Equal(1, _view.ConfirmDeleteCalls);
            Assert.Contains(_services.Toasts.Calls,
                c => c.Text.Contains("Deleted"));
        }

        [Fact]
        public void Delete_Declined_ShowsNoToast()
        {
            _view.ConfirmDeleteResult = false;
            _view.Binder.Dispatch(AnchoredMessageDemoActions.Delete);
            Assert.DoesNotContain(_services.Toasts.Calls,
                c => c.Text.Contains("Deleted"));
            Assert.Equal("Delete cancelled.", _view.LastHint);
        }

        [Fact]
        public void GridTouch_UsesSemanticViewMethod()
        {
            _view.Binder.Dispatch(AnchoredMessageDemoActions.GridTouch);
            Assert.Equal(1, _view.ShowRowTouchedCalls);
        }

        [Fact]
        public void ToggleNotify_ReflectsCheckedState()
        {
            _view.NotificationsEnabled = true;
            _view.Binder.Dispatch(AnchoredMessageDemoActions.ToggleNotify);
            Assert.Contains(_services.Toasts.Calls,
                c => c.Text.Contains("enabled"));
        }
    }
}
