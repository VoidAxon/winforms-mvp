using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations.DialogOptions;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default implementation of IDialogProvider using standard WinForms dialogs.
    /// Uses configurable defaults from DialogDefaults class.
    /// </summary>
    public class DialogProvider : IWinFormsDialogProvider
    {
        public InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = options?.Filter ?? DialogDefaults.DefaultFileFilter;
                    dialog.Multiselect = false;  // Single file only
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = options?.Title ?? DialogDefaults.OpenFileDialogTitle;

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
                return InteractionResult<string>.Error(DialogDefaults.FileOpenErrorMessage, ex);
            }
        }

        public InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = options?.Filter ?? DialogDefaults.DefaultFileFilter;
                    dialog.Multiselect = true;  // Multiple files allowed
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = options?.Title ?? DialogDefaults.OpenFileDialogTitle;

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
                return InteractionResult<string[]>.Error(DialogDefaults.FileOpenErrorMessage, ex);
            }
        }

        public InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null)
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = options?.Filter ?? DialogDefaults.DefaultFileFilter;
                    dialog.InitialDirectory = options?.InitialDirectory ?? "";
                    dialog.Title = options?.Title ?? DialogDefaults.SaveFileDialogTitle;

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
                return InteractionResult<string>.Error(DialogDefaults.FileSaveErrorMessage, ex);
            }
        }

        public InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = options?.Description ?? DialogDefaults.FolderBrowserDescription;
                    dialog.SelectedPath = options?.SelectedPath ?? "";

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<string>.Ok(dialog.SelectedPath)
                        : InteractionResult<string>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<string>.Error(DialogDefaults.FolderSelectErrorMessage, ex);
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
                return InteractionResult<Color>.Error(DialogDefaults.ColorSelectErrorMessage, ex);
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
                return InteractionResult<Font>.Error(DialogDefaults.FontSelectErrorMessage, ex);
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
                return InteractionResult<PrinterSettings>.Error(DialogDefaults.PrintDialogErrorMessage, ex);
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
                return InteractionResult<PageSettings>.Error(DialogDefaults.PageSetupErrorMessage, ex);
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
                        dialog.Text = DialogDefaults.PrintPreviewDialogTitle;
                    }

                    var result = dialog.ShowDialog();
                    return result == DialogResult.OK
                        ? InteractionResult<bool>.Ok(true)
                        : InteractionResult<bool>.Cancel();
                }
            }
            catch (Exception ex)
            {
                return InteractionResult<bool>.Error(DialogDefaults.PrintPreviewErrorMessage, ex);
            }
        }

    }
}
