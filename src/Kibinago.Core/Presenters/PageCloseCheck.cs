using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Logging;

namespace Kibinago.Presenters
{
    /// <summary>
    /// One "may these pages be discarded?" decision — a jump, a <see cref="PageStack.CanCloseAllAsync"/> or a
    /// window close — and the reservation it holds. The operation that created it owns it and releases it.
    /// </summary>
    /// <remarks>
    /// Order: a stack is asked top first; for each page, the stacks its presenter tree owns (its own stacks in
    /// reverse registration order, then its plain children's trees in reverse attach order) come before the
    /// page's own guard. Each guard is awaited before the next is asked; the first refusal stops the check.
    /// Results are kept per presenter and per stack, so a forwarding guard that asks a stack this check already
    /// answered gets that answer again; asking one that is still being answered is a cycle and refuses.
    /// UI thread only.
    /// </remarks>
    internal sealed class PageCloseCheck
    {
        // The check whose guard is running on this logical flow: set while a guard (or the window's own close
        // decision) is called, so it flows into that code's awaits but not into unrelated handlers.
        private static readonly AsyncLocal<PageCloseCheck> CallingGuard = new AsyncLocal<PageCloseCheck>();

        private readonly IComposablePresenter _operationOwner;
        private readonly List<PageStack> _enrolled = new List<PageStack>();
        private readonly List<IComposablePresenter> _roots = new List<IComposablePresenter>();
        private readonly Dictionary<PageStack, bool?> _stackAnswers = new Dictionary<PageStack, bool?>();
        private readonly Dictionary<IComposablePresenter, bool?> _guardAnswers =
            new Dictionary<IComposablePresenter, bool?>(ReferenceComparer.Instance);
        private readonly List<IComposablePresenter> _pending = new List<IComposablePresenter>();
        private bool _released;

        /// <param name="operationOwner">The presenter whose disposal ends the check with a refusal.</param>
        internal PageCloseCheck(IComposablePresenter operationOwner)
        {
            _operationOwner = operationOwner;
        }

        private bool Stopped => _operationOwner.IsDisposed;

        // ---- Reservation ----

        /// <summary>Every stack in <paramref name="root"/>'s tree: its own, its children's (pages included).</summary>
        internal static IEnumerable<PageStack> StacksOf(IComposablePresenter root)
        {
            foreach (var stack in root.OwnedStacks) yield return stack;
            foreach (var attachment in root.Children)
            {
                var child = attachment.Child;
                if (child == null) continue;
                foreach (var stack in StacksOf(child)) yield return stack;
            }
        }

        /// <summary>The check whose traversal covers <paramref name="presenter"/> (it or an ancestor is a root).</summary>
        internal static PageCloseCheck FindReserving(IComposablePresenter presenter)
        {
            for (var p = presenter; p != null; p = p.OwnerLink?.Owner)
            {
                var check = p.ReservedBy;
                if (check != null && !check._released) return check;
            }
            return null;
        }

        /// <summary>
        /// Reserves <paramref name="stacks"/> (all or nothing) and marks <paramref name="roots"/> — the presenters
        /// whose whole trees the check traverses, so stacks that appear there during the check are enrolled.
        /// </summary>
        /// <returns><c>false</c> when one of the stacks is already busy; nothing is reserved then.</returns>
        internal bool TryReserve(IEnumerable<PageStack> stacks, IEnumerable<IComposablePresenter> roots)
        {
            var list = stacks.Distinct().ToList();
            if (list.Any(s => s.IsBusy)) return false;
            foreach (var stack in list) Enrol(stack);
            foreach (var root in roots)
            {
                if (root.ReservedBy != null) continue;
                root.ReservedBy = this;
                _roots.Add(root);
            }
            return true;
        }

        /// <summary>Adds a stack to this check's reservation (born in, or attached into, a traversed tree).</summary>
        internal void Enrol(PageStack stack)
        {
            if (_released || stack.Reservation != null) return;
            stack.Reservation = this;
            _enrolled.Add(stack);
        }

        /// <summary>Enrols every stack of <paramref name="presenter"/>'s tree.</summary>
        internal void EnrolTree(IComposablePresenter presenter)
        {
            foreach (var stack in StacksOf(presenter).ToList()) Enrol(stack);
        }

        /// <summary>Ends the reservation; a repeated call does nothing.</summary>
        internal void Release()
        {
            if (_released) return;
            _released = true;
            foreach (var stack in _enrolled)
                if (ReferenceEquals(stack.Reservation, this)) stack.Reservation = null;
            foreach (var root in _roots)
                if (ReferenceEquals(root.ReservedBy, this)) root.ReservedBy = null;
            _enrolled.Clear();
            _roots.Clear();
        }

        /// <summary>Marks <paramref name="stack"/> as being answered (a jump on it), so a forwarding call into it
        /// is a cycle.</summary>
        internal void MarkAsking(PageStack stack) => _stackAnswers[stack] = null;

        /// <summary>
        /// Whether a <see cref="PageStack.CanCloseAllAsync"/> on <paramref name="stack"/> (reserved by this check)
        /// comes from a guard this check is waiting for — it runs in that guard's code, and the stack's owner is
        /// the guard's presenter or one of its descendants — so it runs within this check instead of seeing the
        /// stack busy. Any other caller sees the stack busy.
        /// </summary>
        internal bool CanJoin(PageStack stack)
        {
            if (_released || _pending.Count == 0 || !ReferenceEquals(CallingGuard.Value, this)) return false;
            for (var p = stack.Owner; p != null; p = p.OwnerLink?.Owner)
                if (IsPending(p)) return true;
            return false;
        }

        private bool IsPending(IComposablePresenter presenter)
        {
            foreach (var p in _pending)
                if (ReferenceEquals(p, presenter)) return true;
            return false;
        }

        private void RemovePending(IComposablePresenter presenter)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_pending[i], presenter)) { _pending.RemoveAt(i); return; }
        }

        /// <summary>
        /// Calls <paramref name="decide"/> — a page's guard, or the window's own close decision — as code this check
        /// waits for: a forwarding call it makes (now or after its awaits) into a stack of
        /// <paramref name="presenter"/>'s tree joins this check.
        /// </summary>
        internal Task<bool> CallAsPending(IComposablePresenter presenter, Func<Task<bool>> decide, out Action end)
        {
            _pending.Add(presenter);
            end = () => RemovePending(presenter);
            var previous = CallingGuard.Value;
            CallingGuard.Value = this;
            try { return decide(); }
            finally { CallingGuard.Value = previous; }
        }

        // ---- Traversal ----

        /// <summary>Asks the given entries (top first) and their nested stacks.</summary>
        internal async Task<bool> AskEntriesAsync(IReadOnlyList<PageEntry> topFirst)
        {
            foreach (var entry in topFirst)
            {
                if (entry.Handle.IsReleased) continue;   // discarded meanwhile (its control was disposed)
                if (!await AskTreeAsync(entry.Page)) return false;
                if (Stopped) return false;
                if (entry.Handle.IsReleased) continue;
                if (!await AskGuardAsync(entry.Page)) return false;
                if (Stopped) return false;
            }
            return true;
        }

        /// <summary>Asks the stacks of <paramref name="presenter"/>'s tree (not its own guard).</summary>
        internal async Task<bool> AskTreeAsync(IComposablePresenter presenter)
        {
            var stacks = presenter.OwnedStacks.ToList();
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                if (!await AskStackAsync(stacks[i])) return false;
                if (Stopped) return false;
            }
            var children = presenter.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var attachment = children[i];
                var child = attachment.Child;
                if (child == null || attachment.IsPage) continue;   // pages are asked through their stack
                if (!await AskTreeAsync(child)) return false;
                if (Stopped) return false;
            }
            return true;
        }

        /// <summary>Asks every page of <paramref name="stack"/> (answered once per check).</summary>
        internal async Task<bool> AskStackAsync(PageStack stack)
        {
            if (_stackAnswers.TryGetValue(stack, out var known))
            {
                if (known.HasValue) return known.Value;
                LogCycle(stack.Owner);
                return false;
            }
            _stackAnswers[stack] = null;
            bool answer = false;
            try
            {
                answer = await AskEntriesAsync(stack.EntriesTopFirst());
                return answer;
            }
            finally
            {
                _stackAnswers[stack] = answer;
            }
        }

        private async Task<bool> AskGuardAsync(IComposablePresenter page)
        {
            var guard = page as IPageCloseGuard;
            if (guard == null) return true;
            if (_guardAnswers.TryGetValue(page, out var known))
            {
                if (known.HasValue) return known.Value;
                LogCycle(page);
                return false;
            }
            _guardAnswers[page] = null;
            bool answer = false;
            Action end = null;
            try
            {
                Task<bool> decision;
                try { decision = CallAsPending(page, guard.CanCloseAsync, out end); }
                catch (Exception ex)
                {
                    page.Logger.LogError(ex, "{Page}.CanCloseAsync threw; the page is not discarded", page.GetType().Name);
                    return false;
                }
                answer = await AwaitDecisionAsync(decision, page);
                return answer;
            }
            finally
            {
                end?.Invoke();
                _guardAnswers[page] = answer;
            }
        }

        private static async Task<bool> AwaitDecisionAsync(Task<bool> decision, IComposablePresenter page)
        {
            if (decision == null)
            {
                page.Logger.LogError("{Page}.CanCloseAsync returned no task; the page is not discarded", page.GetType().Name);
                return false;
            }
            try { return await decision; }
            catch (OperationCanceledException ex)
            {
                page.Logger.LogError(ex, "{Page}.CanCloseAsync was cancelled; the page is not discarded", page.GetType().Name);
                return false;
            }
            catch (Exception ex)
            {
                page.Logger.LogError(ex, "{Page}.CanCloseAsync failed; the page is not discarded", page.GetType().Name);
                return false;
            }
        }

        private static void LogCycle(IComposablePresenter presenter)
            => presenter.Logger.LogWarning(
                "A page close check for {Presenter} was asked again while it was still deciding (a cycle); refusing",
                presenter.GetType().Name);
    }

    /// <summary>Identity comparison: presenters that override <c>Equals</c> are still different presenters.</summary>
    internal sealed class ReferenceComparer : IEqualityComparer<IComposablePresenter>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();
        public bool Equals(IComposablePresenter x, IComposablePresenter y) => ReferenceEquals(x, y);
        public int GetHashCode(IComposablePresenter obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
