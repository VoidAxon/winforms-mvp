using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// SubscribeViewEvent: one line per view event, unsubscribed when the presenter is disposed, and
    /// silent once disposal has begun (WinForms can raise events while controls are torn down after
    /// the presenter's Cleanup).
    /// </summary>
    public class SubscribeViewEventTests
    {
        public sealed class SearchArgs : EventArgs
        {
            public SearchArgs(string text) { Text = text; }
            public string Text { get; }
        }

        public interface IListView : IWindowView
        {
            event EventHandler SelectionChanged;
            event EventHandler<SearchArgs> SearchRequested;
        }

        private sealed class ListView : IListView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public event EventHandler SelectionChanged;
            public event EventHandler<SearchArgs> SearchRequested;

            public void Select() => SelectionChanged?.Invoke(this, EventArgs.Empty);
            public void Search(string text) => SearchRequested?.Invoke(this, new SearchArgs(text));
            public bool HasSelectionSubscribers => SelectionChanged != null;
            public bool HasSearchSubscribers => SearchRequested != null;

            /// <summary>The handlers as a control would hold them at the moment it raises.</summary>
            public EventHandler CapturedSelection => SelectionChanged;
        }

        private sealed class ListPresenter : WindowPresenterBase<IListView>
        {
            public int Selections;
            public string LastSearch;
            public IDisposable SelectionToken;
            public Action DuringCleanup;

            protected override void OnViewAttached()
            {
                SelectionToken = SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h,
                                                    () => Selections++);
                SubscribeViewEvent<SearchArgs>(h => View.SearchRequested += h, h => View.SearchRequested -= h,
                                               e => LastSearch = e.Text);
            }

            // A test double's own forwarding to the protected helper (what a presenter's code would call).
            public IDisposable Subscribe(Action<EventHandler> add, Action<EventHandler> remove, Action handler)
                => SubscribeViewEvent(add, remove, handler);

            public IDisposable Subscribe<T>(Action<EventHandler<T>> add, Action<EventHandler<T>> remove, Action<T> handler)
                => SubscribeViewEvent(add, remove, handler);

            protected override void Cleanup() => DuringCleanup?.Invoke();
        }

        private static (ListPresenter presenter, ListView view, PresenterTestHost host) Start()
        {
            var view = new ListView();
            var presenter = new ListPresenter();
            return (presenter, view, PresenterTestHost.Start(TestServicesFor(), presenter, view));
        }

        private static IServiceProvider TestServicesFor() => TestServices.Create();

        [Fact]
        public void DeliversTheEvent()
        {
            var (presenter, view, _) = Start();
            view.Select();
            view.Select();
            Assert.Equal(2, presenter.Selections);
        }

        [Fact]
        public void TypedOverload_PassesTheEventArgs()
        {
            var (presenter, view, _) = Start();
            view.Search("佐藤");
            Assert.Equal("佐藤", presenter.LastSearch);
        }

        [Fact]
        public void PresenterDisposal_Unsubscribes()
        {
            var (_, view, host) = Start();
            host.Dispose();
            Assert.False(view.HasSelectionSubscribers);
            Assert.False(view.HasSearchSubscribers);
        }

        [Fact]
        public void Token_UnsubscribesEarly_AndIsIdempotent()
        {
            var (presenter, view, host) = Start();
            presenter.SelectionToken.Dispose();
            presenter.SelectionToken.Dispose();

            view.Select();

            Assert.Equal(0, presenter.Selections);
            Assert.False(view.HasSelectionSubscribers);
            Assert.True(view.HasSearchSubscribers);   // only that subscription
            host.Dispose();                           // and disposing the presenter afterwards is fine
        }

        [Fact]
        public void EventRaisedDuringCleanup_IsIgnored()
        {
            var (presenter, view, host) = Start();
            presenter.DuringCleanup = view.Select;   // e.g. a control raising while torn down

            host.Dispose();

            Assert.Equal(0, presenter.Selections);
        }

        [Fact]
        public void EventFromAnInvocationListCapturedBeforeDisposal_IsIgnored()
        {
            var (presenter, view, host) = Start();
            var captured = view.CapturedSelection;
            host.Dispose();

            captured(view, EventArgs.Empty);

            Assert.Equal(0, presenter.Selections);
        }

        [Fact]
        public void HandlerException_Propagates()
        {
            var (presenter, view, _) = Start();
            presenter.Subscribe<SearchArgs>(h => view.SearchRequested += h, h => view.SearchRequested -= h,
                                            e => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => view.Search("x"));
        }

        [Fact]
        public void NullArguments_AreRejected_WithoutAttaching()
        {
            var (presenter, view, _) = Start();
            var before = presenter.Selections;
            Assert.Throws<ArgumentNullException>(() => presenter.Subscribe(null, h => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => presenter.Subscribe(h => view.SelectionChanged += h, null, () => presenter.Selections += 100));
            Assert.Throws<ArgumentNullException>(() => presenter.Subscribe(h => view.SelectionChanged += h, h => { }, null));
            view.Select();
            Assert.Equal(before + 1, presenter.Selections);   // only the presenter's own subscription ran
        }

        [Fact]
        public void SubscribingAfterDisposal_Throws_WithoutAttaching()
        {
            var (presenter, view, host) = Start();
            host.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                presenter.Subscribe(h => view.SelectionChanged += h, h => view.SelectionChanged -= h, () => { }));
            Assert.False(view.HasSelectionSubscribers);
        }
    }
}
