using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kibinago.ViewActions
{
    internal sealed class ViewActionHandler : IViewActionHandler
    {
        private readonly Action _action;
        public ViewActionHandler(Action action) => _action = action;

        public bool IsAsync => false;

        public Task Execute(object payload, CancellationToken cancellationToken)
        {
            _action();
            return null;
        }
    }

    internal sealed class ViewActionHandler<T> : IViewActionHandler
    {
        private readonly Action<T> _action;
        public ViewActionHandler(Action<T> action) => _action = action;

        public bool IsAsync => false;

        public Task Execute(object payload, CancellationToken cancellationToken)
        {
            if (Payload<T>.TryConvert(payload, out var value))
                _action(value);
            return null;
        }
    }

    internal sealed class AsyncViewActionHandler : IViewActionHandler
    {
        private readonly Func<CancellationToken, Task> _action;
        public AsyncViewActionHandler(Func<CancellationToken, Task> action) => _action = action;

        public bool IsAsync => true;

        public Task Execute(object payload, CancellationToken cancellationToken)
            => _action(cancellationToken) ?? Task.CompletedTask;
    }

    internal sealed class AsyncViewActionHandler<T> : IViewActionHandler
    {
        private readonly Func<T, CancellationToken, Task> _action;
        public AsyncViewActionHandler(Func<T, CancellationToken, Task> action) => _action = action;

        public bool IsAsync => true;

        public Task Execute(object payload, CancellationToken cancellationToken)
            => Payload<T>.TryConvert(payload, out var value)
                ? _action(value, cancellationToken) ?? Task.CompletedTask
                : Task.CompletedTask;
    }

    internal static class Payload<T>
    {
        private static readonly bool AcceptsNull =
            !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

        public static bool TryConvert(object payload, out T value)
        {
            // Pattern match `payload is T` returns false for null even when T is a reference or
            // Nullable<U> type, so handle the null case explicitly.
            if (payload == null)
            {
                value = default(T);
                return AcceptsNull;
            }

            if (payload is T typed)
            {
                value = typed;
                return true;
            }

            value = default(T);
            return false;
        }
    }
}
