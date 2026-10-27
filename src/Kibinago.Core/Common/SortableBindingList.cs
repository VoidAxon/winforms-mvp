using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Kibinago.Common
{
    /// <summary>
    /// A <see cref="BindingList{T}"/> that sorts: a grid bound to it (directly or through a
    /// <c>BindingSource</c>) sorts by a column header click, and
    /// <c>rows.ApplySort(s => s.By(r => r.Ward).ThenByDescending(r => r.No))</c> sorts by several columns. <see cref="BindingList{T}"/>
    /// itself cannot sort, so a grid bound to a list of objects otherwise ignores header clicks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sort is stable: rows with equal keys keep their relative order, in both directions.
    /// Each sort column is compared one way for all its values: a comparer given to
    /// <see cref="SetComparer{TKey}"/>; strings with <see cref="StringComparer"/> (the culture current when
    /// the list was created, by default); values of one type with its own comparison
    /// (<see cref="IComparable"/> / <see cref="IComparable{T}"/>); anything else (mixed or
    /// non-comparable types) by its text, with <see cref="StringComparer"/>. <c>null</c> and
    /// <see cref="DBNull"/> sort first ascending, last descending. Values are read once per sort; when
    /// reading or comparing throws, the list and its sort state are left unchanged.
    /// </para>
    /// <para>
    /// The constructor copies the collection (not the objects): the caller's list is never reordered.
    /// A sort, <see cref="RemoveSort"/> and <see cref="ReplaceAll"/> raise one
    /// <see cref="ListChangedType.Reset"/>, which may move the grid's current row — keep the selection
    /// by record key in the view. A row being added (<see cref="BindingList{T}.AddNew"/>) is committed
    /// before a sort.
    /// </para>
    /// <para>
    /// The sort is the one the user chose (<see cref="IBindingList.IsSorted"/>, <see cref="SortDescriptions"/>,
    /// the grid's sort glyph); it stays until <see cref="RemoveSort"/> or another sort. By default rows
    /// are not re-sorted while they are edited (that would move the row under the cursor, as in Excel):
    /// an edited, added or moved row stays where it is, and <see cref="Resort"/> puts the rows in order
    /// again; <see cref="ReplaceAll"/> (a reload) sorts the new rows. With <see cref="KeepSorted"/> the
    /// list keeps itself in order instead: a row whose sort-column value changes, or a row added, moves
    /// to its place once the change is complete. <see cref="RemoveSort"/> keeps the current order (the
    /// original order is not restored). Filtering is not supported. Not thread-safe: change it, and the
    /// objects it holds, on the UI thread.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The row type; its public properties are the sortable columns.</typeparam>
    public class SortableBindingList<T> : BindingList<T>, IBindingListView
    {
        private static readonly SortKey[] NoKeys = new SortKey[0];

        private readonly List<T> _items;
        private readonly Dictionary<string, IComparer> _comparers = new Dictionary<string, IComparer>(StringComparer.Ordinal);
        private StringComparer _stringComparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: false);
        private SortKey[] _sort;         // the sort the user chose (null = not sorted)
        private int _pendingNew = -1;    // index of the row AddNew created and not yet committed
        private bool _ownReset;          // the Reset being raised is this list's own (after a sort / reload)
        private bool _keepSorted;
        private readonly List<T> _toPlace = new List<T>();   // KeepSorted: rows changed since the last placing
        private bool _placingPosted;
        private bool _addingNew;         // inside AddNew: its ItemAdded is not a row to place yet
        private int _changing;           // inside an insert / remove / replace / clear: indexes must not move yet

        /// <summary>Creates an empty list.</summary>
        public SortableBindingList()
            : base(new List<T>())
        {
            _items = (List<T>)Items;
        }

        /// <summary>Creates a list holding a copy of <paramref name="items"/> (the objects are not copied).</summary>
        /// <param name="items">The initial rows.</param>
        public SortableBindingList(IEnumerable<T> items)
            : base(new List<T>(items ?? throw new ArgumentNullException(nameof(items))))
        {
            _items = (List<T>)Items;
        }

        /// <summary>
        /// The comparison for string values and for values compared by their text. Default: the culture
        /// current when the list was created, case-sensitive. Rows already sorted are not moved: call <see cref="Resort"/>.
        /// </summary>
        public StringComparer StringComparer
        {
            get => _stringComparer;
            set => _stringComparer = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Keeps the rows in sort order: a row whose sort-column value changes (or a row added) moves to its
        /// place with one <see cref="ListChangedType.ItemMoved"/> (a bound grid's current row moves with it),
        /// after the change is complete — posted to the <see cref="SynchronizationContext"/> current at the
        /// change (the UI thread), so a grid committing a cell edit is not interrupted; without one, at once.
        /// Default <c>false</c>: edited rows stay where they are until <see cref="Resort"/> (the edited row
        /// does not jump away from the user). Setting it to <c>true</c> on a sorted list sorts it now.
        /// Changes made while <see cref="BindingList{T}.RaiseListChangedEvents"/> is <c>false</c>, or to
        /// properties that do not notify, are not seen — call <see cref="Resort"/> after them.
        /// </summary>
        public bool KeepSorted
        {
            get => _keepSorted;
            set
            {
                if (_keepSorted == value) return;
                _keepSorted = value;
                if (value) Resort();
            }
        }

        /// <summary>
        /// The sort the user chose, first column first; empty when it is not sorted. The rows are in this
        /// order after a sort; edits can leave them out of it (see <see cref="KeepSorted"/>, <see cref="Resort"/>).
        /// </summary>
        public ListSortDescriptionCollection SortDescriptions =>
            new ListSortDescriptionCollection((_sort ?? NoKeys)
                .Select(k => new ListSortDescription(k.Property, k.Direction)).ToArray());

        /// <inheritdoc />
        protected override bool SupportsSortingCore => true;

        /// <inheritdoc />
        protected override bool IsSortedCore => _sort != null;

        /// <inheritdoc />
        protected override PropertyDescriptor SortPropertyCore => _sort?[0].Property;

        /// <inheritdoc />
        protected override ListSortDirection SortDirectionCore => _sort?[0].Direction ?? ListSortDirection.Ascending;

        bool IBindingListView.SupportsAdvancedSorting => true;

        bool IBindingListView.SupportsFiltering => false;

        string IBindingListView.Filter
        {
            get => null;
            set => throw new NotSupportedException("SortableBindingList does not filter.");
        }

        void IBindingListView.RemoveFilter() { }

        /// <summary>
        /// Uses <paramref name="comparer"/> for the values of one column (for example, Japanese names by
        /// their reading): <c>rows.SetComparer(r => r.Name, comparer)</c>; <c>null</c> goes back to the
        /// default comparison. Rows already sorted are not moved: call <see cref="Resort"/>.
        /// </summary>
        /// <typeparam name="TKey">The property type.</typeparam>
        /// <param name="property">The column: one property of the row (<c>r => r.Name</c>; no paths).</param>
        /// <param name="comparer">Compares two values of the property (never <c>null</c> values — those sort first), or <c>null</c>.</param>
        public void SetComparer<TKey>(Expression<Func<T, TKey>> property, IComparer<TKey> comparer)
        {
            var descriptor = PropertyOf(property, nameof(property));
            if (!typeof(TKey).IsAssignableFrom(descriptor.PropertyType))   // r => r.IntProp as long: values are ints
                throw new ArgumentException("The comparer compares " + typeof(TKey).Name + " but " + descriptor.Name
                    + " is " + descriptor.PropertyType.Name + "; use a comparer of the property's type.", nameof(property));
            if (comparer == null) _comparers.Remove(descriptor.Name);
            else _comparers[descriptor.Name] = new BoxedComparer<TKey>(comparer);
        }

        /// <summary>
        /// Sorts by the given columns, first column first:
        /// <c>rows.ApplySort(s => s.By(r => r.Ward).ThenByDescending(r => r.AdmittedOn))</c>.
        /// The columns are checked by the compiler; for column names known only at run time (a sort the
        /// user saved), use <see cref="ApplySort(ListSortDescriptionCollection)"/>.
        /// </summary>
        /// <param name="sort">Adds the columns to the builder it is given.</param>
        /// <exception cref="ArgumentException">A column is not a single property of the row, or no column was given.</exception>
        public void ApplySort(Action<SortBuilder> sort)
        {
            if (sort == null) throw new ArgumentNullException(nameof(sort));
            var builder = new SortBuilder();
            sort(builder);
            if (builder.Keys.Count == 0) throw new ArgumentException("No sort column was given.", nameof(sort));
            Sort(builder.Keys.ToArray());
        }

        /// <summary>
        /// Sorts by the given columns, first column first — what <c>BindingSource.Sort</c> calls with several
        /// columns; also for columns known only at run time (a sort the user saved by column name:
        /// <c>TypeDescriptor.GetProperties(typeof(T))[name]</c>). An empty collection removes the sort.
        /// </summary>
        /// <param name="sorts">The columns and directions.</param>
        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            if (sorts == null) throw new ArgumentNullException(nameof(sorts));
            if (sorts.Count == 0)
            {
                RemoveSort();
                return;
            }

            var keys = new SortKey[sorts.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                var description = sorts[i] ?? throw new ArgumentException("A sort description is null.", nameof(sorts));
                keys[i] = new SortKey(
                    description.PropertyDescriptor ?? throw new ArgumentException("A sort description has no property.", nameof(sorts)),
                    description.SortDirection);
            }

            Sort(keys);
        }

        /// <summary>Removes the sort; the rows keep their current order. Raises one <see cref="ListChangedType.Reset"/>.</summary>
        public void RemoveSort() => RemoveSortCore();

        /// <summary>
        /// Puts the rows in the sort order again — after rows were added or edited, or after changes made
        /// with <see cref="BindingList{T}.RaiseListChangedEvents"/> off. Does nothing when the list is not sorted.
        /// </summary>
        public void Resort()
        {
            if (_sort != null) Sort(_sort);
        }

        /// <summary>
        /// Replaces every row with <paramref name="items"/> (a reload), sorted by the last sort, and raises
        /// one <see cref="ListChangedType.Reset"/> instead of one notification per row.
        /// </summary>
        /// <param name="items">The new rows (copied first, so the list itself may be passed).</param>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var rows = new List<T>(items);
            if (_sort != null) rows = Order(rows, _sort);   // may throw: nothing has changed yet

            var raise = RaiseListChangedEvents;
            RaiseListChangedEvents = false;
            _changing++;
            try
            {
                Clear();
                foreach (var row in rows) Add(row);
            }
            finally
            {
                _changing--;
                RaiseListChangedEvents = raise;
            }

            _toPlace.Clear();
            RaiseReset();
        }

        /// <summary>
        /// Moves the row at <paramref name="from"/> to <paramref name="to"/> and raises one
        /// <see cref="ListChangedType.ItemMoved"/> — a bound grid's current row moves with it (a
        /// <c>CurrencyManager</c> follows the moved row), instead of the delete and add that
        /// <c>RemoveAt</c> + <c>Insert</c> report. The sort is kept (the moved row may be out of order,
        /// even with <see cref="KeepSorted"/>, until its value changes or <see cref="Resort"/>); works with <see cref="BindingList{T}.AllowRemove"/> <c>false</c>. A row being added
        /// (<see cref="BindingList{T}.AddNew"/>) is committed first. Move one row of any list with
        /// <see cref="ListMoveExtensions"/> (<c>rows.MoveUp(row)</c>), which uses this method for this list.
        /// </summary>
        /// <param name="from">The index of the row to move.</param>
        /// <param name="to">Its new index.</param>
        public void Move(int from, int to)
        {
            if (from < 0 || from >= Count) throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= Count) throw new ArgumentOutOfRangeException(nameof(to));
            if (from == to) return;

            CommitPendingNew();   // its index is about to change
            var row = _items[from];
            _items.RemoveAt(from);   // the row stays in the list: its change notifications stay hooked
            _items.Insert(to, row);

            if (RaiseListChangedEvents)
                OnListChanged(new ListChangedEventArgs(ListChangedType.ItemMoved, to, from));
        }

        /// <inheritdoc />
        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            if (prop == null) throw new ArgumentNullException(nameof(prop));
            Sort(new[] { new SortKey(prop, direction) });
        }

        /// <inheritdoc />
        protected override void RemoveSortCore()
        {
            _sort = null;
            _toPlace.Clear();
            RaiseReset();
        }

        /// <inheritdoc />
        protected override object AddNewCore()
        {
            object item;
            _addingNew = true;
            try { item = base.AddNewCore(); }
            finally { _addingNew = false; }
            _pendingNew = item is T row ? IndexOf(row) : -1;
            return item;
        }

        /// <inheritdoc />
        public override void EndNew(int itemIndex)
        {
            var committed = itemIndex >= 0 && itemIndex == _pendingNew;
            if (committed) _pendingNew = -1;
            base.EndNew(itemIndex);
            if (committed && itemIndex < Count) Place(this[itemIndex]);   // the new row is complete now
        }

        /// <inheritdoc />
        public override void CancelNew(int itemIndex)
        {
            var canceled = itemIndex >= 0 && itemIndex == _pendingNew;
            if (canceled) _pendingNew = -1;
            base.CancelNew(itemIndex);
            if (canceled && _toPlace.Count > 0) SchedulePlacing();   // changes held while the row was pending
        }

        /// <inheritdoc />
        protected override void InsertItem(int index, T item) => Change(() => base.InsertItem(index, item));

        /// <inheritdoc />
        protected override void RemoveItem(int index) => Change(() => base.RemoveItem(index));

        /// <inheritdoc />
        protected override void SetItem(int index, T item) => Change(() => base.SetItem(index, item));

        /// <inheritdoc />
        protected override void ClearItems() => Change(() => base.ClearItems());

        // BindingList commits a pending new row (EndNew) at the start of these, before using the index:
        // placing rows then would make the index point at another row. Place once the change is done.
        private void Change(Action change)
        {
            _changing++;
            try { change(); }
            finally { _changing--; }
            if (_changing == 0 && _toPlace.Count > 0) SchedulePlacing();
        }

        /// <inheritdoc />
        protected override void OnListChanged(ListChangedEventArgs e)
        {
            base.OnListChanged(e);
            if (!_keepSorted || _sort == null || _addingNew || e.NewIndex == _pendingNew && e.NewIndex >= 0) return;   // a new row: placed when committed

            switch (e.ListChangedType)
            {
                case ListChangedType.ItemChanged when e.PropertyDescriptor == null || IsSortedBy(e.PropertyDescriptor.Name):
                case ListChangedType.ItemAdded:
                    if (e.NewIndex >= 0 && e.NewIndex < Count) Place(this[e.NewIndex]);
                    break;
                case ListChangedType.Reset when !_ownReset:   // a row's PropertyChanged with no name ("everything changed")
                    Place(default, all: true);
                    break;
            }
        }

        // KeepSorted: the row goes to its place once the change is complete (posted, so a grid that is
        // committing a cell edit finishes first); several changes before that are placed together.
        private void Place(T row, bool all = false)
        {
            if (!_keepSorted || _sort == null) return;
            if (all) _toPlace.Add(row);   // default(T) marks "everything"
            else if (IndexOfReference(_toPlace, row) < 0) _toPlace.Add(row);
            SchedulePlacing();
        }

        private void SchedulePlacing()
        {
            if (_placingPosted || _changing > 0) return;   // a change in progress schedules it when done

            var context = SynchronizationContext.Current;
            if (context == null)
            {
                PlaceNow();
                return;
            }
            _placingPosted = true;
            context.Post(_ => { _placingPosted = false; PlaceNow(); }, null);
        }

        private void PlaceNow()
        {
            if (_toPlace.Count == 0 || !_keepSorted || _sort == null) { _toPlace.Clear(); return; }
            if (_pendingNew >= 0) return;   // kept: placed when the new row is committed (EndNew)
            var rows = _toPlace.Where(r => IndexOfReference(_items, r) >= 0).ToList();
            var everything = _toPlace.Count != rows.Count;   // "everything changed", or a placed row was removed
            _toPlace.Clear();

            var sorted = Order(_items, _sort);
            if (!everything && rows.Count == 1 && _items.Count(r => ReferenceEquals(r, rows[0])) == 1)
            {
                var row = rows[0];
                var others = _items.Where(r => !ReferenceEquals(r, row));
                if (others.SequenceEqual(sorted.Where(r => !ReferenceEquals(r, row)), ReferenceComparer.Instance))
                {
                    Move(IndexOfReference(_items, row), IndexOfReference(sorted, row));   // one ItemMoved
                    return;
                }
            }
            if (!sorted.SequenceEqual(_items, ReferenceComparer.Instance)) Sort(_sort);   // several rows: one Reset
        }

        // Commits the row AddNew created without placing it (the caller is about to move or sort the rows).
        private void CommitPendingNew()
        {
            if (_pendingNew < 0) return;
            var index = _pendingNew;
            _pendingNew = -1;
            base.EndNew(index);
        }

        private static int IndexOfReference(List<T> rows, T row) => rows.FindIndex(r => ReferenceEquals(r, row));

        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private void RaiseReset()
        {
            _ownReset = true;
            try { ResetBindings(); }
            finally { _ownReset = false; }
        }

        private void Sort(SortKey[] keys)
        {
            var sorted = Order(_items, keys);   // may throw: nothing has changed yet

            CommitPendingNew();   // its index is about to change
            _items.Clear();
            _items.AddRange(sorted);                     // a permutation: no item hooks change
            _sort = keys;
            _toPlace.Clear();
            RaiseReset();
        }

        private List<T> Order(List<T> rows, SortKey[] keys)
        {
            var values = new object[keys.Length][];
            var comparers = new IComparer[keys.Length];
            for (var k = 0; k < keys.Length; k++)
            {
                var column = new object[rows.Count];
                for (var i = 0; i < column.Length; i++)
                {
                    var value = keys[k].Property.GetValue(rows[i]);
                    column[i] = value is DBNull ? null : value;
                }
                comparers[k] = ComparerFor(keys[k].Property.Name, column);
                values[k] = column;
            }

            var order = Enumerable.Range(0, rows.Count).ToArray();
            Array.Sort(order, (x, y) =>
            {
                for (var k = 0; k < keys.Length; k++)
                {
                    var descending = keys[k].Direction == ListSortDirection.Descending;
                    var a = values[k][descending ? y : x];   // swap rather than negate (-int.MinValue is int.MinValue)
                    var b = values[k][descending ? x : y];
                    var result = a == null ? (b == null ? 0 : -1) : b == null ? 1 : comparers[k].Compare(a, b);
                    if (result != 0) return result;
                }
                return x.CompareTo(y);   // equal keys keep their order, in both directions
            });

            return order.Select(i => rows[i]).ToList();
        }

        // One comparison for all values of a column, so the order is consistent (comparing a pair by
        // number and another by text could make 2 < 10 < "15" < 2). Text values replace the column's values.
        private IComparer ComparerFor(string propertyName, object[] column)
        {
            if (_comparers.TryGetValue(propertyName, out var custom)) return custom;

            Type type = null;
            var oneType = true;
            foreach (var value in column)
            {
                if (value == null) continue;
                if (type == null) type = value.GetType();
                else if (value.GetType() != type) { oneType = false; break; }
            }

            if (type == null) return _stringComparer;   // all null: never compared
            if (oneType && type == typeof(string)) return _stringComparer;
            if (oneType && IsComparable(type))
                return (IComparer)typeof(Comparer<>).MakeGenericType(type).GetProperty(nameof(Comparer<int>.Default)).GetValue(null);

            for (var i = 0; i < column.Length; i++)
                if (column[i] != null) column[i] = Convert.ToString(column[i], CultureInfo.CurrentCulture);
            return _stringComparer;
        }

        private static bool IsComparable(Type type) =>
            typeof(IComparable).IsAssignableFrom(type)
            || typeof(IComparable<>).MakeGenericType(type).IsAssignableFrom(type);

        private bool IsSortedBy(string propertyName) =>
            _sort != null && _sort.Any(k => k.Property.Name == propertyName);

        private static PropertyDescriptor PropertyOf<TKey>(Expression<Func<T, TKey>> property, string parameterName)
        {
            if (property == null) throw new ArgumentNullException(parameterName);
            var body = property.Body is UnaryExpression convert && convert.NodeType == ExpressionType.Convert ? convert.Operand : property.Body;
            if (!(body is MemberExpression member) || member.Expression != property.Parameters[0] || !(member.Member is PropertyInfo))
                throw new ArgumentException("Sort by one property of the row (r => r.Name); paths and fields are not supported: " + property, parameterName);
            return TypeDescriptor.GetProperties(typeof(T)).Find(member.Member.Name, ignoreCase: false)
                ?? throw new ArgumentException("\"" + member.Member.Name + "\" is not a bindable property of " + typeof(T).Name + ".", parameterName);
        }

        /// <summary>
        /// The columns of a sort, given to <see cref="ApplySort(Action{SortBuilder})"/>: <c>s.By(r => r.Ward).ThenByDescending(r => r.No)</c>.
        /// Each call adds the next column.
        /// </summary>
        public sealed class SortBuilder
        {
            internal SortBuilder() { }

            internal List<SortKey> Keys { get; } = new List<SortKey>();

            /// <summary>Sorts by <paramref name="property"/>, ascending.</summary>
            /// <typeparam name="TKey">The property type.</typeparam>
            /// <param name="property">One property of the row (<c>r => r.Ward</c>).</param>
            /// <returns>This builder, for the next column.</returns>
            public SortBuilder By<TKey>(Expression<Func<T, TKey>> property) => Add(property, ListSortDirection.Ascending);

            /// <summary>Sorts by <paramref name="property"/>, descending.</summary>
            /// <typeparam name="TKey">The property type.</typeparam>
            /// <param name="property">One property of the row.</param>
            /// <returns>This builder, for the next column.</returns>
            public SortBuilder ByDescending<TKey>(Expression<Func<T, TKey>> property) => Add(property, ListSortDirection.Descending);

            /// <summary>Then by <paramref name="property"/>, ascending, for rows equal so far.</summary>
            /// <typeparam name="TKey">The property type.</typeparam>
            /// <param name="property">One property of the row.</param>
            /// <returns>This builder, for the next column.</returns>
            public SortBuilder ThenBy<TKey>(Expression<Func<T, TKey>> property) => Add(property, ListSortDirection.Ascending);

            /// <summary>Then by <paramref name="property"/>, descending, for rows equal so far.</summary>
            /// <typeparam name="TKey">The property type.</typeparam>
            /// <param name="property">One property of the row.</param>
            /// <returns>This builder, for the next column.</returns>
            public SortBuilder ThenByDescending<TKey>(Expression<Func<T, TKey>> property) => Add(property, ListSortDirection.Descending);

            private SortBuilder Add<TKey>(Expression<Func<T, TKey>> property, ListSortDirection direction)
            {
                Keys.Add(new SortKey(PropertyOf(property, nameof(property)), direction));
                return this;
            }
        }

        private sealed class BoxedComparer<TKey> : IComparer
        {
            private readonly IComparer<TKey> _comparer;

            public BoxedComparer(IComparer<TKey> comparer) => _comparer = comparer;

            public int Compare(object x, object y) => _comparer.Compare((TKey)x, (TKey)y);
        }

        internal struct SortKey
        {
            public SortKey(PropertyDescriptor property, ListSortDirection direction)
            {
                Property = property;
                Direction = direction;
            }

            public PropertyDescriptor Property { get; }

            public ListSortDirection Direction { get; }
        }
    }
}
