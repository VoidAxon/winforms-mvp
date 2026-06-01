using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples
{
    /// <summary>
    /// Example showing ActionKeys for actions that require parameters.
    /// </summary>
    public static class DocumentActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Document");

        public static readonly ViewAction Open = Factory.Create("Open");
        public static readonly ViewAction SaveAs = Factory.Create("SaveAs");
        public static readonly ViewAction Export = Factory.Create("Export");
    }

    #region View Interface

    /// <summary>
    /// View interface for document editor.
    /// Demonstrates proper MVP separation - no UI elements exposed.
    ///
    /// IMPORTANT:
    /// - No Button, ComboBox or any UI control types
    /// - Only data properties and behavior methods
    /// - Presenter has NO knowledge of UI implementation
    /// </summary>
    public interface IDocumentEditorView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        /// <summary>
        /// Path to the currently loaded document
        /// </summary>
        string DocumentPath { get; set; }

        /// <summary>
        /// Whether a document is currently loaded
        /// </summary>
        bool IsDocumentLoaded { get; }

        /// <summary>
        /// Selected export format (e.g., "PDF", "HTML", "Markdown")
        /// </summary>
        string SelectedExportFormat { get; set; }

        // ========================================
        // View Behaviors
        // ========================================

        /// <summary>
        /// Update the status message
        /// </summary>
        void UpdateStatus(string message);
    }

    #endregion

    /// <summary>
    /// Example presenter demonstrating ViewAction with typed parameters.
    /// Shows how to use the ViewActionDispatcher's generic Register method.
    ///
    /// Best practices demonstrated:
    /// - Uses IMessageService instead of MessageBox (MVP principle)
    /// - Uses IDialogProvider instead of new OpenFileDialog/SaveFileDialog
    /// - No direct access to UI elements (Button, ComboBox, etc.)
    /// - All UI interactions through View interface abstraction
    /// </summary>
    public class DocumentEditorPresenter : WindowPresenterBase<IDocumentEditorView>
    {
        private readonly IMessageService _messageService;
        private readonly IDialogProvider _dialogProvider;

        public DocumentEditorPresenter(IMessageService messageService, IDialogProvider dialogProvider)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _dialogProvider = dialogProvider ?? throw new ArgumentNullException(nameof(dialogProvider));
        }

        protected override void OnViewAttached()
        {
            // Nothing to do - View will bind UI controls internally
        }

        protected override void RegisterViewActions()
        {
            // Register actions with handlers and CanExecute predicates
            _dispatcher.Register(
                DocumentActions.Open,
                OnOpenDocument,
                canExecute: () => true);

            _dispatcher.Register(
                DocumentActions.SaveAs,
                OnSaveDocumentAs,
                canExecute: () => View.IsDocumentLoaded);

            _dispatcher.Register(
                DocumentActions.Export,
                OnExportDocument,
                canExecute: () => View.IsDocumentLoaded);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Initialize view state
            View.UpdateStatus("Ready. Select actions from the toolbar.");
        }

        #region Action Handlers

        /// <summary>
        /// Open document action handler.
        /// Uses IDialogProvider to show file dialog (MVP best practice).
        /// </summary>
        private void OnOpenDocument()
        {
            var options = new WinformsMVP.Services.Implementations.DialogOptions.OpenFileDialogOptions
            {
                Filter = "Text Files|*.txt|All Files|*.*",
                Title = "Open Document"
            };

            var result = _dialogProvider.ShowOpenFileDialog(options);

            if (result.Status == WinformsMVP.Common.InteractionStatus.Ok)
            {
                View.DocumentPath = result.Value;
                View.UpdateStatus($"Opened: {System.IO.Path.GetFileName(result.Value)}");

                // Best practice: Use IMessageService instead of MessageBox
                _messageService.ShowInfo($"Document opened successfully:\n{result.Value}", "Success");

                // Note: UI state automatically updates after action execution
                // CanExecute predicates will be re-evaluated
            }
            else
            {
                View.UpdateStatus("Open cancelled.");
            }
        }

        /// <summary>
        /// Save As document action handler.
        /// Uses IDialogProvider to show file dialog (MVP best practice).
        /// </summary>
        private void OnSaveDocumentAs()
        {
            var options = new WinformsMVP.Services.Implementations.DialogOptions.SaveFileDialogOptions
            {
                Filter = "Text Files|*.txt|All Files|*.*",
                Title = "Save Document As",
                FileName = System.IO.Path.GetFileName(View.DocumentPath)
            };

            var result = _dialogProvider.ShowSaveFileDialog(options);

            if (result.Status == WinformsMVP.Common.InteractionStatus.Ok)
            {
                View.DocumentPath = result.Value;
                View.UpdateStatus($"Saved as: {System.IO.Path.GetFileName(result.Value)}");

                // Best practice: Use IMessageService instead of MessageBox
                _messageService.ShowInfo($"Document saved successfully:\n{result.Value}", "Success");
            }
            else
            {
                View.UpdateStatus("Save cancelled.");
            }
        }

        /// <summary>
        /// Export document action handler.
        /// Gets export format from View property (no ComboBox access).
        /// </summary>
        private void OnExportDocument()
        {
            var format = View.SelectedExportFormat ?? "PDF";

            // Simulate export operation
            View.UpdateStatus($"Exporting to {format}...");

            // Best practice: Use IMessageService instead of MessageBox
            _messageService.ShowInfo(
                $"Document exported successfully!\n\n" +
                $"Format: {format}\n" +
                $"Source: {System.IO.Path.GetFileName(View.DocumentPath)}",
                "Export Complete");

            View.UpdateStatus($"Export to {format} completed.");
        }

        #endregion
    }
}
