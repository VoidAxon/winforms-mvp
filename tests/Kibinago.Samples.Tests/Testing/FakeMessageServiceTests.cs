using System.Linq;
using Kibinago.Common;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakeMessageServiceTests
    {
        [Fact]
        public void Info_IsRecorded()
        {
            var messages = new FakeMessageService();
            messages.ShowInfo("Saved", "Title");

            var call = Assert.Single(messages.Calls);
            Assert.Equal(MessageKind.Info, call.Kind);
            Assert.Equal("Saved", call.Text);
            Assert.Equal("Title", call.Caption);
        }

        [Fact]
        public void ConfirmYesNo_Unscripted_ThrowsNamingTheQuestion_AndIsRecorded()
        {
            var messages = new FakeMessageService();

            var ex = Assert.Throws<UnscriptedInteractionException>(() => messages.ConfirmYesNo("Delete order 7?"));

            Assert.Contains("Delete order 7?", ex.Message);
            Assert.Contains("YesNo", ex.Message);
            Assert.Equal(MessageKind.YesNo, Assert.Single(messages.Calls).Kind);
        }

        [Fact]
        public void Queue_IsUsedBeforeTheHandler_InOrder()
        {
            var messages = new FakeMessageService { YesNoHandler = _ => true };
            messages.EnqueueYesNo(false, false);

            Assert.False(messages.ConfirmYesNo("1"));
            Assert.False(messages.ConfirmYesNo("2"));
            Assert.True(messages.ConfirmYesNo("3"));
        }

        [Fact]
        public void Handler_SeesTheCall_IncludingCaption()
        {
            MessageCall seen = null;
            var messages = new FakeMessageService { OkCancelHandler = c => { seen = c; return true; } };

            Assert.True(messages.ConfirmOkCancel("Continue?", "Import"));
            Assert.Equal(MessageKind.OkCancel, seen.Kind);
            Assert.Equal("Import", seen.Caption);
        }

        [Fact]
        public void AnswerUnscripted_AnswersYes()
        {
            var messages = new FakeMessageService();
            messages.AnswerUnscripted(yes: true);

            Assert.True(messages.ConfirmYesNo("a"));
            Assert.True(messages.ConfirmOkCancel("b"));
            Assert.Equal(ConfirmResult.Yes, messages.ConfirmYesNoCancel("c"));

            messages.AnswerUnscripted(yes: false);
            Assert.False(messages.ConfirmYesNo("a"));
            Assert.Equal(ConfirmResult.No, messages.ConfirmYesNoCancel("c"));
        }

        [Fact]
        public void YesNoCancel_QueueAndHandler()
        {
            var messages = new FakeMessageService { YesNoCancelHandler = _ => ConfirmResult.No };
            messages.EnqueueYesNoCancel(ConfirmResult.Cancel);

            Assert.Equal(ConfirmResult.Cancel, messages.ConfirmYesNoCancel("Save?"));
            Assert.Equal(ConfirmResult.No, messages.ConfirmYesNoCancel("Save?"));
        }

        [Fact]
        public void IsConfirmation_IsTrueForTheThreeConfirmKinds()
        {
            var messages = new FakeMessageService();
            messages.AnswerUnscripted(yes: true);
            messages.ShowWarning("w");
            messages.ConfirmYesNo("a");
            messages.ConfirmOkCancel("b");
            messages.ConfirmYesNoCancel("c");

            Assert.Equal(new[] { false, true, true, true }, messages.Calls.Select(c => c.IsConfirmation).ToArray());
        }

        [Fact]
        public void ClearCalls_KeepsScripts_ResetScripts_KeepsCalls()
        {
            var messages = new FakeMessageService();
            messages.EnqueueYesNo(true, true);
            messages.ConfirmYesNo("1");

            messages.ClearCalls();
            Assert.Empty(messages.Calls);
            Assert.True(messages.ConfirmYesNo("2"));

            messages.ResetScripts();
            Assert.Single(messages.Calls);
            Assert.Throws<UnscriptedInteractionException>(() => messages.ConfirmYesNo("3"));
        }
    }
}
