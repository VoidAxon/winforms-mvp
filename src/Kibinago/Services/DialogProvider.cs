using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Services;

namespace Kibinago.Services
{
    /// <summary>
    /// Default implementation of IDialogProvider using standard WinForms dialogs. Titles, filter
    /// and error messages not given per call come from <see cref="DialogProviderOptions"/>.
    /// </summary>
    public class DialogProvider : IDialogProvider
    {
        private readonly DialogProviderOptions _options;

        /// <param name="options">Defaults; copied here (later edits have no effect).</param>
        public DialogProvider(DialogProviderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options.Copy();
        }

        internal static string ResolveFilter(string perCall, DialogProviderOptions options)
            => perCall ?? options.DefaultFileFilter;

        internal static string ResolveOpenTitle(OpenFileDialogOptions perCall, DialogProviderOptions options)
            => perCall?.Title ?? options.OpenFileDialogTitle;

        internal static string ResolveSaveTitle(SaveFileDialogOptions perCall, DialogProviderOptions options)
            => perCall?.Title ?? options.SaveFileDialogTitle;

        internal static string ResolveFolderDescription(FolderBrowserDialogOptions perCall, DialogProviderOptions options)
            => perCall?.Description ?? options.FolderBrowserDescription;

        // Per-call options onto the WinForms dialog (null options = the option class's defaults).
        internal static void Configure(FolderBrowserDialog dialog, FolderBrowserDialogOptions perCall, DialogProviderOptions options)
        {
            perCall = perCall ?? new FolderBrowserDialogOptions();
            dialog.Description = ResolveFolderDescription(perCall, options);
            dialog.SelectedPath = perCall.SelectedPath ?? "";
            dialog.ShowNewFolderButton = perCall.ShowNewFolderButton;
        }

        internal static void Configure(ColorDialog dialog, ColorDialogOptions options)
        {
            options = options ?? new ColorDialogOptions();
            dialog.Color = options.InitialColor ?? Color.Black;
            dialog.AllowFullOpen = options.AllowFullOpen;
            dialog.AnyColor = options.AnyColor;
        }

        /// <inheritdoc />
        public InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = ResolveFilter(options?.Filter, _options);
                    dialog.Multiselect = false;  // Single file only
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = ResolveOpenTitle(options, _options);

                    if (!string.IsNullOrEmpty(options?.FileName))
                        dialog.FileName = options.FileName;

                    dialog.CheckFileExists = options?.CheckFileExists ?? true;
                    dialog.CheckPathExists = options?.CheckPathExists ?? true;

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<string>.Ok(dialog.FileName)
                        : InteractionResult<string>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<string>.Error(_options.FileOpenErrorMessage, ex);
            }
        }

        /// <inheritdoc />
        public InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = ResolveFilter(options?.Filter, _options);
                    dialog.Multiselect = true;  // Multiple files allowed
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = ResolveOpenTitle(options, _options);

                    if (!string.IsNullOrEmpty(options?.FileName))
                        dialog.FileName = options.FileName;

                    dialog.CheckFileExists = options?.CheckFileExists ?? true;
                    dialog.CheckPathExists = options?.CheckPathExists ?? true;

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<string[]>.Ok(dialog.FileNames)
                        : InteractionResult<string[]>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<string[]>.Error(_options.FileOpenErrorMessage, ex);
            }
        }

        /// <inheritdoc />
        public InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = ResolveFilter(options?.Filter, _options);
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = ResolveSaveTitle(options, _options);

                    if (!string.IsNullOrEmpty(options?.DefaultExt))
                        dialog.DefaultExt = options.DefaultExt;

                    if (!string.IsNullOrEmpty(options?.FileName))
                        dialog.FileName = options.FileName;

                    dialog.OverwritePrompt = options?.OverwritePrompt ?? true;
                    dialog.CreatePrompt = options?.CreatePrompt ?? false;

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<string>.Ok(dialog.FileName)
                        : InteractionResult<string>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<string>.Error(_options.FileSaveErrorMessage, ex);
            }
        }

        /// <inheritdoc />
        public InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    Configure(dialog, options, _options);

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<string>.Ok(dialog.SelectedPath)
                        : InteractionResult<string>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<string>.Error(_options.FolderSelectErrorMessage, ex);
            }
        }

        // -----------------------
        // Style Selection Dialogs
        // -----------------------
        /// <inheritdoc />
        public InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null)
        {
            try
            {
                using (var dialog = new ColorDialog())
                {
                    Configure(dialog, options);

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<Color>.Ok(dialog.Color)
                        : InteractionResult<Color>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<Color>.Error(_options.ColorSelectErrorMessage, ex);
            }
        }
    }
}
