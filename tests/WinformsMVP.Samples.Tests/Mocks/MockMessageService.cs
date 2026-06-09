using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IMessageService for testing.
    /// Records all method calls for verification.
    /// </summary>
    public class MockMessageService : IMessageService
    {
        // Record all calls
        public List<MessageCall> Calls { get; } = new List<MessageCall>();

        // Convenience properties - Check if specific messages were displayed
        public bool InfoMessageShown => Calls.Exists(c => c.Type == MessageType.Info);
        public bool WarningMessageShown => Calls.Exists(c => c.Type == MessageType.Warning);
        public bool ErrorMessageShown => Calls.Exists(c => c.Type == MessageType.Error);
        public bool ConfirmDialogShown => Calls.Exists(c => c.Type == MessageType.Confirm);

        // Control return values
        public bool ConfirmYesNoResult { get; set; } = true;
        public bool ConfirmOkCancelResult { get; set; } = true;
        public ConfirmResult ConfirmYesNoCancelResult { get; set; } = ConfirmResult.Yes;

        // Implement interface methods
        public void ShowInfo(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Info, Message = text, Title = caption });
        }

        public void ShowWarning(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Warning, Message = text, Title = caption });
        }

        public void ShowError(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Error, Message = text, Title = caption });
        }

        public bool ConfirmYesNo(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Confirm, Message = text, Title = caption });
            return ConfirmYesNoResult;
        }

        public bool ConfirmOkCancel(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Confirm, Message = text, Title = caption });
            return ConfirmOkCancelResult;
        }

        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
        {
            Calls.Add(new MessageCall { Type = MessageType.Confirm, Message = text, Title = caption });
            return ConfirmYesNoCancelResult;
        }

        public void ShowToast(string text, ToastType type, int duration = 3000)
        {
            Calls.Add(new MessageCall { Type = MessageType.Toast, Message = text });
        }

        public void ShowToast(string text, ToastType type, ToastOptions options)
        {
            Calls.Add(new MessageCall { Type = MessageType.Toast, Message = text });
        }

        // Helper methods
        public void Clear()
        {
            Calls.Clear();
        }

        public MessageCall GetLastCall()
        {
            return Calls.Count > 0 ? Calls[Calls.Count - 1] : null;
        }

        public bool HasCall(MessageType type, string messageContains = null)
        {
            return Calls.Exists(c =>
                c.Type == type &&
                (messageContains == null || c.Message.Contains(messageContains)));
        }
    }

    /// <summary>
    /// Records a message service call
    /// </summary>
    public class MessageCall
    {
        public MessageType Type { get; set; }
        public string Message { get; set; }
        public string Title { get; set; }

        public override string ToString()
        {
            return $"{Type}: {Message} (Title: {Title})";
        }
    }

    public enum MessageType
    {
        Info,
        Warning,
        Error,
        Confirm,
        Toast
    }
}
