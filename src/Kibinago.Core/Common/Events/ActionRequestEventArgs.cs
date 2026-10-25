using System;
using Kibinago.ViewActions;

namespace Kibinago.Common.Events
{
    /// <summary>
    /// Event data for a triggered <see cref="ViewAction"/>: raised by the binder's <c>ActionTriggered</c>
    /// event and, in the explicit pattern, forwarded by the view as its <c>ActionRequest</c> event.
    /// </summary>
    public class ActionRequestEventArgs : EventArgs
    {
        /// <summary>Creates event data for an action triggered without a payload.</summary>
        /// <param name="actionKey">The triggered action.</param>
        public ActionRequestEventArgs(ViewAction actionKey)
        {
            ActionKey = actionKey;
        }
        /// <summary>The triggered action.</summary>
        public ViewAction ActionKey { get; }
    }

    /// <summary>
    /// <see cref="ActionRequestEventArgs"/> carrying a payload (e.g. the new value of a bound
    /// check box or combo box), which the presenter passes on to the handler.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    public class ActionRequestEventArgs<T> : ActionRequestEventArgs, IActionRequestEventArgsWithValue
    {
        /// <summary>Creates event data for an action triggered with a payload.</summary>
        /// <param name="actionKey">The triggered action.</param>
        /// <param name="value">The payload.</param>
        public ActionRequestEventArgs(ViewAction actionKey, T value) : base(actionKey)
        {
            Value = value;
        }

        /// <summary>The payload.</summary>
        public T Value { get; }
        /// <inheritdoc />
        public object GetValue() => Value;
    }
}
