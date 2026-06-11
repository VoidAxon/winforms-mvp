using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Generic convenience over the BCL non-generic <see cref="IServiceProvider"/>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>Resolves <typeparamref name="T"/>, or <c>null</c> if not registered.</summary>
        public static T GetService<T>(this IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            return (T)provider.GetService(typeof(T));
        }

        /// <summary>Resolves <typeparamref name="T"/>, throwing if it is not registered.</summary>
        public static T GetRequiredService<T>(this IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            object service = provider.GetService(typeof(T));
            if (service == null)
                throw new InvalidOperationException("No service registered for type " + typeof(T).FullName + ".");
            return (T)service;
        }
    }
}
