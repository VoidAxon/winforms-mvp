using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Common;
using Kibinago.Samples.ListDemo;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.Samples
{
    /// <summary>
    /// A presenter test: a mock view (no Form), PresenterTestHost runs the real initialization, and the
    /// actions are dispatched through the view's binder as a bound button would.
    /// </summary>
    public class MoveDemoPresenterTests
    {
        private sealed class MockMoveView : IMoveDemoView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public SortableBindingList<PhraseRow> Rows;
            public int? CurrentId { get; set; }
            public List<string> Logs { get; } = new List<string>();

            public void SetRows(SortableBindingList<PhraseRow> rows) => Rows = rows;
            public void SelectRow(int id) => CurrentId = id;
            public void Log(string text) => Logs.Add(text);
            public event EventHandler CurrentRowChanged;

            public void Select(int id)
            {
                CurrentId = id;
                CurrentRowChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Fact]
        public void MoveUp_SwapsTwoRows_AndSavesOnlyThoseTwo()
        {
            var view = new MockMoveView();
            using (PresenterTestHost.Start(TestServices.Create(), new MoveDemoPresenter(), view))
            {
                view.Select(3);
                view.Binder.Dispatch(MoveDemoActions.MoveUp);

                Assert.Equal(new[] { 1, 3, 2, 4, 5, 6 }, view.Rows.Select(r => r.Id));
                Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, view.Rows.Select(r => r.DisplayOrder));
                Assert.Contains("2 行", Assert.Single(view.Logs));
                Assert.Equal(3, view.CurrentId);   // the moved row stays current
            }
        }

        [Fact]
        public void TheFirstRow_CannotMoveUp_ButCanMoveDown()
        {
            var view = new MockMoveView();
            using (PresenterTestHost.Start(TestServices.Create(), new MoveDemoPresenter(), view))
            {
                view.Select(1);

                Assert.False(view.Binder.CanDispatch(MoveDemoActions.MoveUp));
                Assert.False(view.Binder.CanDispatch(MoveDemoActions.MoveToTop));
                Assert.True(view.Binder.CanDispatch(MoveDemoActions.MoveDown));
            }
        }

        [Fact]
        public void MoveToBottom_RenumbersEveryRowBelow()
        {
            var view = new MockMoveView();
            using (PresenterTestHost.Start(TestServices.Create(), new MoveDemoPresenter(), view))
            {
                view.Select(2);
                view.Binder.Dispatch(MoveDemoActions.MoveToBottom);

                Assert.Equal(new[] { 1, 3, 4, 5, 6, 2 }, view.Rows.Select(r => r.Id));
                Assert.Contains("5 行", view.Logs.Single());   // rows 3..6 moved up, row 2 to the end
                Assert.False(view.Binder.CanDispatch(MoveDemoActions.MoveDown));
            }
        }
    }
}
