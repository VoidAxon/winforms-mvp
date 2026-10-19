using System;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// EventArgs carrying the <see cref="ViewAction"/> key for the
    /// <see cref="ViewActionDispatcher.ActionExecuted"/> event. Wrapping in
    /// EventArgs is required because <see cref="ViewAction"/> is a struct and
    /// .NET Framework 4.0's <c>EventHandler&lt;T&gt;</c> requires
    /// <c>T : EventArgs</c>.
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
