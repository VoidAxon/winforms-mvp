namespace WinformsMVP.Core.Views
{
    /// <summary>
    /// Marker contract for any view participating in the MVP framework.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface is intentionally empty. It identifies a type as a "view" so that
    /// presenter base classes can generically reference it, but it imposes no obligations
    /// on the implementer.
    /// </para>
    /// <para>
    /// Views that need to bind UI controls to <c>ViewAction</c> keys should implement
    /// <see cref="IActionableView"/> instead (which already extends this interface).
    /// </para>
    /// </remarks>
    public interface IViewBase
    {
    }
}
