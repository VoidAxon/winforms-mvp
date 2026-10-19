using System;
using System.Collections.Generic;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Services
{
    /// <summary>
    /// The built-in, net40-safe service container: register into it via
    /// <see cref="IServiceRegistry"/>, resolve from it via <see cref="IServiceProvider"/>.
    /// Dictionary-backed; supports ready instances and lazy-singleton factories. No scopes —
    /// scoped/graph resolution is a real DI container's job (the M.E.DI bridge).
    /// </summary>
    /// <remarks>
    /// Disposing the provider disposes the singletons its factories created (newest first), the
    /// way M.E.DI does. Instances passed to <see cref="RegisterInstance{TService}"/> belong to the
    /// caller and are never disposed, even when a factory hands one back (an alias registration).
    /// </remarks>
    public sealed class DefaultServiceProvider : IServiceRegistry, IServiceProvider, IDisposable
    {
        private sealed class Entry
        {
            public object Instance;
            public Func<IServiceProvider, object> Factory;
            public bool HasInstance;
            public bool IsRegisteredInstance;
        }

        private readonly Dictionary<Type, Entry> _entries = new Dictionary<Type, Entry>();
        private readonly List<object> _created = new List<object>();   // factory results, creation order
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Registers a ready-made singleton instance for <typeparamref name="TService"/>.
        /// A <c>null</c> instance resolves as <c>null</c> — indistinguishable from "not registered" —
        /// and <see cref="ServiceProviderExtensions.ResolveRequired{T}"/> will throw for it.
        /// </summary>
        public void RegisterInstance<TService>(TService instance)
        {
            lock (_lock)
            {
                _entries[typeof(TService)] = new Entry { Instance = instance, HasInstance = true, IsRegisteredInstance = true };
            }
        }

        /// <summary>Non-generic <see cref="RegisterInstance{TService}"/>, for registration lists built at runtime.</summary>
        internal void Register(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            lock (_lock)
            {
                _entries[serviceType] = new Entry { Instance = instance, HasInstance = true, IsRegisteredInstance = true };
            }
        }

        /// <summary>Non-generic <see cref="RegisterFactory{TService}"/>, for registration lists built at runtime.</summary>
        internal void RegisterFactory(Type serviceType, Func<IServiceProvider, object> factory)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_lock)
            {
                _entries[serviceType] = new Entry { Factory = factory };
            }
        }

        /// <summary>
        /// Registers a lazy-singleton factory for <typeparamref name="TService"/>.
        /// Factories must not introduce resolution cycles: a cyclic factory graph overflows the stack
        /// (the lock is reentrant, so there is no deadlock — it recurses until the stack is exhausted).
        /// </summary>
        public void RegisterFactory<TService>(Func<IServiceProvider, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_lock)
            {
                _entries[typeof(TService)] = new Entry { Factory = sp => factory(sp) };
            }
        }

        public bool IsRegistered(Type serviceType)
        {
            lock (_lock) { return _entries.ContainsKey(serviceType); }
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DefaultServiceProvider));
                Entry entry;
                if (!_entries.TryGetValue(serviceType, out entry))
                    return null;                       // BCL contract: unknown -> null
                if (!entry.HasInstance)
                {
                    entry.Instance = entry.Factory(this);   // lazy, then cache (singleton)
                    entry.HasInstance = true;
                    entry.Factory = null;
                    if (entry.Instance != null) _created.Add(entry.Instance);
                }
                return entry.Instance;
            }
        }

        /// <summary>
        /// Disposes the <see cref="IDisposable"/> singletons created by factories, newest first, each
        /// once. Idempotent. Resolving afterwards throws <see cref="ObjectDisposedException"/>.
        /// </summary>
        public void Dispose()
        {
            var toDispose = new List<IDisposable>();
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                var owned = new HashSet<object>(DeepReflection.ReferenceEqualityComparer.Instance);
                foreach (var entry in _entries.Values)
                {
                    if (entry.IsRegisteredInstance && entry.Instance != null) owned.Add(entry.Instance);
                }

                var seen = new HashSet<object>(DeepReflection.ReferenceEqualityComparer.Instance);
                for (int i = _created.Count - 1; i >= 0; i--)
                {
                    var created = _created[i];
                    var disposable = created as IDisposable;
                    if (disposable != null && !owned.Contains(created) && seen.Add(created))
                        toDispose.Add(disposable);
                }
                _created.Clear();
            }

            // Outside the lock: a Dispose that touches the provider must not deadlock or recurse into it.
            foreach (var disposable in toDispose) disposable.Dispose();
        }
    }
}
