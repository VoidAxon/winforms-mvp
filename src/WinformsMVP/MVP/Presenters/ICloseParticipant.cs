using System;
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Framework-internal hooks the <c>WindowCloseController</c> uses to drive a Presenter's
    /// close lifecycle without referencing its <c>TView</c> or <c>TResult</c>. Implemented
    /// explicitly by <see cref="WindowPresenterBaseCore{TView}"/>.
    /// </summary>
    internal interface ICloseParticipant
    {
        /// <summary>Injects the close sink (Push). Called once, before <c>Initialize</c>.</summary>
        void BindCloseSink(ICloseSink sink);

        /// <summary>Routes a typed/untyped <c>RequestClose</c> to the injected sink.</summary>
        void RequestCloseCore(object result, InteractionStatus status);

        /// <summary>Runs the Pull gate. <paramref name="proceed"/>(true) allows, (false) blocks.
        /// Called synchronously for sync deciders; may be deferred for async ones.</summary>
        void CanCloseGate(CloseReason reason, Action<bool> proceed);
    }
}
