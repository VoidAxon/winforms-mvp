using System;
using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// Converts the boxed <c>(result, status)</c> a presenter pushes through <c>RequestClose</c>
    /// into the caller's <see cref="InteractionResult"/>. Shared by <c>WindowNavigator</c>
    /// and the Adopted <c>Connect</c> path so both report results identically.
    /// </summary>
    internal static class CloseResults
    {
        private const string DefaultErrorMessage = "Operation failed";

        internal static InteractionResult Build(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult.Ok();
                case InteractionStatus.Error: return InteractionResult.Error(ErrorMessage(result), result as Exception);
                default: return InteractionResult.Cancel();
            }
        }

        /// <summary>
        /// A result whose type does not match <typeparamref name="TResult"/> — or a missing result
        /// when <typeparamref name="TResult"/> is a non-nullable value type — is reported as
        /// <see cref="InteractionStatus.Error"/> rather than silently becoming <c>Ok(default)</c>.
        /// For an Error close, a <see cref="string"/> or <see cref="Exception"/> result supplies
        /// the error message.
        /// </summary>
        internal static InteractionResult<TResult> Build<TResult>(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok:
                    if (result is TResult typed)
                        return InteractionResult<TResult>.Ok(typed);
                    if (result == null && default(TResult) == null)
                        return InteractionResult<TResult>.Ok(default(TResult));
                    return InteractionResult<TResult>.Error(
                        result == null
                            ? $"The window closed with Ok but no result; the caller expected a {typeof(TResult).Name}."
                            : $"The window closed with a result of type {result.GetType().Name}; the caller expected a {typeof(TResult).Name}.");
                case InteractionStatus.Error:
                    return InteractionResult<TResult>.Error(ErrorMessage(result), result as Exception);
                default:
                    return InteractionResult<TResult>.Cancel();
            }
        }

        private static string ErrorMessage(object result)
        {
            if (result is string message && message.Length > 0) return message;
            if (result is Exception ex) return ex.Message;
            return DefaultErrorMessage;
        }
    }
}
