namespace Kibinago.Presenters
{
    /// <summary>
    /// A presenter that initializes without parameters. The framework calls <see cref="Initialize"/>
    /// after the view is attached (WindowNavigator, <c>Connect</c>, <c>AttachChild</c>,
    /// <c>PresenterTestHost</c>); the presenter bases implement it explicitly.
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// Initializes the presenter against its attached view: registers the view actions, binds the
        /// view's action binder and runs <c>OnInitialize</c>. Throws <see cref="System.InvalidOperationException"/>
        /// when no view is attached.
        /// </summary>
        void Initialize();
    }

    /// <summary>
    /// A presenter that needs runtime data to initialize. Constructor parameters are for injected
    /// dependencies only; runtime arguments come through <see cref="Initialize"/> (a parameters
    /// class when there are several values).
    /// </summary>
    /// <typeparam name="TParam">The type of the initialization parameters.</typeparam>
    public interface IInitializable<in TParam>
    {
        /// <summary>
        /// Initializes the presenter with <paramref name="param"/> against its attached view. Throws
        /// <see cref="System.InvalidOperationException"/> when no view is attached.
        /// </summary>
        /// <param name="param">The runtime data the presenter works on.</param>
        void Initialize(TParam param);
    }
}
