using System;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// ExecutionRequest event args - Single parameter version
    /// View uses this event to request Presenter to execute specific logic, and gets result through callback or Executor
    /// </summary>
    public class ExecutionRequestEventArgs<T, TResult> : EventArgs
    {
        public T Param { get; }
        public Action<TResult> Callback { get; }
        public Func<T, ExecutionResult<TResult>> Executor { get; }

        /// <summary>
        /// Constructor - Callback version (backward compatible)
        /// </summary>
        public ExecutionRequestEventArgs(T param, Action<TResult> callback)
        {
            Param = param;
            Callback = callback;
        }

        /// <summary>
        /// Constructor - Executor version (for legacy form integration)
        /// </summary>
        public ExecutionRequestEventArgs(Func<T, ExecutionResult<TResult>> executor, T param)
        {
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Param = param;
        }
    }

    /// <summary>
    /// ExecutionRequest event args - Two parameter version
    /// </summary>
    public class ExecutionRequestEventArgs<T1, T2, TResult> : EventArgs
    {
        public T1 Param1 { get; }
        public T2 Param2 { get; }
        public Action<TResult> Callback { get; }
        public Func<T1, T2, ExecutionResult<TResult>> Executor { get; }

        /// <summary>
        /// Constructor - Callback version (backward compatible)
        /// </summary>
        public ExecutionRequestEventArgs(T1 param1, T2 param2, Action<TResult> callback)
        {
            Param1 = param1;
            Param2 = param2;
            Callback = callback;
        }

        /// <summary>
        /// Constructor - Executor version (for legacy form integration)
        /// </summary>
        public ExecutionRequestEventArgs(Func<T1, T2, ExecutionResult<TResult>> executor, T1 param1, T2 param2)
        {
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Param1 = param1;
            Param2 = param2;
        }
    }

    /// <summary>
    /// ExecutionRequest event args - Three parameter version
    /// </summary>
    public class ExecutionRequestEventArgs<T1, T2, T3, TResult> : EventArgs
    {
        public T1 Param1 { get; }
        public T2 Param2 { get; }
        public T3 Param3 { get; }
        public Action<TResult> Callback { get; }
        public Func<T1, T2, T3, ExecutionResult<TResult>> Executor { get; }

        /// <summary>
        /// Constructor - Callback version (backward compatible)
        /// </summary>
        public ExecutionRequestEventArgs(T1 param1, T2 param2, T3 param3, Action<TResult> callback)
        {
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
            Callback = callback;
        }

        /// <summary>
        /// Constructor - Executor version (for legacy form integration)
        /// </summary>
        public ExecutionRequestEventArgs(Func<T1, T2, T3, ExecutionResult<TResult>> executor, T1 param1, T2 param2, T3 param3)
        {
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Param1 = param1;
            Param2 = param2;
            Param3 = param3;
        }
    }
}
