using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kibinago.Services;
using Kibinago.Services.Implementations;
using Kibinago.Services.Implementations.DialogOptions;
using Xunit;

namespace Kibinago.Samples.Tests.Services
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

        // Per-call options reach the WinForms dialog (configured, not shown).
        [Fact]
        public void ColorDialog_AppliesEveryOption()
        {
            using (var dialog = new ColorDialog())
            {
                DialogProvider.Configure(dialog, new ColorDialogOptions { InitialColor = Color.Red, AllowFullOpen = false, AnyColor = true });
                Assert.Equal(Color.Red.ToArgb(), dialog.Color.ToArgb());
                Assert.False(dialog.AllowFullOpen);
                Assert.True(dialog.AnyColor);
            }
        }

        [Fact]
        public void FolderBrowserDialog_AppliesEveryOption()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogProvider.Configure(dialog, new FolderBrowserDialogOptions { SelectedPath = @"C:\Data", ShowNewFolderButton = false }, Configured);
                Assert.Equal(@"C:\Data", dialog.SelectedPath);
                Assert.Equal("Pick!", dialog.Description);
                Assert.False(dialog.ShowNewFolderButton);
            }
        }

        [Fact]
        public void FontDialog_AppliesEveryOption()
        {
            using (var dialog = new FontDialog())
            {
                DialogProvider.Configure(dialog, new FontDialogOptions { ShowColor = true });
                Assert.True(dialog.ShowColor);
            }
        }

        [Fact]
        public void PrintDialog_AppliesEveryOption()
        {
            var printer = new PrinterSettings();
            using (var dialog = new PrintDialog())
            {
                DialogProvider.Configure(dialog, new PrintDialogOptions { PrinterSettings = printer, AllowSelection = true, AllowSomePages = false });
                Assert.Same(printer, dialog.PrinterSettings);
                Assert.True(dialog.AllowSelection);
                Assert.False(dialog.AllowSomePages);
            }
        }

        [Fact]
        public void PageSetupDialog_AppliesEveryOption()
        {
            var page = new PageSettings();
            var printer = new PrinterSettings();
            using (var dialog = new PageSetupDialog())
            {
                DialogProvider.Configure(dialog, new PageSetupDialogOptions { PageSettings = page, PrinterSettings = printer });
                Assert.Same(page, dialog.PageSettings);
                Assert.Same(printer, dialog.PrinterSettings);
            }
        }

        [Fact]
        public void OptionDefaults_MatchTheWinFormsDialogs()
        {
            // An unset option leaves the dialog as WinForms creates it.
            using (var color = new ColorDialog())
            using (var folder = new FolderBrowserDialog())
            using (var font = new FontDialog())
            using (var print = new PrintDialog())
            {
                Assert.Equal(color.AllowFullOpen, new ColorDialogOptions().AllowFullOpen);
                Assert.Equal(color.AnyColor, new ColorDialogOptions().AnyColor);
                Assert.Equal(folder.ShowNewFolderButton, new FolderBrowserDialogOptions().ShowNewFolderButton);
                Assert.Equal(font.ShowColor, new FontDialogOptions().ShowColor);
                Assert.Equal(print.AllowSelection, new PrintDialogOptions().AllowSelection);
            }
        }
    }
}
