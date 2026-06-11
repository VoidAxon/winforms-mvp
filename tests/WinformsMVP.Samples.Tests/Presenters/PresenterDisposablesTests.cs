using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class PresenterDisposablesTests
    {
        private sealed class TestPresenter : PresenterBase<IViewBase>
        {
            public readonly List<string> Log = new List<string>();

            protected override void OnViewAttached() { }
            protected override void Cleanup() { Log.Add("cleanup"); }

            public void AddToBag(IDisposable d) { Disposables.Add(d); }
            public CompositeDisposable Bag => Disposables;
        }

        [Fact]
        public void Dispose_SweepsBag_AfterCleanup()
        {
            var p = new TestPresenter();
            p.AddToBag(Disposable.Create(() => p.Log.Add("bag")));

            p.Dispose();

            Assert.Equal(new[] { "cleanup", "bag" }, p.Log);
        }

        [Fact]
        public void Dispose_Twice_SweepsOnce()
        {
            var p = new TestPresenter();
            int swept = 0;
            p.AddToBag(Disposable.Create(() => swept++));

            p.Dispose();
            p.Dispose();

            Assert.Equal(1, swept);
        }

        [Fact]
        public void Disposables_IsLazy_SameInstanceAcrossAccesses()
        {
            var p = new TestPresenter();
            Assert.Same(p.Bag, p.Bag);
        }

        [Fact]
        public void Dispose_WithUntouchedBag_DoesNotThrow()
        {
            var p = new TestPresenter();
            p.Dispose();   // _disposables stays null; must not throw
            Assert.Equal(new[] { "cleanup" }, p.Log);
        }

        [Fact]
        public void AddToBag_AfterDispose_WithNeverTouchedBag_DisposesImmediately()
        {
            var p = new TestPresenter();
            p.Dispose();   // bag never created before disposal

            int swept = 0;
            p.AddToBag(Disposable.Create(() => swept++));

            Assert.Equal(1, swept);   // disposed immediately, not leaked
        }
    }
}
