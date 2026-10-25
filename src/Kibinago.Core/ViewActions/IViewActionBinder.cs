namespace Kibinago.ViewActions
{
    /// <summary>
    /// Minimal contract exposed by a view's action binder to its presenter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Views expose an action binder so that the presenter can connect view-side UI events
    /// (button clicks, checkbox toggles, ...) to handlers registered on a <see cref="ViewActionDispatcher"/>.
    /// </para>
    /// <para>
    /// This interface intentionally contains only what the presenter (and framework base classes) need.
    /// Configuration APIs such as <c>Add</c>, <c>RegisterStrategy</c>, and bulk-binding extensions
    /// remain on the concrete <c>ViewActionBinder</c> class because they are view-implementation details.
    /// </para>
    /// </remarks>
    public interface IViewActionBinder
    {
        /// <summary>
        /// Binds all configured controls and connects this binder to the given dispatcher,
        /// enabling automatic dispatch and automatic CanExecute UI refresh.
        /// </summary>
        /// <param name="dispatcher">The dispatcher to forward actions to.</param>
        void Bind(ViewActionDispatcher dispatcher);

        /// <summary>
        /// Stops dispatching to the bound dispatcher and refreshing control states. The framework calls
        /// it when the presenter that bound this binder is disposed, so a control that outlives its
        /// presenter does nothing when clicked. The controls and the view are left as they are.
        /// Calling it again, or before <see cref="Bind"/>, does nothing.
        /// </summary>
        void Unbind();
    }
}
