using System;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Adapts a lambda <c>(context, next) =&gt; { ... }</c> into an
    /// <see cref="IDispatchMiddleware"/> so callers can write inline middleware
    /// without declaring a class.
    /// </summary>
    internal sealed class InlineMiddleware : IDispatchMiddleware
    {
        private readonly Action<DispatchContext, DispatchDelegate> _func;

        public InlineMiddleware(Action<DispatchContext, DispatchDelegate> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            _func = func;
        }

        public void Invoke(DispatchContext context, DispatchDelegate next)
        {
            _func(context, next);
        }
    }
}
