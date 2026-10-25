using System;
using Kibinago.Common;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Testing
{
    /// <summary>
    /// A non-modal window opened through <see cref="FakeWindowNavigator"/>. Close it the way the
    /// shown screen would, and the caller's <c>onClosed</c> receives the result exactly as
    /// production converts it.
    /// </summary>
    public class FakeWindow
    {
        private readonly Action<object, InteractionStatus> _deliver;
        private readonly Action<FakeWindow> _release;

        internal FakeWindow(object presenter, object parameters, bool hasParameters, object key, IWindowView owner,
            Action<object, InteractionStatus> deliver, Action<FakeWindow> release)
        {
            Presenter = presenter;
            Parameters = parameters;
            HasParameters = hasParameters;
            Key = key;
            Owner = owner;
            View = new FakeWindowView();
            _deliver = deliver;
            _release = release;
        }

        /// <summary>The presenter passed to <c>ShowWindow</c> (not attached or initialized — there is no Form).</summary>
        public object Presenter { get; }

        /// <summary>The parameters passed, or null for a parameterless overload.</summary>
        public object Parameters { get; }

        /// <summary>Whether a parameterized overload opened the window.</summary>
        public bool HasParameters { get; }

        /// <summary>The key returned by the caller's key selector, or null.</summary>
        public object Key { get; }

        /// <summary>The owner passed, or null.</summary>
        public IWindowView Owner { get; }

        /// <summary>The stand-in view returned to the caller (stable: activations return the same one).</summary>
        public IWindowView View { get; }

        /// <summary>False once the window was closed.</summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>How often a later <c>ShowWindow</c> with the same key activated this window.</summary>
        public int Activations { get; internal set; }

        /// <summary>Closes the window as the user would (X): the caller receives Cancel.</summary>
        public void Close() => CloseCore(null, InteractionStatus.Cancel);

        /// <summary>
        /// Closes the window as the presenter's <c>RequestClose(value, status)</c> would. A value
        /// of the wrong type for the caller's result type becomes <see cref="InteractionStatus.Error"/>.
        /// </summary>
        public void CloseWithResult<T>(T value, InteractionStatus status = InteractionStatus.Ok) => CloseCore(value, status);

        /// <summary>Closes the window with <see cref="InteractionStatus.Error"/>, carrying the message and exception.</summary>
        public void CloseWithError(string message, Exception exception = null)
            => CloseCore(new CloseError(message, exception), InteractionStatus.Error);

        // Production order: the window is gone and its key released, then the caller's onClosed
        // runs (it may reopen the same key), then the presenter is disposed.
        private void CloseCore(object result, InteractionStatus status)
        {
            if (!IsOpen) return;
            IsOpen = false;
            _release(this);
            try
            {
                _deliver(result, status);
            }
            finally
            {
                (Presenter as IDisposable)?.Dispose();
            }
        }

        internal sealed class CloseError
        {
            public CloseError(string message, Exception exception)
            {
                Message = message;
                Exception = exception;
            }

            public string Message { get; }
            public Exception Exception { get; }
        }

        private sealed class FakeWindowView : IWindowView
        {
            public IViewActionBinder ActionBinder => null;
        }
    }
}
