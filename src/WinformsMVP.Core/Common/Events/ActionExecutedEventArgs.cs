using System;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// EventArgs carrying the <see cref="ViewAction"/> key for the
    /// <see cref="ViewActionDispatcher.ActionExecuted"/> event.
    /// </summary>
    public class ActionExecutedEventArgs : EventArgs
    {
        public ActionExecutedEventArgs(ViewAction actionKey)
        {
            ActionKey = actionKey;
        }

        public ViewAction ActionKey { get; }
    }
}
