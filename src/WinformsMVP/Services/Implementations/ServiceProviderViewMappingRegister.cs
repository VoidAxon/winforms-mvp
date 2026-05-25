using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Decorates an <see cref="IViewMappingRegister"/> with an <see cref="IServiceProvider"/>
    /// fallback: when a View interface is not explicitly registered in the inner register,
    /// the decorator asks the <see cref="IServiceProvider"/> to resolve it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolution precedence inside <see cref="CreateInstance"/>:
    /// <list type="number">
    /// <item>If <c>inner.IsRegistered(viewInterface)</c> is true, delegate to
    /// <c>inner.CreateInstance(...)</c>. Explicit registrations always win, so users
    /// retain control even when a DI container is wired in.</item>
    /// <item>Otherwise, call <c>serviceProvider.GetService(viewInterface)</c>. Per the BCL
    /// contract, a missing service returns <c>null</c>.</item>
    /// <item>If the provider returns <c>null</c>, throw
    /// <see cref="KeyNotFoundException"/> — the same exception the inner register would
    /// have thrown, so callers see consistent behaviour.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="IsRegistered"/> and <see cref="GetViewImplementationType"/> reflect the
    /// inner register only. The <see cref="IServiceProvider"/> contract does not expose a
    /// side-effect-free "is this registered?" probe, and probing it via
    /// <see cref="IServiceProvider.GetService"/> would create instances as a side effect,
    /// which would be surprising. <see cref="CreateInstance"/> can still succeed via the
    /// fallback even when <see cref="IsRegistered"/> returns <c>false</c>.
    /// </para>
    /// </remarks>
    public class ServiceProviderViewMappingRegister : IViewMappingRegister
    {
        private readonly IViewMappingRegister _inner;
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderViewMappingRegister(
            IViewMappingRegister inner,
            IServiceProvider serviceProvider)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void Register<TViewInterface, TViewImplementation>(bool allowOverride = false)
            where TViewImplementation : Form, TViewInterface
        {
            _inner.Register<TViewInterface, TViewImplementation>(allowOverride);
        }

        public void Register<TViewInterface>(Func<TViewInterface> factory, bool allowOverride = false)
            where TViewInterface : class
        {
            _inner.Register(factory, allowOverride);
        }

        public Type GetViewImplementationType(Type viewInterfaceType)
            => _inner.GetViewImplementationType(viewInterfaceType);

        public bool IsRegistered(Type viewInterfaceType)
            => _inner.IsRegistered(viewInterfaceType);

        public object CreateInstance(Type viewInterfaceType)
        {
            if (viewInterfaceType == null)
                throw new ArgumentNullException(nameof(viewInterfaceType));

            // 1. Explicit registrations in the inner register always win.
            if (_inner.IsRegistered(viewInterfaceType))
                return _inner.CreateInstance(viewInterfaceType);

            // 2. Fall back to IServiceProvider. Per BCL contract, missing => null.
            var resolved = _serviceProvider.GetService(viewInterfaceType);
            if (resolved != null)
                return resolved;

            // 3. Same exception type the inner register raises, so the failure shape is consistent.
            throw new KeyNotFoundException(
                $"Implementation not found for View interface {viewInterfaceType.Name}. " +
                $"Verify it is registered in IViewMappingRegister or in the IServiceProvider.");
        }
    }
}
