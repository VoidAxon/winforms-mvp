using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Samples.CompositionDemo;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.Samples
{
    public class ContactsDemoPresenterTests
    {
        private sealed class MockRowView : IContactRowView
        {
            public IViewActionBinder ActionBinder => null;
            public string ContactName { get; set; }
            public string Relation { get; set; }
            public string Phone { get; set; }
            public string PhoneError;
            public bool Removed;
            public void ShowPhoneError(string message) => PhoneError = message;
            public event EventHandler Edited;
            public void Edit(string phone) { Phone = phone; Edited?.Invoke(this, EventArgs.Empty); }
            public bool HasSubscribers => Edited != null;
        }

        private sealed class MockContactsView : IContactsDemoView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public List<MockRowView> Rows { get; } = new List<MockRowView>();
            public string Status;

            public IContactRowView AddRow() { var row = new MockRowView(); Rows.Add(row); return row; }
            public void RemoveRow(IContactRowView row) { ((MockRowView)row).Removed = true; Rows.Remove((MockRowView)row); }
            public void ShowStatus(string text) => Status = text;
            public event EventHandler<IContactRowView> RowRemoveRequested;
            public void RequestRemove(MockRowView row) => RowRemoveRequested?.Invoke(this, row);
        }

        [Fact]
        public void EachRow_GetsItsOwnPresenter_AndRemovingARowDisposesItFirst()
        {
            var view = new MockContactsView();
            using (PresenterTestHost.Start(TestServices.Create(), new ContactsDemoPresenter(), view))
            {
                var first = Assert.Single(view.Rows);
                Assert.Equal("佐藤 花子", first.ContactName);   // the row presenter showed its model

                view.Binder.Dispatch(ContactsDemoActions.AddRow);
                var second = view.Rows[1];
                second.Edit("abc");
                Assert.NotNull(second.PhoneError);            // the row's own rule

                view.RequestRemove(first);
                Assert.True(first.Removed);
                Assert.False(first.HasSubscribers);           // its presenter was disposed (unsubscribed)
                Assert.True(second.HasSubscribers);
                Assert.Contains("1 件", view.Status);
            }
        }

        [Fact]
        public void DisposingTheParent_DisposesEveryRowPresenter()
        {
            var view = new MockContactsView();
            var host = PresenterTestHost.Start(TestServices.Create(), new ContactsDemoPresenter(), view);
            view.Binder.Dispatch(ContactsDemoActions.AddRow);

            host.Dispose();

            Assert.All(view.Rows, r => Assert.False(r.HasSubscribers));
        }
    }
}
