using System;
using System.Threading.Tasks;

namespace Kibinago.ViewActions
{
    /// <summary>Adapts a lambda passed to <see cref="ViewActionDispatcher.Use(Func{DispatchContext, DispatchDelegate, Task})"/>.</summary>
    internal sealed class InlineMiddleware : IDispatchMiddleware
    {
        private readonly Func<DispatchContext, DispatchDelegate, Task> _func;

        public InlineMiddleware(Func<DispatchContext, DispatchDelegate, Task> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            _func = func;
        }

        public Task InvokeAsync(DispatchContext context, DispatchDelegate next)
        {
            return _func(context, next);
        }
    }
}
