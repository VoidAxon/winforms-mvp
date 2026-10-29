using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Lets the framework attach a view to a presenter (WindowNavigator, <c>Connect</c>,
    /// <c>AttachChild</c>, <c>PresenterTestHost</c>). The presenter bases implement it explicitly;
    /// application code does not call it.
    /// </summary>
    /// <typeparam name="TView">The view interface type.</typeparam>
    public interface IViewAttacher<in TView> where TView : IView
    {
        /// <summary>
        /// Attaches <paramref name="view"/> to the presenter. A presenter takes exactly one view:
        /// attaching a second one throws <see cref="System.InvalidOperationException"/>.
        /// </summary>
        /// <param name="view">The view to drive; must not be <c>null</c>.</param>
        void AttachView(TView view);
    }
}
