using System;
using System.Collections.Generic;

namespace WinformsMVP.Services
{
    /// <summary>
    /// The built-in, net40-safe service container: register into it via
    /// <see cref="IServiceRegistry"/>, resolve from it via <see cref="IServiceProvider"/>.
    /// Dictionary-backed; supports ready instances and lazy-singleton factories. No scopes —
    /// scoped/graph resolution is a real DI container's job (the M.E.DI bridge).
    /// </summary>
    public sealed class DefaultServiceProvider : IServiceRegistry, IServiceProvider
    {
        private sealed class Entry
        {
            public object Instance;
            public Func<IServiceProvider, object> Factory;
            public bool HasInstance;
        }

        private readonly Dictionary<Type, Entry> _entries = new Dictionary<Type, Entry>();
        private readonly object _lock = new object();

        /// <summary>
        /// Registers a ready-made singleton instance for <typeparamref name="TService"/>.
        /// A <c>null</c> instance resolves as <c>null</c> — indistinguishable from "not registered" —
        /// and <see cref="ServiceProviderExtensions.ResolveRequired{T}"/> will throw for it.
        /// </summary>
        public void RegisterInstance<TService>(TService instance)
        {
            lock (_lock)
            {
                _entries[typeof(TService)] = new Entry { Instance = instance, HasInstance = true };
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
                Entry entry;
                if (!_entries.TryGetValue(serviceType, out entry))
                    return null;                       // BCL contract: unknown -> null
                if (!entry.HasInstance)
                {
                    entry.Instance = entry.Factory(this);   // lazy, then cache (singleton)
                    entry.HasInstance = true;
                    entry.Factory = null;
                }
                return entry.Instance;
            }
        }
    }
}
