using System;
using Kibinago.Events;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>
    /// View events that carry the value before and after a change — after it (ValueChanged) or before
    /// it, cancelable (ValueChanging) — without a hand-written EventArgs class or a WinForms one.
    /// </summary>
    public class ValueChangedEventArgsTests
    {
        public interface IRecordListView : IWindowView
        {
            /// <summary>The selected record is about to change; Cancel keeps the current one.</summary>
            event EventHandler<ValueChangingEventArgs<int>> SelectedRecordChanging;
        }

        private sealed class ListStubView : IRecordListView
        {
            public int Selected = 1;
            public IViewActionBinder ActionBinder => null;
            public event EventHandler<ValueChangingEventArgs<int>> SelectedRecordChanging;

            // What the Form does before letting the grid move to another row.
            public void TrySelect(int id)
            {
                var e = new ValueChangingEventArgs<int>(Selected, id);
                SelectedRecordChanging?.Invoke(this, e);
                if (!e.Cancel) Selected = id;
            }
        }

        // Keeps the current record while it has unsaved edits.
        private sealed class RecordListPresenter : WindowPresenterBase<IRecordListView>
        {
            public bool Dirty;
            protected override void OnViewAttached()
                => SubscribeViewEvent<ValueChangingEventArgs<int>>(
                    h => View.SelectedRecordChanging += h, h => View.SelectedRecordChanging -= h,
                    e => { if (Dirty && e.OldValue != e.NewValue) e.Cancel = true; });
            protected override void OnInitialize() { }
        }

        [Fact]
        public void Changing_CarriesBothValues_AndIsCancelable()
        {
            var e = new ValueChangingEventArgs<string>("before", "after");
            Assert.Equal("before", e.OldValue);
            Assert.Equal("after", e.NewValue);
            Assert.False(e.Cancel);
            Assert.IsAssignableFrom<System.ComponentModel.CancelEventArgs>(e);
        }

        [Fact]
        public void APresenterCanCancelTheChangeBeforeItHappens()
        {
            var view = new ListStubView();
            var presenter = new RecordListPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                view.TrySelect(2);
                Assert.Equal(2, view.Selected);   // nothing pending: the selection moves

                presenter.Dirty = true;
                view.TrySelect(3);
                Assert.Equal(2, view.Selected);   // unsaved edits: the presenter keeps the current record
            }
        }

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
