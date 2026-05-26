using System;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// EventArgs carrying the result for the
    /// <see cref="ExecutionResult{TResult}.Completed"/> event. Wrapping in
    /// EventArgs is required because <typeparamref name="TResult"/> is
    /// unconstrained and .NET Framework 4.0's <c>EventHandler&lt;T&gt;</c>
    /// requires <c>T : EventArgs</c>.
    /// </summary>
    public class ExecutionCompletedEventArgs<TResult> : EventArgs
    {
        public ExecutionCompletedEventArgs(TResult result)
        {
            Result = result;
        }

        public TResult Result { get; }
    }
}
