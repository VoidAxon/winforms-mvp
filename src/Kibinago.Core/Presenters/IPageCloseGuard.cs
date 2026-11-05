using System.Threading.Tasks;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Implemented by a page — a control presenter pushed into a <see cref="PageStack"/>, or a panel that owns
    /// one — that may refuse to be discarded (unsaved edits). Implement it explicitly so it stays off the
    /// presenter's public surface:
    /// <code>
    /// Task&lt;bool&gt; IPageCloseGuard.CanCloseAsync()
    ///     =&gt; Task.FromResult(!_tracker.IsChanged || Messages.ConfirmYesNo("変更を破棄しますか?"));
    /// </code>
    /// Window presenters do not implement it: they override <c>CanClose</c> / <c>CanCloseAsync(CloseReason)</c>.
    /// </summary>
    public interface IPageCloseGuard
    {
        /// <summary>
        /// Whether this page may be discarded. Asked before a <see cref="PageStack"/> jump removes the page,
        /// before its window closes, and when application code asks (an ancestor about to dispose it, through
        /// <see cref="PageStack.CanCloseAllAsync"/>). It may be asked again after a refusal elsewhere; work done
        /// here (a save) is not undone when another page refuses.
        /// </summary>
        /// <returns><c>true</c> when the page may be discarded. A faulted or cancelled task refuses.</returns>
        Task<bool> CanCloseAsync();
    }
}
