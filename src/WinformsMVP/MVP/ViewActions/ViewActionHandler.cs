using System;

namespace WinformsMVP.MVP.ViewActions
{
    internal class ViewActionHandler : IViewActionHandler
    {
        private readonly Action _action;
        public ViewActionHandler(Action action) => _action = action;
        public void Execute(object payload) => _action?.Invoke();
    }

    /// <summary>
    /// Strongly-typed handler. <see cref="ViewActionDispatcher.Dispatch"/> validates the payload type
    /// against the registered <c>T</c> before invoking Execute, so a mismatch surfaces through the
    /// dispatcher's logger instead of being silently dropped here.
    /// </summary>
    public class ViewActionHandler<T> : IViewActionHandler
    {
        private static readonly bool AcceptsNull =
            !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

        private readonly Action<T> _action;
        public ViewActionHandler(Action<T> action) => _action = action;

        public void Execute(object payload)
        {
            // Pattern match `payload is T` returns false for null even when T is a reference or
            // Nullable<U> type, so handle the null case explicitly.
            if (payload == null)
            {
                if (AcceptsNull)
                {
                    _action?.Invoke(default(T));
                }
                return;
            }

            if (payload is T typedPayload)
            {
                _action?.Invoke(typedPayload);
            }
        }
    }
}
