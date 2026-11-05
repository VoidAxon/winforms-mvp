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
    /// <c>AttachChild</c> children; the pages below the top stay alive (the host hides them), popped pages are
    /// disposed. A jump back asks every page it discards (<see cref="IPageCloseGuard"/>) first, all or nothing,
    /// and the owner's window asks every page before it closes.
    /// </summary>
    /// <remarks>
    /// <para>Create it in the owner's constructor (<c>_pages = new PageStack(this, () =&gt; View.Pages)</c>; the owner's
    /// view interface exposes <c>IPageHost Pages { get; }</c>, a <c>PageHost</c> panel on the Form) and push from
    /// <c>OnInitialize</c> on: <c>_pages.Push(new OrderEditPresenter(deps), parameters, "受注 101")</c>. The host creates
    /// each page control from the view mapping (<c>register.Register&lt;IOrderEditView, OrderEditControl&gt;()</c> at
    /// startup), hides the page below and removes discarded pages; the view draws the breadcrumb from the host's
    /// titles.</para>
    /// <para>While a check or a removal is running the stack is busy (<see cref="IsBusy"/>): <c>Push</c> and the pops
    /// answer <c>false</c> and do nothing; requests are not queued, and callers need no <see cref="IsBusy"/> check.
    /// Busy blocks stack operations only, not editing in the pages.</para>
    /// <para>Results travel back through callbacks in a page's parameters (<c>OrderEditParams.Saved</c>). There is
    /// no "shown again" notification: a page that needs one gets a semantic event from its own view.</para>
    /// <para>Single-threaded (UI thread), like the presenters that use it.</para>
    /// </remarks>
    public sealed class PageStack
    {
        private readonly List<PageEntry> _entries = new List<PageEntry>();
        private readonly Func<IPageHost> _hostLookup;
        private IPageHost _host;
        private bool _dead;
        private int _operations;

        /// <summary>Creates an empty stack owned by <paramref name="owner"/>, showing its pages in a host.</summary>
        /// <param name="owner">The presenter whose view shows the pages — typically <c>this</c> in the owner's
        /// constructor. Construction touches no service, view or overridable code.</param>
        /// <param name="host">Returns where the pages are shown — typically <c>() =&gt; View.Pages</c> (the view
        /// interface exposes an <see cref="IPageHost"/>). Called once, on the first <c>Push</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="host"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="owner"/> is not a framework presenter.</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="owner"/> is disposed.</exception>
        public PageStack(IPresenter owner, Func<IPageHost> host)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (host == null) throw new ArgumentNullException(nameof(host));
            _hostLookup = host;
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
        /// the owner's window close holds the stack. <c>Push</c> and the pops answer <c>false</c> then (nothing done).
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
        /// Shows a new page on top: the host creates its view (mapped to <typeparamref name="TView"/> at startup), then
        /// <paramref name="page"/> is attached to it and initialized. The page is owned like an <c>AttachChild</c>
        /// child: it uses the owner's services unless it has its own, and is disposed with the owner.
        /// </summary>
        /// <typeparam name="TView">The page's view interface.</typeparam>
        /// <param name="page">The new page's presenter (<c>new</c>-ed by the owner).</param>
        /// <param name="title">The page's breadcrumb text (empty allowed).</param>
        /// <returns><c>true</c> when the page is on top. <c>false</c> when it is not: the stack was busy (a page is
        /// deciding whether it may close — nothing created, <paramref name="page"/> untouched), or the page ended during
        /// its own initialization (it disposed itself — its view removed — or the owner was disposed).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="title"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The owner or the page is disposed.</exception>
        /// <exception cref="InvalidOperationException">The owner has no view yet or exposes no host; the page is
        /// already attached; or the host returned an unusable view.</exception>
        /// <remarks>A rejection before the view exists changes nothing. A view the host made but the page could not
        /// take (wrong type, the page's services rejected) is removed through the host; <paramref name="page"/> is not
        /// disposed. A failure after the page was accepted (its <c>OnInitialize</c> throws) disposes it and removes its
        /// view once, then propagates.</remarks>
        public bool Push<TView>(ControlPresenterBase<TView> page, string title)
            where TView : class, IControlView
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return PushCore(page, title, () => ((IInitializable)page).Initialize());
        }

        /// <summary>
        /// Shows a new page that needs initialization parameters (its input and result callbacks). Same contract as
        /// <see cref="Push{TView}(ControlPresenterBase{TView}, string)"/>.
        /// </summary>
        /// <typeparam name="TView">The page's view interface.</typeparam>
        /// <typeparam name="TParam">The page's parameter type.</typeparam>
        /// <param name="page">The new page's presenter.</param>
        /// <param name="parameters">The page's parameters.</param>
        /// <param name="title">The page's breadcrumb text (empty allowed).</param>
        /// <returns><c>true</c> when the page is on top; <c>false</c> as for the overload without parameters.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="title"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The owner or the page is disposed.</exception>
        /// <exception cref="InvalidOperationException">The owner has no view yet or exposes no host; the page is
        /// already attached; or the host returned an unusable view.</exception>
        public bool Push<TView, TParam>(ControlPresenterBase<TView, TParam> page, TParam parameters, string title)
            where TView : class, IControlView
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return PushCore(page, title, () => ((IInitializable<TParam>)page).Initialize(parameters));
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

        // Each page's view is removed (through the host) at most once, whichever path gets there first; never after
        // the owner was disposed (presenter disposal does not tear views down).
        private void RemoveView(PageEntry entry, List<Exception> failures)
        {
            if (entry.ViewRemoved || Owner.IsDisposed) return;
            entry.ViewRemoved = true;
            try { _host.RemovePage(entry.View); }
            catch (Exception ex) { failures.Add(ex); }
        }

        private static void Throw(List<Exception> failures)
        {
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        // One transaction: busy → false; page-level checks (nothing created yet); reserve; the host makes the view;
        // a view made but not taken is removed again (the page stays undisposed); #81's attach path; a page that ended
        // during its initialization → false.
        private bool PushCore<TView>(PresenterBase<TView> page, string title, Action initialize)
            where TView : class, IControlView
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (IsBusy) return false;
            if (IsDead) throw new ObjectDisposedException(nameof(PageStack), "The page stack's owner is disposed.");
            if (!((IViewAttachable)Owner).IsViewAttached)
                throw new InvalidOperationException(
                    Owner.GetType().Name + " has no view attached yet; push pages from OnInitialize or later.");
            IComposablePresenter composable = page;
            IViewAttachable attachable = page;
            if (composable.IsDisposed) throw new ObjectDisposedException(page.GetType().Name);
            if (attachable.IsViewAttached || attachable.IsInitialized || composable.OwnerLink != null)
                throw new InvalidOperationException(
                    page.GetType().Name + " is already attached to a view; a presenter is pushed once.");

            PageEntry entry = null;
            _operations++;   // busy for the whole push — the host lookup and the compensation included (reentrancy is rejected)
            try
            {
                var host = Host();
                var created = host.AddPage(typeof(TView), title, composable.BoundServices ?? Owner.Services);
                if (created == null)
                    throw new InvalidOperationException(host.GetType().Name + ".AddPage returned no view for " + typeof(TView).Name + ".");
                if (ViewOwnership.IsClaimed(created))
                    throw new InvalidOperationException(   // not ours to remove: another presenter's live view
                        host.GetType().Name + ".AddPage returned a view another live presenter uses; a host returns a new view.");
                var view = created as TView;
                if (view == null)
                {
                    RemoveUnaccepted(host, created, new InvalidOperationException(
                        "The view made for " + typeof(TView).Name + " is a " + created.GetType().Name +
                        ", which does not implement it; check the view mapping."));
                }

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
                            entry = new PageEntry(page, view, attachment) { Pushing = true };
                            _entries.Add(entry);
                            attachment.IsPage = true;
                            attachment.Released = OnReleased;
                        });
                }
                catch (Exception failure)
                {
                    if (entry == null) RemoveUnaccepted(host, view, failure);   // made but not taken: the page stays undisposed
                    entry.Pushing = false;
                    _entries.Remove(entry);
                    var removal = new List<Exception>();
                    RemoveView(entry, removal);
                    if (removal.Count > 0) throw new AggregateException(failure, removal[0]);
                    throw;
                }

                // The page ended while it was being pushed (it disposed itself in OnInitialize; the owner was
                // disposed) without a failure: it is not on the stack — its view goes too, unless the owner went.
                entry.Pushing = false;
                if (!_entries.Contains(entry))
                {
                    var removal = new List<Exception>();
                    RemoveView(entry, removal);
                    Throw(removal);
                    return false;
                }
                return true;
            }
            finally
            {
                if (entry != null) entry.Pushing = false;
                _operations--;
            }
        }

        private IPageHost Host()
        {
            if (_host != null) return _host;
            _host = _hostLookup()
                ?? throw new InvalidOperationException(
                    Owner.GetType().Name + "'s page stack has no host: expose the PageHost control through the view " +
                    "interface (IPageHost Pages { get; }) and pass () => View.Pages to the PageStack.");
            return _host;
        }

        // A view the host made that the page did not take: remove it again, then report the failure (with the
        // removal's own failure, if any). Always throws.
        private static void RemoveUnaccepted(IPageHost host, IControlView view, Exception failure)
        {
            try { host.RemovePage(view); }
            catch (Exception removal) { throw new AggregateException(failure, removal); }
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        // A page left the owner. Pops and failed pushes have already dropped their entries (not found here);
        // owner teardown removes no views; anything else is an external release (the page's control was
        // disposed, or the presenter was disposed directly): that page and every page above it leave the stack
        // without being asked; the pages above are disposed and their views removed top first, then the
        // released page's view once its own teardown has finished (so the breadcrumb stays in step).
        // Failures propagate to whoever disposed the page.
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
                // The released page's own teardown has not run yet (it is being disposed right now): its view
                // goes after it, at the end of that disposal — presenters first, then their views.
                ((IPresenterResourceOwner)entry.Page).OwnUntilDisposed(Kibinago.Common.Disposable.Create(() =>
                {
                    var removal = new List<Exception>();
                    _operations++;   // busy here too: a breadcrumb redraw triggered by the removal cannot push or pop
                    try { RemoveView(entry, removal); }
                    finally { _operations--; }
                    Throw(removal);
                }));
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
        internal PageEntry(IComposablePresenter page, IControlView view, ChildAttachment handle)
        {
            Page = page;
            View = view;
            Handle = handle;
        }

        internal IComposablePresenter Page { get; }
        internal IControlView View { get; }
        internal ChildAttachment Handle { get; }

        /// <summary>Accepted by a <c>Push</c> that has not returned yet.</summary>
        internal bool Pushing { get; set; }

        /// <summary>Its view has been removed through the host (never twice).</summary>
        internal bool ViewRemoved { get; set; }
    }
}
