using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kibinago.Services;
using Kibinago.Views;

namespace Kibinago.Testing
{
    /// <summary>One recorded busy request (<c>ShowBusy</c>).</summary>
    public sealed class BusyCall
    {
        private int _ended;

        internal BusyCall(IView view, string message)
        {
            View = view;
            Message = message;
        }

        /// <summary>The busy view.</summary>
        public IView View { get; }

        /// <summary>The message as passed (<c>null</c> when none).</summary>
        public string Message { get; }

        /// <summary>Whether the request has ended (its result was disposed, or the presenter was disposed).</summary>
        public bool Ended => Volatile.Read(ref _ended) != 0;

        internal bool End() => Interlocked.Exchange(ref _ended, 1) == 0;

        /// <inheritdoc />
        public override string ToString() => (Ended ? "ended: " : "busy: ") + (Message ?? "(no message)");
    }

    /// <summary><see cref="IBusyIndicator"/> fake that records every busy request and whether it ended.</summary>
    public class FakeBusyIndicator : IBusyIndicator
    {
        private readonly object _gate = new object();
        private readonly List<BusyCall> _calls = new List<BusyCall>();

        /// <summary>Every request so far, in order (a snapshot).</summary>
        public IReadOnlyList<BusyCall> Calls
        {
            get { lock (_gate) return _calls.ToArray(); }
        }

        /// <summary>How many requests began.</summary>
        public int BeginCount
        {
            get { lock (_gate) return _calls.Count; }
        }

        /// <summary>Whether any request is still open.</summary>
        public bool IsBusy
        {
            get { lock (_gate) return _calls.Any(c => !c.Ended); }
        }

        /// <summary>Whether a request on <paramref name="view"/> is still open.</summary>
        /// <param name="view">The view.</param>
        /// <returns><c>true</c> while it is busy.</returns>
        public bool IsBusyFor(IView view)
        {
            lock (_gate) return _calls.Any(c => !c.Ended && ReferenceEquals(c.View, view));
        }

        /// <inheritdoc />
        public IDisposable Begin(IView view, string message)
        {
            var call = new BusyCall(view, message);
            lock (_gate) _calls.Add(call);
            return new Ending(call);
        }

        /// <summary>Forgets every recorded request.</summary>
        public void Clear()
        {
            lock (_gate) _calls.Clear();
        }

        private sealed class Ending : IDisposable
        {
            private readonly BusyCall _call;
            public Ending(BusyCall call) { _call = call; }
            public void Dispose() => _call.End();
        }
    }
}
