using System.Collections.Generic;
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
        }

        public sealed class MessageCall
        {
            public string Text;
            public string Caption;
            public MessageButtons Buttons;
            public MessageIcon Icon;
        }

        public List<ToastCall> Toasts { get; } = new List<ToastCall>();
        public List<MessageCall> Messages { get; } = new List<MessageCall>();

        /// <summary>The result the next ShowMessage call returns. Default Yes.</summary>
        public ConfirmResult NextResult { get; set; } = ConfirmResult.Yes;

        public void ShowToast(string text, ToastType type, ToastOptions options)
            => Toasts.Add(new ToastCall { Text = text, Type = type, Options = options });

        public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
        {
            Messages.Add(new MessageCall { Text = text, Caption = caption, Buttons = buttons, Icon = icon });
            return NextResult;
        }

        public void Clear() { Toasts.Clear(); Messages.Clear(); }
    }
}
