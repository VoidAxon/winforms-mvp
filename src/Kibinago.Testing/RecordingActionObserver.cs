using System.Collections.Generic;
using Kibinago.ViewActions;

namespace Kibinago.Testing
{
    /// <summary>
    /// An <see cref="IActionObserver"/> that records every finished action, for assertions
    /// (<c>fakes.Actions</c>): which actions ran, with which payload, how they ended.
    /// </summary>
    public sealed class RecordingActionObserver : IActionObserver
    {
        private readonly object _gate = new object();
        private readonly List<ActionExecution> _executions = new List<ActionExecution>();

        /// <summary>A snapshot of the recorded executions, in the order they finished.</summary>
        public IReadOnlyList<ActionExecution> Executions
        {
            get { lock (_gate) return _executions.ToArray(); }
        }

        /// <inheritdoc />
        public void OnExecuted(ActionExecution execution)
        {
            lock (_gate) _executions.Add(execution);
        }

        /// <summary>Forgets what was recorded so far.</summary>
        public void Clear()
        {
            lock (_gate) _executions.Clear();
        }
    }
}
