using Kibinago.ViewActions;

namespace Kibinago.Views
{
    /// <summary>
    /// A view that exposes an <see cref="IViewActionBinder"/> the framework binds to its presenter's
    /// dispatcher. Every <see cref="IWindowView"/> and <see cref="IControlView"/> is one.
    /// </summary>
    /// <remarks>
    /// <para>The framework reads <see cref="ActionBinder"/> once, after <c>RegisterViewActions</c>, binds
    /// it (control Enabled states follow <c>canExecute</c>) and unbinds that same binder when the presenter
    /// is disposed.</para>
    /// <para>Return <c>null</c> when the view takes no part in automatic binding: a view without actions,
    /// or one that binds its own binder and forwards actions through an event (the explicit pattern).</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IMyView : IWindowView   // IWindowView is IActionBindable
    /// {
    ///     string Title { get; set; }
    /// }
    ///
    /// public partial class MyForm : Form, IMyView
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
    public interface IActionBindable
    {
        /// <summary>
        /// The view's action binder, or <c>null</c> for no automatic binding. Read once by the framework,
        /// after <c>RegisterViewActions</c>.
        /// </summary>
        IViewActionBinder ActionBinder { get; }
    }
}
