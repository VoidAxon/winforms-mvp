using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using WinformsMVP.Services.Implementations.DialogOptions;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    // The merge of per-call dialog options with the provider's options, tested without opening dialogs.
    public class DialogProviderOptionsTests
    {
        private static readonly DialogProviderOptions Configured = new DialogProviderOptions
        {
            OpenFileDialogTitle = "Open!",
            SaveFileDialogTitle = "Save!",
            DefaultFileFilter = "Text|*.txt",
            FolderBrowserDescription = "Pick!",
        };

        [Fact]
        public void FolderBrowserDialogOptions_Description_DefaultsToNull()
        {
            Assert.Null(new FolderBrowserDialogOptions().Description);
        }

        [Fact]
        public void FolderDescription_UsesOptionsWhenUnset()
        {
            Assert.Equal("Pick!", DialogProvider.ResolveFolderDescription(new FolderBrowserDialogOptions(), Configured));
            Assert.Equal("Pick!", DialogProvider.ResolveFolderDescription(null, Configured));
            Assert.Equal("Mine", DialogProvider.ResolveFolderDescription(new FolderBrowserDialogOptions { Description = "Mine" }, Configured));
        }

        [Fact]
        public void OpenFile_TitleAndFilter_UseOptionsWhenUnset()
        {
            Assert.Equal("Open!", DialogProvider.ResolveOpenTitle(new OpenFileDialogOptions(), Configured));
            Assert.Equal("Text|*.txt", DialogProvider.ResolveFilter(new OpenFileDialogOptions().Filter, Configured));
            Assert.Equal("Mine", DialogProvider.ResolveOpenTitle(new OpenFileDialogOptions { Title = "Mine" }, Configured));
        }

        [Fact]
        public void SaveFile_Title_UsesOptionsWhenUnset()
        {
            Assert.Equal("Save!", DialogProvider.ResolveSaveTitle(new SaveFileDialogOptions(), Configured));
        }

        [Fact]
        public void Defaults_MatchThePreviousBuiltInValues()
        {
            var d = new DialogProviderOptions();
            Assert.Equal("Open File", d.OpenFileDialogTitle);
            Assert.Equal("Save File", d.SaveFileDialogTitle);
            Assert.Equal("All Files (*.*)|*.*", d.DefaultFileFilter);
            Assert.Equal("Please select a folder", d.FolderBrowserDescription);
            Assert.Equal("System", new MessageDialogOptions().DefaultCaption);
        }
    }
}
