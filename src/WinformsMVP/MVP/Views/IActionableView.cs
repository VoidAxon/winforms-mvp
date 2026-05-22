using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Core.Views
{
    /// <summary>
    /// A view that participates in the ViewAction system by exposing an <see cref="IViewActionBinder"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Views that bind UI controls to <see cref="ViewAction"/> keys should implement this interface
    /// so that the framework (and the corresponding presenter) can wire up the dispatcher and
    /// receive control-triggered action events.
    /// </para>
    /// <para>
    /// Views that have no buttons, menu items, or other action-producing controls (for example,
    /// a static splash screen) can implement <see cref="IViewBase"/> directly and skip this
    /// interface entirely.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IMyView : IWindowView // IWindowView already inherits IActionableView
    /// {
    ///     string Title { get; set; }
    /// }
    ///
    /// public class MyForm : Form, IMyView
    /// {
    ///     private readonly ViewActionBinder _binder = new ViewActionBinder();
    ///     public IViewActionBinder ActionBinder =&gt; _binder;
    ///
    ///     public MyForm()
    ///     {
    ///         InitializeComponent();
    ///         _binder.Add(CommonActions.Save, _saveButton);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IActionableView : IViewBase
    {
        /// <summary>
        /// The view's action binder. The framework binds this to the presenter's dispatcher
        /// automatically once <c>RegisterViewActions</c> has run.
        /// </summary>
        IViewActionBinder ActionBinder { get; }
    }
}
