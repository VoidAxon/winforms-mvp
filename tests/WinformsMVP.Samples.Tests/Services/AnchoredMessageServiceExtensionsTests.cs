using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class AnchoredMessageServiceExtensionsTests
    {
        private sealed class RecordingService : IAnchoredMessageService
        {
            public readonly List<object[]> Toasts = new List<object[]>();
            public readonly List<object[]> Messages = new List<object[]>();
            public ConfirmResult NextResult = ConfirmResult.Yes;

            public void ShowToast(string text, ToastType type, ToastOptions options)
                => Toasts.Add(new object[] { text, type, options });

            public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
            {
                Messages.Add(new object[] { text, caption, buttons, icon });
                return NextResult;
            }
        }

        [Fact]
        public void ShowToast_TwoArg_ForwardsWithNullOptions()
        {
            var s = new RecordingService();
            s.ShowToast("hi", ToastType.Success);
            Assert.Single(s.Toasts);
            Assert.Equal("hi", s.Toasts[0][0]);
            Assert.Equal(ToastType.Success, s.Toasts[0][1]);
            Assert.Null(s.Toasts[0][2]);
        }

        [Theory]
        [InlineData("ShowInfo", MessageIcon.Information)]
        [InlineData("ShowWarning", MessageIcon.Warning)]
        [InlineData("ShowError", MessageIcon.Error)]
        public void ShowXxx_MapsToOkButtonAndIcon(string method, MessageIcon expectedIcon)
        {
            var s = new RecordingService();
            if (method == "ShowInfo") s.ShowInfo("t", "c");
            else if (method == "ShowWarning") s.ShowWarning("t", "c");
            else s.ShowError("t", "c");

            Assert.Single(s.Messages);
            Assert.Equal("t", s.Messages[0][0]);
            Assert.Equal("c", s.Messages[0][1]);
            Assert.Equal(MessageButtons.Ok, s.Messages[0][2]);
            Assert.Equal(expectedIcon, s.Messages[0][3]);
        }

        [Fact]
        public void ConfirmYesNo_UsesYesNoQuestion_AndMapsYesToTrue()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Yes };
            Assert.True(s.ConfirmYesNo("sure?", "cap"));
            Assert.Equal(MessageButtons.YesNo, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);

            s.NextResult = ConfirmResult.No;
            Assert.False(s.ConfirmYesNo("sure?", "cap"));
        }

        [Fact]
        public void ConfirmOkCancel_UsesOkCancelQuestion_AndMapsYesToTrue()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Yes };
            Assert.True(s.ConfirmOkCancel("go?", "cap"));
            Assert.Equal(MessageButtons.OkCancel, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);

            s.NextResult = ConfirmResult.Cancel;
            Assert.False(s.ConfirmOkCancel("go?", "cap"));
        }

        [Fact]
        public void ConfirmYesNoCancel_UsesYesNoCancelQuestion_AndReturnsRawResult()
        {
            var s = new RecordingService { NextResult = ConfirmResult.Cancel };
            Assert.Equal(ConfirmResult.Cancel, s.ConfirmYesNoCancel("pick", "cap"));
            Assert.Equal(MessageButtons.YesNoCancel, s.Messages[0][2]);
            Assert.Equal(MessageIcon.Question, s.Messages[0][3]);
        }
    }
}
