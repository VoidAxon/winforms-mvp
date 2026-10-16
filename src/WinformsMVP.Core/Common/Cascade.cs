using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Wires parent-&gt;child (and multi-parent) selection cascades in one declaration. Use for
    /// master/detail and N-level cascading selection (category -&gt; subcategory -&gt; product, etc.).
    /// </summary>
    public static class Cascade
    {
        /// <summary>
        /// When <paramref name="from"/> changes: clears <paramref name="target"/> (whose own
        /// <c>CurrentChanged</c> cascades the clear further down) then reloads <paramref name="target"/>'s
        /// list from the new parent value. <paramref name="initialSync"/> (default true) also reloads
        /// once at bind time, so a child bound after its parent already has a value is not left empty.
        /// </summary>
        /// <remarks>
        /// Synchronous, single-pass: the clear propagates depth-first before reload runs.
        /// <paramref name="reload"/> receives <c>from.Current</c> (may be <c>null</c> when the parent
        /// was cleared). <paramref name="reload"/> should not throw; if it does, the child is already
        /// cleared but not reloaded. Dispose the returned token to unsubscribe.
        /// </remarks>
        public static IDisposable Bind<TParent, TChild>(
            ISelectionStore<TParent> from,
            ISelectionStore<TChild> target,
            Action<TParent> reload,
            bool initialSync = true)
            where TParent : class
            where TChild : class
        {
            if (from == null) throw new ArgumentNullException("from");
            if (target == null) throw new ArgumentNullException("target");
            if (reload == null) throw new ArgumentNullException("reload");

            EventHandler handler = delegate
            {
                target.Select(null);    // clear self -> fires target.CurrentChanged -> downstream Bind clears next
                reload(from.Current);    // from.Current may be null (parent cleared) -> reload empties
            };
            from.CurrentChanged += handler;
            // initialSync: reload once now so a child bound after its parent already has a value is
            // not left empty. Each level self-syncs; order-independent. Clears nothing (no selection yet).
            if (initialSync) reload(from.Current);
            return new Unsubscriber(delegate { from.CurrentChanged -= handler; });
        }

        /// <summary>
        /// Multi-parent cascade: <paramref name="target"/> depends on BOTH <paramref name="a"/> and
        /// <paramref name="b"/>. Either parent changing clears <paramref name="target"/> and reloads
        /// with both current values. (<see cref="Bind{TParent,TChild}"/> is the single-parent case.)
        /// </summary>
        public static IDisposable Combine<TA, TB, TChild>(
            ISelectionStore<TA> a,
            ISelectionStore<TB> b,
            ISelectionStore<TChild> target,
            Action<TA, TB> reload,
            bool initialSync = true)
            where TA : class
            where TB : class
            where TChild : class
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (target == null) throw new ArgumentNullException("target");
            if (reload == null) throw new ArgumentNullException("reload");

            EventHandler handler = delegate
            {
                target.Select(null);
                reload(a.Current, b.Current);
            };
            a.CurrentChanged += handler;
            b.CurrentChanged += handler;
            if (initialSync) reload(a.Current, b.Current);
            return new Unsubscriber(delegate
            {
                a.CurrentChanged -= handler;
                b.CurrentChanged -= handler;
            });
        }

        private sealed class Unsubscriber : IDisposable
        {
            private Action _dispose;
            public Unsubscriber(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                if (d != null) d();
            }
        }
    }
}
