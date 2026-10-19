using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Creates Presenter instances on demand. Used by a parent Presenter to obtain a
    /// dependency-injected child Presenter at runtime without holding a direct reference
    /// to any concrete DI container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Constructor dependencies of a child Presenter
    /// (repositories, services, loggers) are normally resolved by a DI container, but a
    /// parent Presenter lives outside the container's construction graph and needs an
    /// abstraction to ask for a new child instance. <see cref="IPresenterFactory"/> is
    /// that abstraction; it shields user code from the specific DI container in use.
    /// </para>
    /// <para>
    /// <b>What this is not.</b> Runtime parameters (the value of a user-edited record's
    /// id, the file path the user picked, etc.) are <b>not</b> passed through
    /// <see cref="Create{TPresenter}"/>. Implement <see cref="IInitializable{TParam}"/>
    /// on the Presenter and pass them via <c>Navigator.For(p).WithParam(...)</c> —
    /// constructor injection is for stable dependencies, <c>Initialize</c> is for
    /// per-invocation data.
    /// </para>
    /// <para>
    /// <b>Legacy projects without a DI container</b> do not need this interface at all —
    /// just construct Presenters directly (<c>new MyPresenter()</c>) and pass them to
    /// <c>WindowNavigator</c>. <see cref="IPresenterFactory"/> only earns its keep when a
    /// container is wired in.
    /// </para>
    /// </remarks>
    public interface IPresenterFactory
    {
        /// <summary>
        /// Creates a Presenter of the requested type, with its constructor dependencies
        /// resolved by the underlying mechanism (typically a DI container).
        /// </summary>
        /// <typeparam name="TPresenter">Concrete Presenter type to create.</typeparam>
        /// <returns>A fully-constructed Presenter instance, ready to be attached to a
        /// View. Initialization with runtime parameters happens later via
        /// <c>Navigator.WithParam(...)</c>, not here.</returns>
        TPresenter Create<TPresenter>() where TPresenter : IPresenter;
    }
}
