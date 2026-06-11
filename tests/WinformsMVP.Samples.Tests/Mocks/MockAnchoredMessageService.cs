using System.Collections.Generic;
using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Recording <see cref="IAnchoredMessageService"/> for tests. Because the
    /// <c>View.ShowToast(...)</c> view extensions resolve the service from the GLOBAL
    /// <see cref="ServiceLocator.Current"/>, tests that assert anchored feedback must install
    /// this mock globally and clean up:
    /// <code>
    /// [Collection("ServiceLocator")] // serialize: ServiceLocator.Current is process-global
    /// public class MyTests : System.IDisposable
    /// {
    ///     private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
    ///     public MyTests() => ServiceLocator.Configure(reg =>
    ///         reg.RegisterInstance&lt;IAnchoredMessageService&gt;(_anchored));
    ///     public void Dispose() => ServiceLocator.Reset();
    /// }
    /// </code>
    /// A test that forgets this resolves the REAL service and pops a real window.
    /// </summary>
    public class MockAnchoredMessageService : IAnchoredMessageService
    {
        public sealed class ToastCall
        {
            public string Text;
            public ToastType Type;
            public ToastOptions Options;
            public Point? Anchor;   // null = cursor-anchored overload
        }

        public sealed class MessageCall
        {
            public string Method;   // "ShowInfo", "ConfirmYesNo", ...
            public string Text;
            public string Caption;
            public Point? Anchor;   // null = cursor-anchored overload
        }

        public List<ToastCall> Toasts { get; } = new List<ToastCall>();
        public List<MessageCall> Messages { get; } = new List<MessageCall>();

        /// <summary>
        /// The choice the next confirmation returns. <c>Yes</c> makes <c>ConfirmYesNo</c> /
        /// <c>ConfirmOkCancel</c> return <c>true</c>. Default Yes.
        /// </summary>
        public ConfirmResult NextResult { get; set; } = ConfirmResult.Yes;

        public void ShowToast(string text, ToastType type, ToastOptions options = null)
            => Toasts.Add(new ToastCall { Text = text, Type = type, Options = options, Anchor = null });

        public void ShowToast(string text, ToastType type, Point anchor, ToastOptions options = null)
            => Toasts.Add(new ToastCall { Text = text, Type = type, Options = options, Anchor = anchor });

        public void ShowInfo(string text, string caption = "") => Record("ShowInfo", text, caption, null);
        public void ShowInfo(string text, Point anchor, string caption = "") => Record("ShowInfo", text, caption, anchor);
        public void ShowWarning(string text, string caption = "") => Record("ShowWarning", text, caption, null);
        public void ShowWarning(string text, Point anchor, string caption = "") => Record("ShowWarning", text, caption, anchor);
        public void ShowError(string text, string caption = "") => Record("ShowError", text, caption, null);
        public void ShowError(string text, Point anchor, string caption = "") => Record("ShowError", text, caption, anchor);

        public bool ConfirmYesNo(string text, string caption = "")
        { Record("ConfirmYesNo", text, caption, null); return NextResult == ConfirmResult.Yes; }
        public bool ConfirmYesNo(string text, Point anchor, string caption = "")
        { Record("ConfirmYesNo", text, caption, anchor); return NextResult == ConfirmResult.Yes; }
        public bool ConfirmOkCancel(string text, string caption = "")
        { Record("ConfirmOkCancel", text, caption, null); return NextResult == ConfirmResult.Yes; }
        public bool ConfirmOkCancel(string text, Point anchor, string caption = "")
        { Record("ConfirmOkCancel", text, caption, anchor); return NextResult == ConfirmResult.Yes; }
        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
        { Record("ConfirmYesNoCancel", text, caption, null); return NextResult; }
        public ConfirmResult ConfirmYesNoCancel(string text, Point anchor, string caption = "")
        { Record("ConfirmYesNoCancel", text, caption, anchor); return NextResult; }

        private void Record(string method, string text, string caption, Point? anchor)
            => Messages.Add(new MessageCall { Method = method, Text = text, Caption = caption, Anchor = anchor });

        public void Clear() { Toasts.Clear(); Messages.Clear(); }
    }
}
