using System;
using System.Drawing;
using Kibinago.Common;
using Kibinago.Services;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakeDialogProviderTests
    {
        [Fact]
        public void SaveFile_Unscripted_Throws()
        {
            var dialogs = new FakeDialogProvider();
            var ex = Assert.Throws<UnscriptedInteractionException>(() => dialogs.ShowSaveFileDialog());
            Assert.Contains("SaveFile", ex.Message);
            Assert.Equal(DialogKind.SaveFile, Assert.Single(dialogs.Calls).Kind);
        }

        [Fact]
        public void SaveFile_Queued_ReturnsIt_AndRecordsTheOptions()
        {
            var dialogs = new FakeDialogProvider();
            dialogs.EnqueueSaveFile(InteractionResult<string>.Ok(@"C:\out.csv"));
            var options = new SaveFileDialogOptions { FileName = "out.csv" };

            var result = dialogs.ShowSaveFileDialog(options);

            Assert.True(result.IsOk);
            Assert.Equal(@"C:\out.csv", result.Value);
            Assert.Same(options, Assert.Single(dialogs.Calls).Options);
        }

        [Fact]
        public void NullOptions_ArePreservedInTheCall()
        {
            var dialogs = new FakeDialogProvider { OpenFileHandler = _ => InteractionResult<string>.Cancel() };
            dialogs.ShowOpenFileDialog();
            Assert.Null(Assert.Single(dialogs.Calls).Options);
        }

        [Fact]
        public void Handler_SeesOptions_AndItsExceptionPropagates()
        {
            var dialogs = new FakeDialogProvider
            {
                FolderBrowserHandler = o => InteractionResult<string>.Ok(o.Description),
                ColorHandler = _ => throw new InvalidOperationException("setup error")
            };

            Assert.Equal("Pick", dialogs.ShowFolderBrowserDialog(new FolderBrowserDialogOptions { Description = "Pick" }).Value);
            Assert.Throws<InvalidOperationException>(() => dialogs.ShowColorDialog());
        }

        [Fact]
        public void QueueBeforeHandler_PerKind()
        {
            var dialogs = new FakeDialogProvider { OpenMultipleFilesHandler = _ => InteractionResult<string[]>.Cancel() };
            dialogs.EnqueueOpenMultipleFiles(InteractionResult<string[]>.Ok(new[] { "a", "b" }));
            dialogs.EnqueueColor(InteractionResult<Color>.Ok(Color.Red));

            Assert.Equal(2, dialogs.ShowOpenMultipleFilesDialog().Value.Length);
            Assert.True(dialogs.ShowOpenMultipleFilesDialog().IsCancelled);
            Assert.Equal(Color.Red, dialogs.ShowColorDialog().Value);
        }

        [Fact]
        public void CancelUnscripted_ReturnsCancel()
        {
            var dialogs = new FakeDialogProvider();
            dialogs.CancelUnscripted();

            Assert.True(dialogs.ShowOpenFileDialog().IsCancelled);
            Assert.True(dialogs.ShowColorDialog().IsCancelled);

            dialogs.ClearCalls();
            Assert.Empty(dialogs.Calls);
        }
    }
}
