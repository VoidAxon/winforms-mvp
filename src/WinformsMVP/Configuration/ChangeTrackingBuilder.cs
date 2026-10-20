using System;
using WinformsMVP.Common;

namespace WinformsMVP.Configuration
{
    /// <summary>
    /// Change-tracking engines and per-type policies (<c>mvp.ChangeTracking</c>), used by a
    /// presenter's <c>TrackChanges(value)</c>. Trackers created with <c>new ChangeTracker&lt;T&gt;</c>
    /// use only what their constructor is given.
    /// </summary>
    public sealed class ChangeTrackingBuilder
    {
        private readonly WinformsMvpBuilder _owner;

        internal ChangeTrackingBuilder(WinformsMvpBuilder owner) { _owner = owner; }

        /// <summary>Deep-copies snapshots with <paramref name="cloner"/> (e.g. an adapter over a deep-clone library).</summary>
        public ChangeTrackingBuilder UseCloner(IObjectCloner cloner)
        {
            _owner.SetInstance(typeof(IObjectCloner), cloner ?? throw new ArgumentNullException(nameof(cloner)));
            return this;
        }

        /// <summary>Like <see cref="UseCloner(IObjectCloner)"/>, built on first use with access to other services.</summary>
        public ChangeTrackingBuilder UseCloner(Func<IServiceProvider, IObjectCloner> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IObjectCloner), sp => factory(sp));
            return this;
        }

        /// <summary>Compares content with <paramref name="comparer"/> (e.g. an adapter over a compare library).</summary>
        public ChangeTrackingBuilder UseComparer(IObjectContentComparer comparer)
        {
            _owner.SetInstance(typeof(IObjectContentComparer), comparer ?? throw new ArgumentNullException(nameof(comparer)));
            return this;
        }

        /// <summary>Like <see cref="UseComparer(IObjectContentComparer)"/>, built on first use with access to other services.</summary>
        public ChangeTrackingBuilder UseComparer(Func<IServiceProvider, IObjectContentComparer> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IObjectContentComparer), sp => factory(sp));
            return this;
        }

        /// <summary>Uses <paramref name="policy"/> for exactly <typeparamref name="T"/> (no base or derived types).</summary>
        public ChangeTrackingBuilder For<T>(ChangeTrackingPolicy<T> policy) where T : class
        {
            _owner.SetInstance(typeof(ChangeTrackingPolicy<T>), policy ?? throw new ArgumentNullException(nameof(policy)));
            return this;
        }
    }
}
