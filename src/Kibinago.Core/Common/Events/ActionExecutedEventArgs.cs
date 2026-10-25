using System;
using Kibinago.ViewActions;

namespace Kibinago.Common.Events
{
    /// <summary>
    /// EventArgs carrying the <see cref="ViewAction"/> key for the
    /// <see cref="ViewActionDispatcher.ActionExecuted"/> event.
    /// </summary>
    public class ActionExecutedEventArgs : EventArgs
    {
        /// <summary>Creates event data for the action that has executed.</summary>
        /// <param name="actionKey">The action whose handler completed.</param>
        public ActionExecutedEventArgs(ViewAction actionKey)
        {
            ActionKey = actionKey;
        }

        /// <summary>The action whose handler completed.</summary>
        public ViewAction ActionKey { get; }
    }
}
