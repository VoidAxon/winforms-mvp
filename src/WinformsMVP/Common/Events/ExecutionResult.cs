using System;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// Execution result wrapper class that manages event subscriptions and final result
    /// Mainly used for subscription management when opening legacy forms (non-MVP forms)
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the form</typeparam>
    public class ExecutionResult<TResult> : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _isDisposed;

        /// <summary>
        /// The final result returned by the form (null indicates user cancelled)
        /// For modal forms, available immediately after ShowDialog returns
        /// For non-modal forms, available after calling SetResult
        /// </summary>
        public TResult Result { get; private set; }

        /// <summary>
        /// Triggered when non-modal form closes (carries the final result)
        /// </summary>
        public event EventHandler<ExecutionCompletedEventArgs<TResult>> Completed;

        /// <summary>
        /// Constructor: Modal form - pass result and Action (when additional cleanup logic is needed)
        /// </summary>
        /// <param name="result">The result returned by the form</param>
        /// <param name="disposeAction">Cleanup logic (unsubscribe events, release resources, etc.)</param>
        public ExecutionResult(TResult result, Action disposeAction)
        {
            Result = result;
            _disposeAction = disposeAction;
        }

        /// <summary>
        /// Constructor: Modal form - pass result and IDisposable (most common, directly pass Form)
        /// </summary>
        /// <param name="result">The result returned by the form</param>
        /// <param name="disposable">Resource to dispose (usually the Form)</param>
        public ExecutionResult(TResult result, IDisposable disposable)
        {
            Result = result;
            _disposeAction = () => disposable?.Dispose();
        }

        /// <summary>
        /// Constructor: Non-modal form - pass Action (result set later)
        /// </summary>
        /// <param name="disposeAction">Cleanup logic (unsubscribe events, release resources, etc.)</param>
        public ExecutionResult(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        /// <summary>
        /// Constructor: Non-modal form - pass IDisposable (result set later)
        /// </summary>
        /// <param name="disposable">Resource to dispose (usually the Form)</param>
        public ExecutionResult(IDisposable disposable)
        {
            _disposeAction = () => disposable?.Dispose();
        }

        /// <summary>
        /// Set result and trigger Completed event (for non-modal forms)
        /// </summary>
        /// <param name="result">The final result returned by the form</param>
        public void SetResult(TResult result)
        {
            Result = result;
            Completed?.Invoke(this, new ExecutionCompletedEventArgs<TResult>(result));
        }

        /// <summary>
        /// Release resources and execute cleanup logic
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _disposeAction?.Invoke();
                _isDisposed = true;
            }
        }
    }
}
