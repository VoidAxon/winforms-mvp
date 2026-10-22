using System;
using System.Collections.Generic;

namespace WinformsMVP.MVP.Presenters
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

        public void Dispose()
        {
            var child = Unlink();
            child?.Dispose();
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
