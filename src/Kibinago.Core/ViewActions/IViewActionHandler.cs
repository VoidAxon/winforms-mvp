using System.Threading;
using System.Threading.Tasks;

namespace Kibinago.ViewActions
{
    internal interface IViewActionHandler
    {
        /// <summary>True for handlers registered with a Task-returning delegate.</summary>
        bool IsAsync { get; }

        /// <summary>
        /// Runs the handler. Synchronous handlers return null; asynchronous handlers return the
        /// task that completes when the handler finishes. A payload of the wrong type is ignored
        /// (the dispatcher validates payloads before calling).
        /// </summary>
        Task Execute(object payload, CancellationToken cancellationToken);
    }
}
