using System;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// No-op <see cref="IViewActionBinder"/> for use in mock views.
    /// </summary>
    /// <remarks>
    /// Mock views that don't exercise UI control wiring can use <see cref="Instance"/> instead of
    /// constructing a real <see cref="ViewActionBinder"/>. The real binder registers WinForms
    /// control strategies in its constructor, which is unnecessary overhead for unit tests.
    /// </remarks>
    public sealed class NullViewActionBinder : IViewActionBinder
    {
        public static readonly NullViewActionBinder Instance = new NullViewActionBinder();

        private NullViewActionBinder() { }

        public event EventHandler<ActionRequestEventArgs> ActionTriggered
        {
            add { }
            remove { }
        }

        public void Bind(ViewActionDispatcher dispatcher) { }

        public void Bind() { }

        public void Unbind() { }
    }
}
