using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>Records presenter toasts instead of showing them.</summary>
    public class MockToastService : IToastService
    {
        public List<ToastCall> Calls { get; } = new List<ToastCall>();

        public void Show(string text, ToastType type, int? duration = null)
            => Calls.Add(new ToastCall { Text = text, Type = type, Duration = duration });

        public void Clear() => Calls.Clear();
    }

    public class ToastCall
    {
        public string Text { get; set; }
        public ToastType Type { get; set; }
        public int? Duration { get; set; }
        public override string ToString() => $"[{Type}] {Text}";
    }
}
