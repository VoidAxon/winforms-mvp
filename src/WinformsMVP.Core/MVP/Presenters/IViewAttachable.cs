using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Non-generic, framework-internal counterpart to <see cref="IViewAttacher{TView}"/>.
    /// Lets <c>WindowNavigator</c> attach a freshly created view to a presenter without
    /// knowing the concrete <c>TView</c> at compile time — replacing the previous
    /// reflection-based <c>AttachView</c> invocation with a direct virtual call.
    /// </summary>
    /// <remarks>
    /// Implemented once, explicitly, in <see cref="PresenterBase{TView}"/>, so it does not
    /// appear on the public surface of concrete presenters. The implementation casts the
    /// incoming <see cref="IViewBase"/> to the presenter's <c>TView</c>; the cast is an
    /// internal invariant guaranteed by the view-mapping registry handing back the correct
    /// implementation type.
    /// </remarks>
    internal interface IViewAttachable
    {
        void AttachView(IViewBase view);

        /// <summary>True once a view has been attached. Lets the <c>Connect</c> extension stay
        /// idempotent without touching the protected <c>View</c> property.</summary>
        bool IsViewAttached { get; }
    }
}
