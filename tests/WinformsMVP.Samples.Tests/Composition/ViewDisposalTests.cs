using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using Xunit;

namespace WinformsMVP.Samples.Tests.Composition
{
    public class ViewDisposalTests
    {
        private sealed class PlainView : IViewBase { }

        [Fact]
        public void NonControlView_IsNeverDisposed_AndObservingIsHarmless()
        {
            var view = new PlainView();
            bool called = false;

            using (ViewDisposal.Observer.ObserveDisposal(view, () => called = true)) { }

            Assert.False(ViewDisposal.Observer.IsDisposed(view));
            Assert.False(called);
        }
    }
}
