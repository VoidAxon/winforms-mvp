namespace Kibinago.Views
{
    /// <summary>
    /// The view of a window (a Form): what <c>WindowPresenterBase</c> requires and Navigator creates.
    /// It has an <see cref="IActionBindable.ActionBinder"/> (<c>null</c> when the window uses no
    /// actions) and nothing else: closing is the presenter's (<c>CanClose</c> / <c>RequestClose</c>).
    /// </summary>
    /// <remarks>A view is a window or a control, never both.</remarks>
    public interface IWindowView : IView, IActionBindable
    {
    }
}
