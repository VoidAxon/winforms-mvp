using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Kibinago.Common
{
    /// <summary>
    /// Moves one row of a list — the 上へ / 下へ / 最上へ / 最下へ buttons of a list screen:
    /// <c>rows.MoveUp(row)</c> returns <c>false</c> when the row cannot move (already first), so the
    /// presenter knows there is nothing to save.
    /// </summary>
    /// <remarks>
    /// Each list type is moved with its best notification: a <see cref="SortableBindingList{T}"/> with
    /// <see cref="SortableBindingList{T}.Move"/> (one <see cref="ListChangedType.ItemMoved"/>: a bound
    /// grid's current row moves with the row); an <see cref="ObservableCollection{T}"/> with its own
    /// <see cref="ObservableCollection{T}.Move"/>; any other list (<see cref="BindingList{T}"/>,
    /// <see cref="List{T}"/>) with <c>RemoveAt</c> + <c>Insert</c> — a bound <see cref="BindingList{T}"/>
    /// reports that as a delete and an add, so the grid's current row lands on another row: the view
    /// makes the moved row current again (select it by record key), or use a
    /// <see cref="SortableBindingList{T}"/>. A <see cref="BindingList{T}"/> commits a row being added
    /// before the move; with <see cref="BindingList{T}.AllowRemove"/> <c>false</c> its <c>RemoveAt</c> throws.
    /// </remarks>
    public static class ListMoveExtensions
    {
        /// <summary>Moves the row at <paramref name="from"/> to <paramref name="to"/>.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="from">The index of the row to move.</param>
        /// <param name="to">Its new index.</param>
        public static void Move<T>(this IList<T> list, int from, int to)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (from < 0 || from >= list.Count) throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= list.Count) throw new ArgumentOutOfRangeException(nameof(to));
            if (from == to) return;

            switch (list)
            {
                case SortableBindingList<T> sortable:
                    sortable.Move(from, to);
                    break;
                case ObservableCollection<T> observable:
                    observable.Move(from, to);
                    break;
                default:
                    var item = list[from];
                    list.RemoveAt(from);
                    list.Insert(to, item);
                    break;
            }
        }

        /// <summary>Moves <paramref name="item"/> up by one.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="item">The row to move.</param>
        /// <returns><c>false</c> when it is the first row or not in the list (nothing moved).</returns>
        public static bool MoveUp<T>(this IList<T> list, T item) => MoveTo(list, item, (from, count) => from - 1);

        /// <summary>Moves <paramref name="item"/> down by one.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="item">The row to move.</param>
        /// <returns><c>false</c> when it is the last row or not in the list (nothing moved).</returns>
        public static bool MoveDown<T>(this IList<T> list, T item) => MoveTo(list, item, (from, count) => from + 1);

        /// <summary>Moves <paramref name="item"/> to the top.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="item">The row to move.</param>
        /// <returns><c>false</c> when it already is the first row or not in the list (nothing moved).</returns>
        public static bool MoveToTop<T>(this IList<T> list, T item) => MoveTo(list, item, (from, count) => 0);

        /// <summary>Moves <paramref name="item"/> to the bottom.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="item">The row to move.</param>
        /// <returns><c>false</c> when it already is the last row or not in the list (nothing moved).</returns>
        public static bool MoveToBottom<T>(this IList<T> list, T item) => MoveTo(list, item, (from, count) => count - 1);

        private static bool MoveTo<T>(IList<T> list, T item, Func<int, int, int> target)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            var from = list.IndexOf(item);
            if (from < 0) return false;
            var to = target(from, list.Count);
            if (to < 0 || to >= list.Count || to == from) return false;
            list.Move(from, to);
            return true;
        }
    }
}
