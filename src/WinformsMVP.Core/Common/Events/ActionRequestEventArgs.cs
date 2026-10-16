using System;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Common.Events
{
    public class ActionRequestEventArgs : EventArgs
    {
        public ActionRequestEventArgs(ViewAction actionKey)
        {
            ActionKey = actionKey;
        }
        public ViewAction ActionKey { get; }
    }

    public class ActionRequestEventArgs<T> : ActionRequestEventArgs, IActionRequestEventArgsWithValue
    {
        public ActionRequestEventArgs(ViewAction actionKey, T value) : base(actionKey)
        {
            Value = value;
        }

        public T Value { get; }
        public object GetValue() => Value;
    }
}
