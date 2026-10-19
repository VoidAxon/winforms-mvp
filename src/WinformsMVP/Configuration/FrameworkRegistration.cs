using System;

namespace WinformsMVP.Configuration
{
    /// <summary>One framework service registration: a ready instance or a lazy singleton factory.</summary>
    internal sealed class FrameworkRegistration
    {
        private FrameworkRegistration(Type serviceType, object instance, Func<IServiceProvider, object> factory)
        {
            ServiceType = serviceType;
            Instance = instance;
            Factory = factory;
        }

        public Type ServiceType { get; }
        public object Instance { get; }
        public Func<IServiceProvider, object> Factory { get; }

        public static FrameworkRegistration ForInstance(Type serviceType, object instance)
            => new FrameworkRegistration(serviceType, instance, null);

        public static FrameworkRegistration ForFactory(Type serviceType, Func<IServiceProvider, object> factory)
            => new FrameworkRegistration(serviceType, null, factory);
    }
}
