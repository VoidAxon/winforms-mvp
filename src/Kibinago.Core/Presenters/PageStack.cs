using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>
    /// The pages of one in-window drill-down (customer list → customer detail → order edit) with a breadcrumb,
    /// owned by a presenter (a window or a panel). Pages are control presenters attached to the owner like
    /// <c>AttachChild</c> children; the pages below the top stay alive (the view hides them), popped pages are
    /// disposed. A jump back asks every page it discards (<see cref="IPageCloseGuard"/>) first, all or nothing,
    /// and the owner's window asks every page before it closes.
    /// </summary>
    /// <remarks>
    /// <para>Create it in the owner's constructor (<c>_pages = new PageStack(this)</c>); push from
    /// <c>OnInitialize</c> on. The view creates and removes the page controls: the owner's view interface
    /// exposes methods such as <c>IOrderEditView PushOrderEdit(string title)</c> and
    /// <c>void RemovePage(IControlView page)</c>, and <c>RemovePage</c> is passed to <c>Push</c>.</para>
    /// <para>While a check or a removal is running the stack is busy (<see cref="IsBusy"/>): <c>Push</c> throws and
    /// the pops answer <c>false</c>; requests are not queued. Check <see cref="IsBusy"/> before the view creates a
    /// page control. Busy blocks stack operations only, not editing in the pages.</para>
    /// <para>Results travel back through callbacks in a page's parameters (<c>OrderEditParams.Saved</c>). There is
    /// no "shown again" notification: a page that needs one gets a semantic event from its own view.</para>
    /// <para>Single-threaded (UI thread), like the presenters that use it.</para>
    /// </remarks>
    public sealed class PageStack
    {
        private readonly List<PageEntry> _entries = new List<PageEntry>();
        private bool _dead;
        private int _operations;

        /// <summary>Creates an empty stack owned by <paramref name="owner"/>.</summary>
        /// <param name="owner">The presenter whose view shows the pages — typically <c>this</c> in the owner's
        /// constructor. Construction touches no service, view or overridable code.</param>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="owner"/> is not a framework presenter.</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="owner"/> is disposed.</exception>
        public PageStack(IPresenter owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var composable = owner as IComposablePresenter;
            if (composable == null)
                throw new ArgumentException(
                    owner.GetType().Name + " is not a framework presenter; a page stack is owned by a window or control presenter.",
                    nameof(owner));
            if (composable.IsDisposed) throw new ObjectDisposedException(owner.GetType().Name);
            Owner = composable;
            composable.RegisterStack(this);
            // Created by code that runs inside a page-close check of this tree (a guard): born reserved.
            PageCloseCheck.FindReserving(composable)?.Enrol(this);
        }

        /// <summary>The number of pages on the stack.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// True while a check (a page's <see cref="IPageCloseGuard"/> deciding) or a removal is running, or while
        /// the owner's window close holds the stack. <c>Push</c> throws then and the pops answer <c>false</c>.
        /// It blocks stack operations only; it does not disable editing or other actions in the pages.
        /// </summary>
        public bool IsBusy => _operations > 0 || Reservation != null;

        internal IComposablePresenter Owner { get; }

        internal IReadOnlyList<PageEntry> Entries => _entries;

        /// <summary>The check holding this stack, or null.</summary>
        internal PageCloseCheck Reservation { get; set; }

        /// <summary>A snapshot of the entries, top first.</summary>
        internal IReadOnlyList<PageEntry> EntriesTopFirst()
        {
            var list = new List<PageEntry>(_entries);
            list.Reverse();
            return list;
        }

        internal bool IsDead => _dead || Owner.IsDisposed;

        /// <summary>
        /// Attaches <paramref name="page"/> to <paramref name="view"/>, initializes it and puts it on top. The page
        /// is owned like an <c>AttachChild</c> child: it uses the owner's services and is disposed with the owner.
        /// </summary>
        /// <typeparam name="TView">The page's view interface.</typeparam>
        /// <param name="page">The new page's presenter (<c>new</c>-ed by the owner).</param>
        /// <param name="view">The page control the owner's view has just created.</param>
        /// <param name="removeView">Removes that control (and its breadcrumb item) when the page leaves the stack;
        /// it must tolerate a control that is already disposed.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ObjectDisposedException">The owner, the page or the page's control is disposed.</exception>
        /// <exception cref="InvalidOperationException">The stack is busy; the owner has no view yet; the page is
        /// already attached; or the view is used by another presenter.</exception>
        /// <remarks>A rejection before the page is accepted changes nothing (the page is not disposed and
        /// <paramref name="removeView"/> is not called). A failure after it (the page's <c>OnInitialize</c> throws)
        /// disposes the page and calls <paramref name="removeView"/> once, then propagates.</remarks>
        public void Push<TView>(ControlPresenterBase<TView> page, TView view, Action<TView> removeView)
            where TView : IControlView
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            PushCore(page, view, removeView, () => ((IInitializable)page).Initialize());
        }

        /// <summary>
        /// Attaches a page that needs initialization parameters (its input and result callbacks) and puts it on
        /// top. Same contract as <see cref="Push{TView}(ControlPresenterBase{TView}, TView, Action{TView})"/>.
        /// </summary>
        /// <typeparam name="TView">The page's view interface.</typeparam>
        /// <typeparam name="TParam">The page's parameter type.</typeparam>
        /// <param name="page">The new page's presenter.</param>
        /// <param name="view">The page control the owner's view has just created.</param>
        /// <param name="parameters">The page's parameters.</param>
        /// <param name="removeView">Removes that control when the page leaves the stack.</param>
        /// <exception cref="ArgumentNullException">An argument other than <paramref name="parameters"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The owner, the page or the page's control is disposed.</exception>
        /// <exception cref="InvalidOperationException">The stack is busy; the owner has no view yet; the page is
        /// already attached; or the view is used by another presenter.</exception>
        public void Push<TView, TParam>(ControlPresenterBase<TView, TParam> page, TView view, TParam parameters,
            Action<TView> removeView)
            where TView : IControlView
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            PushCore(page, view, removeView, () => ((IInitializable<TParam>)page).Initialize(parameters));
        }

        /// <summary>
        /// Keeps the first <paramref name="remainingCount"/> pages and discards the ones above them (a breadcrumb
        /// jump), after every discarded page — and the pages of stacks they own — agreed.
        /// </summary>
        /// <param name="remainingCount">How many pages stay (0 empties the stack).</param>
        /// <returns><c>true</c> when the pages were removed (or nothing had to be); <c>false</c> when a page refused,
        /// the stack was busy, or the owner is disposed — then nothing is removed.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="remainingCount"/> is outside
        /// 0..<see cref="Count"/>.</exception>
        public Task<bool> PopToCountAsync(int remainingCount)
        {
            if (IsDead || IsBusy) return Completed(false);
            if (remainingCount < 0 || remainingCount > _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(remainingCount), remainingCount,
                    "remainingCount must be between 0 and Count (" + _entries.Count + ").");
            if (remainingCount == _entries.Count) return Completed(true);
            return PopCoreAsync(remainingCount);
        }

        /// <summary>Discards the top page after it agreed; <c>true</c> on an empty stack. Same rules as
        /// <see cref="PopToCountAsync"/>.</summary>
        /// <returns><c>true</c> when the top page was removed or the stack was empty; <c>false</c> when it
        /// refused, the stack was busy, or the owner is disposed.</returns>
        public Task<bool> PopAsync()
        {
            if (IsDead || IsBusy) return Completed(false);
            if (_entries.Count == 0) return Completed(true);
            return PopToCountAsync(_entries.Count - 1);
        }

        /// <summary>
        /// Asks every page (nested stacks included) whether it may be discarded, removing nothing — for a panel
        /// that owns this stack and is about to be disposed by an ancestor; the panel forwards its own
        /// <see cref="IPageCloseGuard"/> here:
        /// <code>Task&lt;bool&gt; IPageCloseGuard.CanCloseAsync() =&gt; _pages.CanCloseAllAsync();</code>
        /// Reached that way from a jump or a window close, it answers within that decision (each page is asked once).
        /// </summary>
        /// <returns><c>true</c> when every page agreed (or there are none); <c>false</c> when one refused, the stack
        /// was busy, or the owner is disposed.</returns>
        public Task<bool> CanCloseAllAsync()
        {
            if (IsDead) return Completed(false);
            var holder = Reservation;
            if (holder != null)
                return holder.CanJoin(this) ? holder.AskStackAsync(this) : Completed(false);
            if (IsBusy) return Completed(false);
            return CanCloseAllCoreAsync();
        }

        private async Task<bool> CanCloseAllCoreAsync()
        {
            var check = new PageCloseCheck(Owner);
            var pages = _entries.Select(e => e.Page).ToList();
            if (!check.TryReserve(StacksWith(pages), pages)) return false;
            try
            {
                return await check.AskStackAsync(this);
            }
            finally
            {
                check.Release();
            }
        }

        private async Task<bool> PopCoreAsync(int remainingCount)
        {
            var targets = EntriesTopFirst().Take(_entries.Count - remainingCount).ToList();
            var check = new PageCloseCheck(Owner);
            var pages = targets.Select(e => e.Page).ToList();
            if (!check.TryReserve(StacksWith(pages), pages)) return false;
            check.MarkAsking(this);
            List<Exception> failures;
            try
            {
                if (!await check.AskEntriesAsync(targets)) return false;
                if (IsDead) return false;
                failures = RemoveAbove(remainingCount);
            }
            finally
            {
                check.Release();
            }
            Throw(failures);
            return true;
        }

        private IEnumerable<PageStack> StacksWith(IEnumerable<IComposablePresenter> pages)
            => new[] { this }.Concat(pages.SelectMany(PageCloseCheck.StacksOf));

        /// <summary>Removes the entries above <paramref name="remainingCount"/>: bookkeeping first, then top first
        /// each page's disposal and its view's removal; every failure is collected.</summary>
        private List<Exception> RemoveAbove(int remainingCount)
        {
            var failures = new List<Exception>();
            if (remainingCount >= _entries.Count) return failures;
            var removed = _entries.GetRange(remainingCount, _entries.Count - remainingCount);
            _entries.RemoveRange(remainingCount, removed.Count);
            _operations++;
            try
            {
                for (int i = removed.Count - 1; i >= 0; i--)
                    Discard(removed[i], failures);
            }
            finally
            {
                _operations--;
            }
            return failures;
        }

        // Disposes the page, then removes its view — unless the owner was disposed meanwhile (a page's teardown
        // disposed it): presenter disposal never tears views down.
        private void Discard(PageEntry entry, List<Exception> failures)
        {
            try { entry.Handle.Dispose(); }
            catch (Exception ex) { failures.Add(ex); }
            RemoveView(entry, failures);
        }

        // Each page's view is removed at most once, whichever path gets there first.
        private void RemoveView(PageEntry entry, List<Exception> failures)
        {
            if (entry.ViewRemoved || Owner.IsDisposed) return;
            entry.ViewRemoved = true;
            try { entry.RemoveView(); }
            catch (Exception ex) { failures.Add(ex); }
        }

        private static void Throw(List<Exception> failures)
        {
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        private void PushCore<TView>(PresenterBase<TView> page, TView view, Action<TView> removeView, Action initialize)
            where TView : IControlView
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (removeView == null) throw new ArgumentNullException(nameof(removeView));
            if (IsDead) throw new ObjectDisposedException(nameof(PageStack), "The page stack's owner is disposed.");
            if (IsBusy)
                throw new InvalidOperationException(
                    "The page stack is busy (a page is deciding whether it may close, or pages are being removed). " +
                    "Check IsBusy before creating the page's view.");

            PageEntry entry = null;
            _operations++;   // busy for the whole push, its compensation included (reentrancy is rejected)
            try
            {
                try
                {
                    Owner.AttachChildInternal(page, view,
                        () =>
                        {
                            ((IViewAttacher<TView>)page).AttachView(view);
                            initialize();
                        },
                        attachment =>
                        {
                            entry = new PageEntry(page, view, () => removeView(view), attachment) { Pushing = true };
                            _entries.Add(entry);
                            attachment.IsPage = true;
                            attachment.Released = OnReleased;
                        });
                }
                catch (Exception failure)
                {
                    if (entry == null) throw;   // rejected before the page was accepted: nothing to undo
                    entry.Pushing = false;
                    _entries.Remove(entry);
                    var removal = new List<Exception>();
                    RemoveView(entry, removal);
                    if (removal.Count > 0) throw new AggregateException(failure, removal[0]);
                    throw;
                }

                // The page was released while it was being pushed (it disposed itself in OnInitialize, ...) and
                // the push did not fail: it is not on the stack, so its view goes too.
                entry.Pushing = false;
                if (!_entries.Contains(entry))
                {
                    var removal = new List<Exception>();
                    RemoveView(entry, removal);
                    Throw(removal);
                }
            }
            finally
            {
                if (entry != null) entry.Pushing = false;
                _operations--;
            }
        }

        // A page left the owner. Pops and failed pushes have already dropped their entries (not found here);
        // owner teardown removes no views; anything else is an external release (the page's control was
        // disposed, or the presenter was disposed directly): that page and every page above it leave the stack
        // without being asked, and their views are removed top first — the released page's too, so the
        // breadcrumb stays in step. Failures propagate to whoever disposed the page.
        private void OnReleased(ChildAttachment attachment, ChildReleaseKind kind)
        {
            var entry = Find(attachment);
            if (entry == null) return;
            if (kind == ChildReleaseKind.OwnerTeardown)
            {
                _dead = true;
                _entries.Remove(entry);
                return;
            }
            if (entry.Pushing)
            {
                _entries.Remove(entry);   // the Push that is still running removes the view
                return;
            }

            int index = _entries.IndexOf(entry);
            var above = _entries.GetRange(index + 1, _entries.Count - index - 1);
            _entries.RemoveRange(index, _entries.Count - index);
            var failures = new List<Exception>();
            _operations++;
            try
            {
                for (int i = above.Count - 1; i >= 0; i--)
                    Discard(above[i], failures);
                RemoveView(entry, failures);
            }
            finally
            {
                _operations--;
            }
            Throw(failures);
        }

        private PageEntry Find(ChildAttachment attachment)
        {
            foreach (var entry in _entries)
                if (ReferenceEquals(entry.Handle, attachment)) return entry;
            return null;
        }

        private static Task<bool> Completed(bool value) => Task.FromResult(value);
    }

    /// <summary>One page on a <see cref="PageStack"/>.</summary>
    internal sealed class PageEntry
    {
        internal PageEntry(IComposablePresenter page, IView view, Action removeView, ChildAttachment handle)
        {
            Page = page;
            View = view;
            RemoveView = removeView;
            Handle = handle;
        }

        internal IComposablePresenter Page { get; }
        internal IView View { get; }
        internal Action RemoveView { get; }
        internal ChildAttachment Handle { get; }

        /// <summary>Accepted by a <c>Push</c> that has not returned yet.</summary>
        internal bool Pushing { get; set; }

        /// <summary>Its <c>removeView</c> has been called (it is never called twice).</summary>
        internal bool ViewRemoved { get; set; }
    }
}
