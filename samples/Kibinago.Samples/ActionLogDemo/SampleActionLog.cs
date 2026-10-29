using System;
using System.Collections.Generic;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ActionLogDemo
{
    /// <summary>
    /// The application's operation log: registered once at startup (<c>mvp.Actions.Observe(log)</c>,
    /// Program.cs), it hears about every action any presenter runs — the shell's, every demo's.
    /// A real application would write each execution to a file or a log server; this one keeps the
    /// latest ones in memory so the demo can show them.
    /// </summary>
    public sealed class SampleActionLog : IActionObserver
    {
        private const int Capacity = 200;
        private readonly List<ActionExecution> _executions = new List<ActionExecution>();

        /// <summary>Raised after each execution is recorded (on the thread that ran the action: the UI thread).</summary>
        public event EventHandler<ActionExecution> Recorded;

        public IReadOnlyList<ActionExecution> Executions => _executions.ToArray();

        public void OnExecuted(ActionExecution execution)
        {
            _executions.Add(execution);
            if (_executions.Count > Capacity) _executions.RemoveAt(0);
            Recorded?.Invoke(this, execution);
        }

        public void Clear() => _executions.Clear();
    }
}
