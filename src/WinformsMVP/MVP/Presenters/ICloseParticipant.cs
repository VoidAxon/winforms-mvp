using System;
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Framework-internal hooks the <c>WindowLifecycleController</c> and <c>presenter.Connect(...)</c> use to drive a Presenter's
    /// close lifecycle without referencing its <c>TView</c> or result type. Implemented
    /// explicitly by <see cref="WindowPresenterBaseCore{TView}"/>.
    /// </summary>
    internal interface ICloseParticipant
    {
        /// <summary>Injects the close sink (Push). Called once, before <c>Initialize</c>. The
        /// presenter's <c>RequestClose</c> overloads push through this sink.</summary>
        void BindCloseSink(ICloseSink sink);

        /// <summary>Runs the Pull gate. <paramref name="proceed"/>(true) allows, (false) blocks.
        /// Called synchronously for sync deciders; may be deferred for async ones.</summary>
        void CanCloseGate(CloseReason reason, Action<bool> proceed);
    }
}
