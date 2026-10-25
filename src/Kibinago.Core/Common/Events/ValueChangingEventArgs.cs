using System.ComponentModel;

namespace Kibinago.Common.Events
{
    /// <summary>
    /// Event data for a view event raised <b>before</b> a value changes from <see cref="OldValue"/> to
    /// <see cref="NewValue"/>; the presenter sets <see cref="CancelEventArgs.Cancel"/> to keep the old value
    /// (e.g. stay on the selected record while it has unsaved edits). The pair of
    /// <see cref="ValueChangedEventArgs{T}"/>, which reports a change after it happened.
    /// </summary>
    /// <remarks>
    /// The view raises it, then applies the change only when <c>Cancel</c> is still false:
    /// <code>
    /// var e = new ValueChangingEventArgs&lt;int&gt;(current, next);
    /// SelectedRecordChanging?.Invoke(this, e);
    /// if (!e.Cancel) SelectRow(next);
    /// </code>
    /// </remarks>
    public class ValueChangingEventArgs<T> : CancelEventArgs
    {
        /// <summary>Creates event data for a pending change from <paramref name="oldValue"/> to <paramref name="newValue"/>; <c>Cancel</c> starts false.</summary>
        /// <param name="oldValue">The current value.</param>
        /// <param name="newValue">The value the view is about to change to.</param>
        public ValueChangingEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>The current value, kept when the change is cancelled.</summary>
        public T OldValue { get; }

        /// <summary>The value the view is about to change to.</summary>
        public T NewValue { get; }
    }
}
