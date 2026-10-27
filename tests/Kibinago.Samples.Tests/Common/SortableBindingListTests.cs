using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Kibinago.Common;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    public class SortableBindingListTests
    {
        public sealed class Row : NotifyingObject
        {
            private string _ward;
            private int _no;
            private object _value;

            public Row() { }
            public Row(int id, string ward, int no, object value = null) { Id = id; _ward = ward; _no = no; _value = value; }

            public int Id { get; set; }
            public string Ward { get => _ward; set => SetProperty(ref _ward, value); }
            public int No { get => _no; set => SetProperty(ref _no, value); }
            public object Value { get => _value; set => SetProperty(ref _value, value); }
            public string Note { get; set; }
            public void RaiseEverythingChanged() => OnPropertyChanged("");
        }

        private static PropertyDescriptor Prop(string name) => TypeDescriptor.GetProperties(typeof(Row))[name];

        private static int[] Ids(IEnumerable<Row> rows) => rows.Select(r => r.Id).ToArray();

        private static SortableBindingList<Row> Wards() => new SortableBindingList<Row>(new[]
        {
            new Row(1, "B", 2), new Row(2, "A", 9), new Row(3, "B", 1), new Row(4, "A", 3), new Row(5, "B", 2),
        });

        [Fact]
        public void HeaderClick_SortsBySingleColumn_AndReportsTheSort()
        {
            var list = Wards();
            IBindingList bindable = list;

            bindable.ApplySort(Prop(nameof(Row.No)), ListSortDirection.Ascending);

            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
            Assert.True(bindable.SupportsSorting);
            Assert.True(bindable.IsSorted);
            Assert.Equal(nameof(Row.No), bindable.SortProperty.Name);
            Assert.Equal(ListSortDirection.Ascending, bindable.SortDirection);
        }

        [Fact]
        public void Sort_IsStable_InBothDirections()
        {
            var list = Wards();
            IBindingList bindable = list;

            bindable.ApplySort(Prop(nameof(Row.Ward)), ListSortDirection.Ascending);
            Assert.Equal(new[] { 2, 4, 1, 3, 5 }, Ids(list));

            bindable.ApplySort(Prop(nameof(Row.Ward)), ListSortDirection.Descending);
            Assert.Equal(new[] { 1, 3, 5, 2, 4 }, Ids(list));   // equal wards keep their order, not reversed
        }

        [Fact]
        public void ApplySort_SortsBySeveralColumns()
        {
            var list = Wards();

            list.ApplySort(s => s.By(r => r.Ward).ThenByDescending(r => r.No));

            Assert.Equal(new[] { 2, 4, 1, 5, 3 }, Ids(list));
            var sorts = list.SortDescriptions;
            Assert.Equal(2, sorts.Count);
            Assert.Equal(nameof(Row.Ward), sorts[0].PropertyDescriptor.Name);
            Assert.Equal(ListSortDirection.Descending, sorts[1].SortDirection);
        }

        [Fact]
        public void ApplySort_RejectsPathsFieldsAndNoColumns_WithoutChangingTheList()
        {
            var list = new SortableBindingList<PathRow>(new[] { new PathRow(), new PathRow() });

            Assert.Throws<ArgumentException>(() => list.ApplySort(s => s.By(r => r.Child.Name)));
            Assert.Throws<ArgumentException>(() => list.ApplySort(s => s.By(r => r.Field)));
            Assert.Throws<ArgumentException>(() => list.ApplySort(s => s.By(r => r.Name.Length)));
            Assert.Throws<ArgumentException>(() => list.ApplySort(s => { }));
            Assert.Empty(list.SortDescriptions);
        }

        public sealed class PathRow
        {
            public int Field;
            public string Name { get; set; } = "";
            public PathRow Child { get; set; }
        }

        [Fact]
        public void ApplySortDescriptions_SortsByNamesKnownAtRunTime()
        {
            var list = Wards();
            var saved = new[] { ("Ward", ListSortDirection.Ascending), ("No", ListSortDirection.Descending) };   // e.g. from user settings

            list.ApplySort(new ListSortDescriptionCollection(
                saved.Select(c => new ListSortDescription(Prop(c.Item1), c.Item2)).ToArray()));

            Assert.Equal(new[] { 2, 4, 1, 5, 3 }, Ids(list));
        }

        [Fact]
        public void ApplySortEmpty_RemovesTheSort()
        {
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));

            list.ApplySort(new ListSortDescriptionCollection());

            Assert.False(((IBindingList)list).IsSorted);
            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));   // the order stays
        }

        [Fact]
        public void Constructor_CopiesTheList_TheCallersListIsNotReordered()
        {
            var source = new[] { new Row(1, "B", 1), new Row(2, "A", 2) };   // an array: other lists wrap and fail here

            var list = new SortableBindingList<Row>(source);
            list.ApplySort(s => s.By(r => r.Ward));

            Assert.Equal(new[] { 2, 1 }, Ids(list));
            Assert.Equal(new[] { 1, 2 }, Ids(source));
        }

        [Fact]
        public void NullAndDbNull_SortFirstAscending_LastDescending()
        {
            var list = new SortableBindingList<Row>(new[]
            {
                new Row(1, "B", 0), new Row(2, null, 0), new Row(3, "A", 0),
            });
            list[0].Value = 5; list[1].Value = DBNull.Value; list[2].Value = 1;

            list.ApplySort(s => s.By(r => r.Ward));
            Assert.Equal(new[] { 2, 3, 1 }, Ids(list));

            list.ApplySort(s => s.ByDescending(r => r.Value));
            Assert.Equal(new[] { 1, 3, 2 }, Ids(list));
        }

        [Fact]
        public void MixedTypes_AreComparedByText_ConsistentlyAndWithoutThrowing()
        {
            var list = new SortableBindingList<Row>(new[]
            {
                new Row(1, "", 0, 2), new Row(2, "", 0, "15"), new Row(3, "", 0, 10), new Row(4, "", 0, new object()),
            });

            list.ApplySort(s => s.By(r => r.Value));

            Assert.Equal(new[] { 3, 2, 1, 4 }, Ids(list));   // "10" < "15" < "2" < "System.Object"
        }

        [Fact]
        public void OneComparableType_UsesItsOwnComparison()
        {
            var list = new SortableBindingList<Row>(new[]
            {
                new Row(1, "", 0, 10), new Row(2, "", 0, 2), new Row(3, "", 0, 100),
            });

            list.ApplySort(s => s.By(r => r.Value));

            Assert.Equal(new[] { 2, 1, 3 }, Ids(list));   // numeric, not "10" < "100" < "2"
        }

        [Fact]
        public void SetComparer_IsUsedForThatColumn()
        {
            var list = Wards();
            list.SetComparer(r => r.Ward, Comparer<string>.Create((a, b) => string.CompareOrdinal(b, a)));

            list.ApplySort(s => s.By(r => r.Ward));

            Assert.Equal(new[] { 1, 3, 5, 2, 4 }, Ids(list));
        }

        [Fact]
        public void SetComparer_Null_GoesBackToTheDefault()
        {
            var list = Wards();
            list.SetComparer(r => r.Ward, Comparer<string>.Create((a, b) => string.CompareOrdinal(b, a)));
            list.SetComparer(r => r.Ward, null);

            list.ApplySort(s => s.By(r => r.Ward));

            Assert.Equal(new[] { 2, 4, 1, 3, 5 }, Ids(list));
        }

        [Fact]
        public void ThrowingComparer_LeavesOrderAndSortState()
        {
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.SetComparer(r => r.Ward, new ThrowingComparer());

            Assert.ThrowsAny<Exception>(() => list.ApplySort(s => s.By(r => r.Ward)));

            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
        }

        private sealed class ThrowingComparer : IComparer<string>
        {
            public int Compare(string x, string y) => throw new InvalidOperationException("boom");
        }

        [Fact]
        public void Sort_RaisesOneReset()
        {
            var list = Wards();
            var changes = new List<ListChangedType>();
            list.ListChanged += (s, e) => changes.Add(e.ListChangedType);

            list.ApplySort(s => s.By(r => r.No));

            Assert.Equal(new[] { ListChangedType.Reset }, changes);
        }

        [Fact]
        public void Sort_WithEventsOff_RaisesNothing()
        {
            var list = Wards();
            var raised = 0;
            list.ListChanged += (s, e) => raised++;
            list.RaiseListChangedEvents = false;

            list.ApplySort(s => s.By(r => r.No));

            Assert.Equal(0, raised);
            Assert.False(list.RaiseListChangedEvents);
            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
        }

        [Fact]
        public void AddOrEditingASortColumn_KeepsTheSort_WithoutMovingRows()
        {
            var list = Wards();
            IBindingList bindable = list;
            list.ApplySort(s => s.By(r => r.No));

            list[0].No = 100;   // notified change of a sort column: the row stays (not under the cursor's feet)
            Assert.True(bindable.IsSorted);   // the sort the user chose (the grid's glyph) stays
            Assert.Equal(nameof(Row.No), bindable.SortProperty.Name);
            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));

            list.Resort();
            Assert.Equal(new[] { 1, 5, 4, 2, 3 }, Ids(list));

            list.Add(new Row(6, "C", 0));
            Assert.True(bindable.IsSorted);
            Assert.Equal(6, list.Last().Id);
        }

        [Fact]
        public void EverythingChangedNotification_KeepsTheSort()
        {
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));

            list[0].RaiseEverythingChanged();   // BindingList turns it into a Reset

            Assert.True(((IBindingList)list).IsSorted);
            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_AnEditedRow_MovesToItsPlace_WithOneItemMoved()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));   // 3 1 5 4 2 (No 1 2 2 3 9)
            list.KeepSorted = true;
            var seen = new List<string>();
            list.ListChanged += (s, e) => seen.Add($"{e.ListChangedType} {e.NewIndex}");

            list[0].No = 5;   // row 3: between 4 (No 3) and 2 (No 9)

            Assert.Equal(new[] { 1, 5, 4, 3, 2 }, Ids(list));
            Assert.Equal(new[] { "ItemChanged 0", "ItemMoved 3" }, seen);
            Assert.True(((IBindingList)list).IsSorted);
        }

        [Fact]
        public void KeepSorted_EditOfAnotherColumn_MovesNothing()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.KeepSorted = true;

            list[0].Ward = "Z";

            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_AnAddedRow_GoesToItsPlace()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.KeepSorted = true;

            list.Add(new Row(6, "C", 0));

            Assert.Equal(new[] { 6, 3, 1, 5, 4, 2 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_ANewRow_IsPlacedWhenCommitted_NotWhileBeingAdded()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.KeepSorted = true;

            var added = list.AddNew();   // blank (No 0), pending at the end
            Assert.Same(added, list.Last());
            added.Id = 6;
            added.No = 4;                // the grid fills it in: still pending, not moved
            Assert.Same(added, list.Last());

            list.EndNew(list.IndexOf(added));

            Assert.Equal(new[] { 3, 1, 5, 4, 6, 2 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_IsPostedToTheSynchronizationContext()
        {
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.KeepSorted = true;
            var context = new QueueingContext();
            var previous = System.Threading.SynchronizationContext.Current;
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                list[0].No = 5;   // a grid committing a cell edit is still on the stack here
                Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));

                context.RunAll();

                Assert.Equal(new[] { 1, 5, 4, 3, 2 }, Ids(list));
            }
            finally
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        [Fact]
        public void KeepSorted_SettingItOnASortedList_SortsNow()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list[0].No = 100;   // out of order (KeepSorted off)

            list.KeepSorted = true;

            Assert.Equal(new[] { 1, 5, 4, 2, 3 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_EverythingChanged_SortsAgain()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            list.KeepSorted = true;
            list.RaiseListChangedEvents = false;
            list[0].No = 100;
            list[1].No = 0;
            list.RaiseListChangedEvents = true;

            list[2].RaiseEverythingChanged();

            Assert.Equal(new[] { 1, 5, 4, 2, 3 }, Ids(list));
        }

        [Fact]
        public void KeepSorted_WithoutASort_DoesNothing()
        {
            using var _ = WithoutSynchronizationContext();   // placed at once (a UI thread posts it)
            var list = Wards();
            list.KeepSorted = true;

            list[0].No = 100;

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Ids(list));
        }

        private static IDisposable WithoutSynchronizationContext()
        {
            var previous = System.Threading.SynchronizationContext.Current;
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            return new Restore(() => System.Threading.SynchronizationContext.SetSynchronizationContext(previous));
        }

        private sealed class Restore : IDisposable
        {
            private readonly Action _restore;
            public Restore(Action restore) => _restore = restore;
            public void Dispose() => _restore();
        }

        private sealed class QueueingContext : System.Threading.SynchronizationContext
        {
            private readonly Queue<(System.Threading.SendOrPostCallback, object)> _posted = new Queue<(System.Threading.SendOrPostCallback, object)>();
            public override void Post(System.Threading.SendOrPostCallback d, object state) => _posted.Enqueue((d, state));
            public void RunAll() { while (_posted.Count > 0) { var (d, state) = _posted.Dequeue(); d(state); } }
        }

        [Fact]
        public void Descending_WithAComparerReturningIntMinValue_IsReversed()
        {
            var list = Wards();
            list.SetComparer(r => r.No, Comparer<int>.Create((a, b) => a == b ? 0 : a < b ? int.MinValue : int.MaxValue));

            list.ApplySort(s => s.ByDescending(r => r.No));

            Assert.Equal(new[] { 2, 4, 1, 5, 3 }, Ids(list));
        }

        [Fact]
        public void SetComparer_OfAnotherType_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => Wards().SetComparer<long>(r => r.No, Comparer<long>.Default));
        }

        [Fact]
        public void EditingAnotherColumn_OrRemoving_KeepsTheSortedState()
        {
            var list = Wards();
            IBindingList bindable = list;
            list.ApplySort(s => s.By(r => r.No));

            list[0].Ward = "Z";
            list.RemoveAt(0);

            Assert.True(bindable.IsSorted);
        }

        [Fact]
        public void RemoveSort_KeepsTheOrder_AndForgetsTheSort()
        {
            var list = Wards();
            list.ApplySort(s => s.By(r => r.No));
            var changes = new List<ListChangedType>();
            list.ListChanged += (s, e) => changes.Add(e.ListChangedType);

            list.RemoveSort();
            list.Resort();   // nothing to apply

            Assert.False(((IBindingList)list).IsSorted);
            Assert.Equal(new[] { 3, 1, 5, 4, 2 }, Ids(list));
            Assert.Equal(new[] { ListChangedType.Reset }, changes);
        }

        [Fact]
        public void ReplaceAll_KeepsTheLastSort_AndRaisesOneReset()
        {
            var list = Wards();
            list.ApplySort(s => s.ByDescending(r => r.No));
            list.Add(new Row(9, "X", 0));   // ends the sorted state; the sort is still remembered
            var changes = new List<ListChangedType>();
            list.ListChanged += (s, e) => changes.Add(e.ListChangedType);

            list.ReplaceAll(new[] { new Row(7, "A", 1), new Row(8, "A", 5) });

            Assert.Equal(new[] { 8, 7 }, Ids(list));
            Assert.True(((IBindingList)list).IsSorted);
            Assert.Equal(new[] { ListChangedType.Reset }, changes);
        }

        [Fact]
        public void ReplaceAll_WithItself_Works()
        {
            var list = Wards();

            list.ReplaceAll(list);

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Ids(list));
        }

        [Fact]
        public void ReplaceAll_NewRowsStillNotify()
        {
            var list = Wards();
            var row = new Row(7, "A", 1);
            list.ReplaceAll(new[] { row });
            var changed = new List<ListChangedType>();
            list.ListChanged += (s, e) => changed.Add(e.ListChangedType);

            row.Ward = "Q";

            Assert.Equal(new[] { ListChangedType.ItemChanged }, changed);
        }

        [Fact]
        public void PendingAddNew_IsCommittedBeforeASort_SoCancelNewDoesNotRemoveAnotherRow()
        {
            var list = Wards();
            var added = list.AddNew();
            added.Id = 6;
            added.No = 0;
            var pendingIndex = list.IndexOf(added);

            list.ApplySort(s => s.By(r => r.No));
            list.CancelNew(pendingIndex);

            Assert.Equal(6, list.Count);
            Assert.Contains(added, list);
        }

        [Fact]
        public void Filter_IsNotSupported()
        {
            IBindingListView view = Wards();

            Assert.False(view.SupportsFiltering);
            Assert.True(view.SupportsAdvancedSorting);
            Assert.Null(view.Filter);
            Assert.Throws<NotSupportedException>(() => view.Filter = "Ward = 'A'");
        }
    }
}
