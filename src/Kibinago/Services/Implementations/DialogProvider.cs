using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Services.Implementations.DialogOptions;

namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// Default implementation of IDialogProvider using standard WinForms dialogs. Titles, filter
    /// and error messages not given per call come from <see cref="DialogProviderOptions"/>.
    /// </summary>
    public class DialogProvider : IWinFormsDialogProvider
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

        public InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = ResolveFolderDescription(options, _options);
                    dialog.SelectedPath = options?.SelectedPath ?? "";

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
        public InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null)
        {
            try
            {
                using (var dialog = new ColorDialog())
                {
                    dialog.Color = options?.InitialColor ?? Color.Black;

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

        public InteractionResult<Font> ShowFontDialog(FontDialogOptions options = null)
        {
            try
            {
                using (var dialog = new FontDialog())
                {
                    dialog.Font = options?.InitialFont ?? SystemFonts.DefaultFont;

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<Font>.Ok(dialog.Font)
                        : InteractionResult<Font>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<Font>.Error(_options.FontSelectErrorMessage, ex);
            }
        }

        // -----------------------
        // Print Dialogs
        // -----------------------
        public InteractionResult<PrinterSettings> ShowPrintDialog(PrintDialogOptions options = null)
        {
            try
            {
                using (var dialog = new PrintDialog())
                {
                    dialog.AllowSomePages = options?.AllowSomePages ?? true;
                    dialog.PrinterSettings = options?.PrinterSettings ?? new PrinterSettings();

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<PrinterSettings>.Ok(dialog.PrinterSettings)
                        : InteractionResult<PrinterSettings>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<PrinterSettings>.Error(_options.PrintDialogErrorMessage, ex);
            }
        }

        public InteractionResult<PageSettings> ShowPageSetupDialog(PageSetupDialogOptions options = null)
        {
            try
            {
                using (var dialog = new PageSetupDialog())
                {
                    dialog.PageSettings = options?.PageSettings ?? new PageSettings();

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<PageSettings>.Ok(dialog.PageSettings)
                        : InteractionResult<PageSettings>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<PageSettings>.Error(_options.PageSetupErrorMessage, ex);
            }
        }

        public InteractionResult<bool> ShowPrintPreviewDialog(PrintPreviewDialogOptions options = null)
        {
            try
            {
                using (var dialog = new PrintPreviewDialog())
                {
                    dialog.Document = options?.Document;
                    if (!string.IsNullOrEmpty(options?.Title))
                    {
                        dialog.Text = options.Title;
                    }
                    else
                    {
                        dialog.Text = _options.PrintPreviewDialogTitle;
                    }

                    // PrintPreviewDialog has no OK button: it closes with Cancel (or None) however the
                    // user leaves it, so the result only reports that the preview was shown.
                    dialog.ShowDialog();
                    return InteractionResult<bool>.Ok(true);
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<bool>.Error(_options.PrintPreviewErrorMessage, ex);
            }
        }

    }
}
