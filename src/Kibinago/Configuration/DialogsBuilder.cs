using Kibinago.Services;

namespace Kibinago.Configuration
{
    /// <summary>
    /// Built-in file / folder / print dialog texts (<c>mvp.Dialogs</c>). To replace the dialogs
    /// themselves, register your own <see cref="IDialogProvider"/> in the services block.
    /// </summary>
    public sealed class DialogsBuilder
    {
        private readonly KibinagoBuilder _owner;
        internal readonly DialogProviderOptions Options = new DialogProviderOptions();

        internal DialogsBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>See <see cref="DialogProviderOptions.OpenFileDialogTitle"/>.</summary>
        public string OpenFileDialogTitle
        {
            get => Options.OpenFileDialogTitle;
            set { _owner.EnsureNotBuilt(); Options.OpenFileDialogTitle = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.SaveFileDialogTitle"/>.</summary>
        public string SaveFileDialogTitle
        {
            get => Options.SaveFileDialogTitle;
            set { _owner.EnsureNotBuilt(); Options.SaveFileDialogTitle = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.DefaultFileFilter"/>.</summary>
        public string DefaultFileFilter
        {
            get => Options.DefaultFileFilter;
            set { _owner.EnsureNotBuilt(); Options.DefaultFileFilter = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.FolderBrowserDescription"/>.</summary>
        public string FolderBrowserDescription
        {
            get => Options.FolderBrowserDescription;
            set { _owner.EnsureNotBuilt(); Options.FolderBrowserDescription = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.PrintPreviewDialogTitle"/>.</summary>
        public string PrintPreviewDialogTitle
        {
            get => Options.PrintPreviewDialogTitle;
            set { _owner.EnsureNotBuilt(); Options.PrintPreviewDialogTitle = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.FileOpenErrorMessage"/>.</summary>
        public string FileOpenErrorMessage
        {
            get => Options.FileOpenErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.FileOpenErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.FileSaveErrorMessage"/>.</summary>
        public string FileSaveErrorMessage
        {
            get => Options.FileSaveErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.FileSaveErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.FolderSelectErrorMessage"/>.</summary>
        public string FolderSelectErrorMessage
        {
            get => Options.FolderSelectErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.FolderSelectErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.ColorSelectErrorMessage"/>.</summary>
        public string ColorSelectErrorMessage
        {
            get => Options.ColorSelectErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.ColorSelectErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.FontSelectErrorMessage"/>.</summary>
        public string FontSelectErrorMessage
        {
            get => Options.FontSelectErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.FontSelectErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.PrintDialogErrorMessage"/>.</summary>
        public string PrintDialogErrorMessage
        {
            get => Options.PrintDialogErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.PrintDialogErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.PageSetupErrorMessage"/>.</summary>
        public string PageSetupErrorMessage
        {
            get => Options.PageSetupErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.PageSetupErrorMessage = value; }
        }

        /// <summary>See <see cref="DialogProviderOptions.PrintPreviewErrorMessage"/>.</summary>
        public string PrintPreviewErrorMessage
        {
            get => Options.PrintPreviewErrorMessage;
            set { _owner.EnsureNotBuilt(); Options.PrintPreviewErrorMessage = value; }
        }
    }
}
