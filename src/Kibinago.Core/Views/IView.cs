namespace Kibinago.Views
{
    /// <summary>
    /// The root of every view. Applications do not implement it directly: a view interface derives
    /// from <see cref="IWindowView"/> (a Form) or <see cref="IControlView"/> (a UserControl), the role
    /// its presenter base requires.
    /// </summary>
    /// <remarks>
    /// Framework APIs that work with any view take it: <c>PresenterTestHost</c>, Navigator's
    /// <c>owner</c>, busy indication, disposal tracking.
    /// </remarks>
    public interface IView
    {
    }
}
