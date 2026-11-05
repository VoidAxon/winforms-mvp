using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>What a presenter needs from another presenter to own it as a child.</summary>
    internal interface IComposablePresenter : IDisposable
    {
        bool IsDisposed { get; }

        /// <summary>The attachment that owns this presenter, or null.</summary>
        ChildAttachment OwnerLink { get; set; }

        /// <summary>Stops dispatching here and in all descendants (their cleanup runs later).</summary>
        void DeactivateTree();

        /// <summary>Gives an unbound presenter its owner's services; keeps explicitly bound ones;
        /// rejects services fixed elsewhere (resolved in the constructor).</summary>
        void InheritServices(IServiceProvider ownerServices);

        /// <summary>The children this presenter owns, in attach order (a snapshot).</summary>
        IReadOnlyList<ChildAttachment> Children { get; }

        /// <summary>
        /// The body of <c>AttachChild</c>: preflight checks (nothing changes when one throws), ownership
        /// transfer, then <paramref name="beforeInitialize"/>, then <paramref name="attachAndInitialize"/>.
        /// Every failure after the transfer releases the child (disposes it) and propagates.
        /// </summary>
        ChildAttachment AttachChildInternal(IComposablePresenter child, IView view, Action attachAndInitialize,
            Action<ChildAttachment> beforeInitialize);
    }

    /// <summary>Why a <see cref="ChildAttachment"/> released its child.</summary>
    internal enum ChildReleaseKind
    {
        /// <summary>The handle was disposed (or the attach failed after the ownership transfer).</summary>
        Explicit,
        /// <summary>The child was disposed directly, or its control was disposed.</summary>
        External,
        /// <summary>The owner is being disposed and releases its children.</summary>
        OwnerTeardown,
    }

    /// <summary>
    /// One child owned by a presenter; also the handle <c>AttachChild</c> returns. Disposing it releases
    /// (disposes) the child for good; disposing it again, or after the owner released the child, does
    /// nothing.
    /// </summary>
    internal sealed class ChildAttachment : IDisposable
    {
        private readonly List<ChildAttachment> _registry;
        private IComposablePresenter _child;

        internal ChildAttachment(List<ChildAttachment> registry, IComposablePresenter child, object view)
        {
            _registry = registry;
            _child = child;
            View = view;
        }

        internal object View { get; private set; }

        internal IComposablePresenter Child => _child;

        internal bool IsReleased => _child == null;

        /// <summary>Called once when the child is released, after the bookkeeping and before the child's
        /// teardown continues. A failure is reported to whoever released the child; the child is still
        /// disposed.</summary>
        internal Action<ChildAttachment, ChildReleaseKind> Released { get; set; }

        public void Dispose() => ReleaseAndDispose(ChildReleaseKind.Explicit);

        /// <summary>Releases the child and disposes it; failures of the notification and of the disposal
        /// are both reported (one as is, several as an <see cref="AggregateException"/>).</summary>
        internal void ReleaseAndDispose(ChildReleaseKind kind)
        {
            var failures = new List<Exception>();
            var child = Release(kind, failures);
            if (child != null)
            {
                try { child.Dispose(); }
                catch (Exception ex) { failures.Add(ex); }
            }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        /// <summary><see cref="Unlink"/>, then the <see cref="Released"/> notification (its failure goes to
        /// <paramref name="failures"/>). Returns the child, or null when already released.</summary>
        internal IComposablePresenter Release(ChildReleaseKind kind, List<Exception> failures)
        {
            var child = Unlink();
            if (child == null) return null;
            var released = Released;
            Released = null;
            if (released != null)
            {
                try { released(this, kind); }
                catch (Exception ex) { failures.Add(ex); }
            }
            return child;
        }

        /// <summary>Removes this entry and forgets the child (before any of its teardown runs);
        /// returns the child, or null when already released.</summary>
        internal IComposablePresenter Unlink()
        {
            var child = _child;
            if (child == null) return null;
            _child = null;
            View = null;
            _registry.Remove(this);
            if (ReferenceEquals(child.OwnerLink, this)) child.OwnerLink = null;
            return child;
        }
    }
}
