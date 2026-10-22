using System;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// Event data for a view event that reports a value changing from <see cref="OldValue"/> to
    /// <see cref="NewValue"/>: <c>event EventHandler&lt;ValueChangedEventArgs&lt;int&gt;&gt; QuantityChanged;</c>.
    /// </summary>
    /// <remarks>
    /// View events need no hand-written EventArgs class and must not pass a WinForms one (analyzer
    /// MVP004): use <c>EventHandler</c> when the presenter reads the value from the view,
    /// <c>EventHandler&lt;T&gt;</c> with a business type to send the value (<c>EventHandler&lt;int&gt; RecordActivated</c>),
    /// and this class when the presenter needs the value before the change too (to compare, or to put it back).
    /// </remarks>
    public class ValueChangedEventArgs<T> : EventArgs
    {
        public ValueChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>The value before the change.</summary>
        public T OldValue { get; }

        /// <summary>The value after the change.</summary>
        public T NewValue { get; }
    }
}
