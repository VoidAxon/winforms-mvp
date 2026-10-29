using System.Collections.Generic;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.ActionLogDemo
{
    public interface IActionLogDemoView : IControlView
    {
        /// <summary>Replaces the list with these executions (oldest first).</summary>
        void ShowExecutions(IReadOnlyList<ActionExecution> executions);

        /// <summary>Adds one execution at the end of the list.</summary>
        void AddExecution(ActionExecution execution);
    }
}
