using System;

namespace Kibinago.Presenters
{
    /// <summary>
    /// A presenter driving one view. Disposed with its window or control. Derive from
    /// <see cref="WindowPresenterBase{TView}"/> or <see cref="ControlPresenterBase{TView}"/>
    /// (or their parameterized variants) rather than implementing it directly.
    /// </summary>
    public interface IPresenter : IDisposable
    {
        /// <summary>
        /// The view interface the presenter drives; WindowNavigator uses it to find the registered
        /// view implementation.
        /// </summary>
        Type ViewInterfaceType { get; }
    }
}
