using System;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// A view event that carries the value before and after a change, so a presenter can react to (or
    /// undo) it without a hand-written EventArgs class or a WinForms one.
    /// </summary>
    public class ValueChangedEventArgsTests
    {
        public interface IQuantityView : IWindowView
        {
            int Quantity { set; }
            event EventHandler<ValueChangedEventArgs<int>> QuantityChanged;
        }

        private sealed class StubView : IQuantityView
        {
            public int Shown = -1;
            public IViewActionBinder ActionBinder => null;
            public int Quantity { set => Shown = value; }
            public event EventHandler<ValueChangedEventArgs<int>> QuantityChanged;
            public void Change(int from, int to) => QuantityChanged?.Invoke(this, new ValueChangedEventArgs<int>(from, to));
        }

        // Puts the old value back when the new one exceeds the stock.
        private sealed class QuantityPresenter : WindowPresenterBase<IQuantityView>
        {
            protected override void OnViewAttached()
                => SubscribeViewEvent<ValueChangedEventArgs<int>>(
                    h => View.QuantityChanged += h, h => View.QuantityChanged -= h, OnQuantityChanged);

            protected override void OnInitialize() { }

            private void OnQuantityChanged(ValueChangedEventArgs<int> e)
            {
                if (e.NewValue > 10) View.Quantity = e.OldValue;
            }
        }

        [Fact]
        public void CarriesTheOldAndTheNewValue()
        {
            var e = new ValueChangedEventArgs<string>("before", "after");
            Assert.Equal("before", e.OldValue);
            Assert.Equal("after", e.NewValue);
            Assert.IsAssignableFrom<EventArgs>(e);
        }

        [Fact]
        public void APresenterCanPutTheOldValueBack()
        {
            var view = new StubView();
            using (PresenterTestHost.Start(TestServices.Create(), new QuantityPresenter(), view))
            {
                view.Change(3, 5);
                Assert.Equal(-1, view.Shown);   // accepted: nothing written back

                view.Change(5, 12);
                Assert.Equal(5, view.Shown);    // over the stock: the old value is restored
            }
        }
    }
}
