using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.Testing
{
    /// <summary>One recorded <see cref="IToastService.Show"/> call.</summary>
    public sealed class ToastCall
    {
        /// <summary>Creates a record.</summary>
        public ToastCall(string text, ToastType type, int? duration)
        {
            Text = text;
            Type = type;
            Duration = duration;
        }

        /// <summary>The toast text.</summary>
        public string Text { get; }

        /// <summary>The toast type.</summary>
        public ToastType Type { get; }

        /// <summary>The duration as passed (null = the configured default).</summary>
        public int? Duration { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Type}: {Text}";
    }

    /// <summary><see cref="IToastService"/> fake that records every toast.</summary>
    public class FakeToastService : IToastService
    {
        private readonly object _gate = new object();
        private readonly List<ToastCall> _calls = new List<ToastCall>();

        /// <summary>Every toast so far, in order (a snapshot).</summary>
        public IReadOnlyList<ToastCall> Calls
        {
            get { lock (_gate) return _calls.ToArray(); }
        }

        /// <summary>Forgets the recorded toasts.</summary>
        public void ClearCalls()
        {
            lock (_gate) _calls.Clear();
        }

        /// <inheritdoc />
        public virtual void Show(string text, ToastType type, int? duration = null)
        {
            lock (_gate) _calls.Add(new ToastCall(text, type, duration));
        }
    }
}
