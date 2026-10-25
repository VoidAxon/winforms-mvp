using System;

namespace Kibinago.Common
{
    /// <summary>
    /// The outcome of an interaction without a value: <see cref="InteractionStatus.Ok"/>,
    /// <see cref="InteractionStatus.Cancel"/> or <see cref="InteractionStatus.Error"/> (with a message
    /// and/or exception). Use it for failable operations instead of exception-based control flow.
    /// </summary>
    public class InteractionResult
    {
        /// <summary>How the interaction ended.</summary>
        public InteractionStatus Status { get; }
        /// <summary>The error message when <see cref="Status"/> is <see cref="InteractionStatus.Error"/>; otherwise usually null.</summary>
        public string ErrorMessage { get; }
        /// <summary>The exception behind an error, if any; otherwise null.</summary>
        public Exception Exception { get; }

        /// <summary>True when <see cref="Status"/> is <see cref="InteractionStatus.Ok"/>.</summary>
        public bool IsOk => Status == InteractionStatus.Ok;
        /// <summary>True when <see cref="Status"/> is <see cref="InteractionStatus.Cancel"/>.</summary>
        public bool IsCancelled => Status == InteractionStatus.Cancel;
        /// <summary>True when <see cref="Status"/> is <see cref="InteractionStatus.Error"/>.</summary>
        public bool IsError => Status == InteractionStatus.Error;

        /// <summary>Creates a result; use the static factories (<see cref="Ok()"/>, <see cref="Cancel()"/>, <see cref="Error(string, Exception)"/>).</summary>
        /// <param name="status">How the interaction ended.</param>
        /// <param name="errorMessage">The error message, for an error.</param>
        /// <param name="exception">The exception behind an error, if any.</param>
        protected InteractionResult(InteractionStatus status, string errorMessage = null, Exception exception = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        /// <summary>Creates a successful result.</summary>
        public static InteractionResult Ok()
            => new InteractionResult(InteractionStatus.Ok);

        /// <summary>Creates a cancelled result.</summary>
        public static InteractionResult Cancel()
            => new InteractionResult(InteractionStatus.Cancel);

        /// <summary>Creates a failed result.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception behind the error, if any.</param>
        public static InteractionResult Error(string message, Exception exception = null)
            => new InteractionResult(InteractionStatus.Error, message, exception);
    }

    /// <summary>
    /// The outcome of an interaction that returns a value of type <typeparamref name="T"/> on success
    /// (e.g. a modal window shown with <c>ShowAsModal&lt;T&gt;()</c>).
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    public class InteractionResult<T> : InteractionResult
    {
        /// <summary>The result value when the interaction succeeded; <c>default</c> when cancelled or failed.</summary>
        public T Value { get; }

        /// <summary>Creates a result; use the static factories (<see cref="Ok(T)"/>, <see cref="Cancel()"/>, <see cref="Error(string, Exception)"/>).</summary>
        /// <param name="status">How the interaction ended.</param>
        /// <param name="value">The result value.</param>
        /// <param name="errorMessage">The error message, for an error.</param>
        /// <param name="exception">The exception behind an error, if any.</param>
        protected InteractionResult(InteractionStatus status, T value, string errorMessage = null, Exception exception = null)
            : base(status, errorMessage, exception)
        {
            Value = value;
        }

        /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
        /// <param name="value">The result value.</param>
        public static InteractionResult<T> Ok(T value)
            => new InteractionResult<T>(InteractionStatus.Ok, value);

        /// <summary>Creates a cancelled result (<see cref="Value"/> is <c>default</c>).</summary>
        public static new InteractionResult<T> Cancel()
            => new InteractionResult<T>(InteractionStatus.Cancel, default);

        /// <summary>Creates a failed result (<see cref="Value"/> is <c>default</c>).</summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception behind the error, if any.</param>
        public static new InteractionResult<T> Error(string message, Exception exception = null)
            => new InteractionResult<T>(InteractionStatus.Error, default, message, exception);
    }

}
