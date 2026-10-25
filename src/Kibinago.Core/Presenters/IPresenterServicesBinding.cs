using System;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Lets a framework host give a presenter its own service provider for the framework services
    /// (<c>Messages</c>, <c>Navigator</c>, <c>Logger</c>, ...) instead of
    /// <c>ServiceLocator.Current</c>. Bound before the view is attached.
    /// </summary>
    internal interface IPresenterServicesBinding
    {
        /// <exception cref="InvalidOperationException">The presenter already has a view, or has
        /// already resolved framework services (e.g. in its constructor).</exception>
        void BindServices(IServiceProvider services);
    }
}
