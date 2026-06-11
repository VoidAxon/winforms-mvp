using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Views
{
    [Collection("ServiceLocator")]
    public class AnchoredMessageViewExtensionsTests : IDisposable
    {
        private sealed class StubView : IViewBase { }

        private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
        private readonly StubView _view = new StubView();

        public AnchoredMessageViewExtensionsTests()
            => ServiceLocator.Configure(reg => reg.RegisterInstance<IAnchoredMessageService>(_anchored));

        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void ShowToast_ResolvesServiceFromLocator_AndForwards()
        {
            _view.ShowToast("saved", ToastType.Success);
            Assert.Single(_anchored.Toasts);
            Assert.Equal("saved", _anchored.Toasts[0].Text);
            Assert.Equal(ToastType.Success, _anchored.Toasts[0].Type);
            Assert.Null(_anchored.Toasts[0].Options);
        }

        [Fact]
        public void ShowToast_WithOptions_ForwardsOptions()
        {
            var options = new ToastOptions();
            _view.ShowToast("hi", ToastType.Info, options);
            Assert.Same(options, _anchored.Toasts[0].Options);
        }

        [Fact]
        public void ShowInfo_ForwardsThroughConvenienceMapping()
        {
            _view.ShowInfo("note", "cap");
            Assert.Equal(MessageButtons.Ok, _anchored.Messages[0].Buttons);
            Assert.Equal(MessageIcon.Information, _anchored.Messages[0].Icon);
        }

        [Fact]
        public void ConfirmYesNo_ReturnsMappedResult()
        {
            _anchored.NextResult = ConfirmResult.Yes;
            Assert.True(_view.ConfirmYesNo("sure?"));
            _anchored.NextResult = ConfirmResult.No;
            Assert.False(_view.ConfirmYesNo("sure?"));
        }
    }
}
