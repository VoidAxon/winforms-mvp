using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Kibinago.Common;
using Xunit;
using Row = Kibinago.Samples.Tests.Common.SortableBindingListTests.Row;

namespace Kibinago.Samples.Tests.Common
{
    public class ListMoveExtensionsTests
    {
        private static SortableBindingList<Row> Rows() => new SortableBindingList<Row>(new[]
        {
            new Row(1, "A", 1), new Row(2, "A", 2), new Row(3, "B", 3), new Row(4, "B", 4),
        });

        private static int[] Ids(IEnumerable<Row> rows) => rows.Select(r => r.Id).ToArray();

        private static List<string> Notifications(IBindingList list)
        {
            var seen = new List<string>();
            list.ListChanged += (s, e) => seen.Add($"{e.ListChangedType} {e.OldIndex}->{e.NewIndex}");
            return seen;
        }

        [Fact]
        public void TheFourMoves_MoveOneRow()
        {
            var rows = Rows();

            Assert.True(rows.MoveUp(rows[2]));
            Assert.Equal(new[] { 1, 3, 2, 4 }, Ids(rows));
            Assert.True(rows.MoveDown(rows[0]));
            Assert.Equal(new[] { 3, 1, 2, 4 }, Ids(rows));
            Assert.True(rows.MoveToBottom(rows[0]));
            Assert.Equal(new[] { 1, 2, 4, 3 }, Ids(rows));
            Assert.True(rows.MoveToTop(rows[2]));
            Assert.Equal(new[] { 4, 1, 2, 3 }, Ids(rows));
        }

        [Fact]
        public void AtTheEdge_OrNotInTheList_NothingMoves_AndNothingIsRaised()
        {
            var rows = Rows();
            var seen = Notifications(rows);

            Assert.False(rows.MoveUp(rows[0]));
            Assert.False(rows.MoveToTop(rows[0]));
            Assert.False(rows.MoveDown(rows[3]));
            Assert.False(rows.MoveToBottom(rows[3]));
            Assert.False(rows.MoveUp(new Row(9, "Z", 9)));

            Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(rows));
            Assert.Empty(seen);
        }

        [Fact]
        public void SortableBindingList_RaisesOneItemMoved()
        {
            var rows = Rows();
            var seen = Notifications(rows);

            rows.MoveToTop(rows[3]);

            Assert.Equal(new[] { "ItemMoved 3->0" }, seen);
        }

        [Fact]
        public void SortableBindingList_Move_EndsTheSortedState()
        {
            var rows = Rows();
            rows.ApplySort(s => s.ByDescending(r => r.No));

            rows.MoveUp(rows[1]);

            Assert.False(((IBindingList)rows).IsSorted);
            Assert.Equal(new[] { 3, 4, 2, 1 }, Ids(rows));
            rows.Resort();
            Assert.Equal(new[] { 4, 3, 2, 1 }, Ids(rows));
        }

        [Fact]
        public void SortableBindingList_MovedRow_StillNotifiesItsChanges()
        {
            var rows = Rows();
            var moved = rows[0];
            rows.MoveToBottom(moved);
            var seen = new List<(ListChangedType, int)>();
            rows.ListChanged += (s, e) => seen.Add((e.ListChangedType, e.NewIndex));

            moved.No = 99;

            Assert.Equal(new[] { (ListChangedType.ItemChanged, 3) }, seen);
        }

        [Fact]
        public void SortableBindingList_Move_WorksWhenRemoveIsNotAllowed()
        {
            var rows = Rows();
            rows.AllowRemove = false;

            Assert.True(rows.MoveDown(rows[0]));
            Assert.Equal(new[] { 2, 1, 3, 4 }, Ids(rows));
        }

        [Fact]
        public void SortableBindingList_PendingNewRow_IsCommittedBeforeTheMove()
        {
            var rows = Rows();
            var added = rows.AddNew();   // pending at index 4
            added.Id = 5;

            rows.MoveToTop(added);
            ((ICancelAddNew)rows).CancelNew(0);
            ((ICancelAddNew)rows).CancelNew(4);

            Assert.Equal(new[] { 5, 1, 2, 3, 4 }, Ids(rows));
        }

        [Fact]
        public void SortableBindingList_EventsOff_RaisesNothing()
        {
            var rows = Rows();
            var seen = Notifications(rows);
            rows.RaiseListChangedEvents = false;

            rows.Move(0, 3);

            Assert.Equal(new[] { 2, 3, 4, 1 }, Ids(rows));
            Assert.Empty(seen);
        }

        [Fact]
        public void BindingList_MovesWithRemoveAndInsert()
        {
            var rows = new BindingList<Row>(Rows().ToList());
            var seen = Notifications(rows);

            Assert.True(rows.MoveDown(rows[0]));

            Assert.Equal(new[] { 2, 1, 3, 4 }, Ids(rows));
            Assert.Equal(new[] { "ItemDeleted -1->0", "ItemAdded -1->1" }, seen);
        }

        [Fact]
        public void BindingList_PendingNewRow_IsCommittedByTheMove()
        {
            var rows = new BindingList<Row>(Rows().ToList()) { AllowNew = true };
            var added = rows.AddNew();
            added.Id = 5;

            rows.MoveToTop(added);
            ((ICancelAddNew)rows).CancelNew(0);
            ((ICancelAddNew)rows).CancelNew(4);

            Assert.Equal(new[] { 5, 1, 2, 3, 4 }, Ids(rows));
        }

        [Fact]
        public void ObservableCollection_UsesItsOwnMove()
        {
            var rows = new ObservableCollection<Row>(Rows());
            var seen = new List<NotifyCollectionChangedAction>();
            rows.CollectionChanged += (s, e) => seen.Add(e.Action);

            Assert.True(((IList<Row>)rows).MoveToBottom(rows[0]));

            Assert.Equal(new[] { 2, 3, 4, 1 }, Ids(rows));
            Assert.Equal(new[] { NotifyCollectionChangedAction.Move }, seen);
        }

        [Fact]
        public void PlainList_Moves()
        {
            var rows = Rows().ToList();

            Assert.True(rows.MoveToTop(rows[2]));

            Assert.Equal(new[] { 3, 1, 2, 4 }, Ids(rows));
        }

        [Fact]
        public void Move_RejectsIndexesOutsideTheList()
        {
            IList<Row> rows = Rows();

            Assert.Throws<ArgumentOutOfRangeException>(() => rows.Move(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rows.Move(0, 4));
            Assert.Throws<ArgumentNullException>(() => ((IList<Row>)null).MoveUp(new Row()));
        }
    }
}
