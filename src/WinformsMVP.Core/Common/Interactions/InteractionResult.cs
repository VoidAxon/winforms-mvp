using System;

namespace WinformsMVP.Common
{
    public class InteractionResult
    {
        public InteractionStatus Status { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        public bool IsOk => Status == InteractionStatus.Ok;
        public bool IsCancelled => Status == InteractionStatus.Cancel;
        public bool IsError => Status == InteractionStatus.Error;

        protected InteractionResult(InteractionStatus status, string errorMessage = null, Exception exception = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static InteractionResult Ok()
            => new InteractionResult(InteractionStatus.Ok);

        public static InteractionResult Cancel()
            => new InteractionResult(InteractionStatus.Cancel);

        public static InteractionResult Error(string message, Exception exception = null)
            => new InteractionResult(InteractionStatus.Error, message, exception);
    }

    public class InteractionResult<T> : InteractionResult
    {
        public T Value { get; }

        protected InteractionResult(InteractionStatus status, T value, string errorMessage = null, Exception exception = null)
            : base(status, errorMessage, exception)
        {
            Value = value;
        }

        public static InteractionResult<T> Ok(T value)
            => new InteractionResult<T>(InteractionStatus.Ok, value);

        public static new InteractionResult<T> Cancel()
            => new InteractionResult<T>(InteractionStatus.Cancel, default);

        public static new InteractionResult<T> Error(string message, Exception exception = null)
            => new InteractionResult<T>(InteractionStatus.Error, default, message, exception);
    }

}
