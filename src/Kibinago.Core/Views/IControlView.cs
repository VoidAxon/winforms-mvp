namespace Kibinago.Views
{
    /// <summary>
    /// The view of a panel (a UserControl): what <c>ControlPresenterBase</c>, <c>AttachChild</c> and a
    /// control's <c>Connect</c> require. It has an <see cref="IActionBindable.ActionBinder"/>
    /// (<c>null</c> when the panel uses no actions).
    /// </summary>
    /// <remarks>A view is a window or a control, never both.</remarks>
    public interface IControlView : IView, IActionBindable
    {
    }
}
