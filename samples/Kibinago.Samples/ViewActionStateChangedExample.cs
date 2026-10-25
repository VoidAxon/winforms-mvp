using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Services;

namespace Kibinago.Samples
{
    /// <summary>
    /// ActionKeys for the data grid editor module.
    /// </summary>
    public static class DataGridActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("DataGrid");

        public static readonly ViewAction AddRow = Factory.Create("AddRow");
        public static readonly ViewAction EditRow = Factory.Create("EditRow");
        public static readonly ViewAction DeleteRow = Factory.Create("DeleteRow");
        public static readonly ViewAction ExportData = Factory.Create("ExportData");
    }

    #region View Interface

    /// <summary>
    /// View interface for data grid editor.
    /// Demonstrates proper MVP separation - no UI elements exposed.
    ///
    /// IMPORTANT:
    /// - No Button or any UI control types
    /// - Only data properties and events
    /// - BindActions method for ViewAction integration
    /// </summary>
    public interface IDataGridEditorView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        bool HasSelectedRow { get; }
        bool HasData { get; }
        int SelectedRowCount { get; }

        // ========================================
        // Events (for state-driven updates)
        // ========================================

        event EventHandler SelectionChanged;
        event EventHandler DataChanged;
    }

    #endregion

    /// <summary>
    /// Example presenter demonstrating state-driven CanExecute updates.
    /// Shows the difference between:
    /// 1. Action-driven updates (automatic after action execution)
    /// 2. State-driven updates (manual via RaiseCanExecuteChanged)
    ///
    /// This is similar to WPF's ICommand.CanExecuteChanged pattern.
    ///
    /// IMPORTANT:
    /// - Presenter has ZERO knowledge of Button or any UI elements
    /// - Uses BindActions() pattern for proper MVP separation
    /// </summary>
    public class DataGridEditorPresenter : WindowPresenterBase<IDataGridEditorView>
    {
        private readonly IMessageService _messageService;

        public DataGridEditorPresenter(IMessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        protected override void OnViewAttached()
        {
            // Nothing to do here - View will handle UI binding internally
        }

        protected override void RegisterViewActions()
        {
            // AddRow is always enabled
            Dispatcher.Register(DataGridActions.AddRow, OnAddRow);

            // EditRow only enabled when exactly one row is selected
            Dispatcher.Register(
                DataGridActions.EditRow,
                OnEditRow,
                canExecute: () => View.SelectedRowCount == 1);

            // DeleteRow only enabled when at least one row is selected
            Dispatcher.Register(
                DataGridActions.DeleteRow,
                OnDeleteRow,
                canExecute: () => View.HasSelectedRow);

            // ExportData only enabled when there is data
            Dispatcher.Register(
                DataGridActions.ExportData,
                OnExportData,
                canExecute: () => View.HasData);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Subscribe to view events that change state
            // When these events fire, we need to notify the dispatcher
            SubscribeToViewEvents();
        }

        private void SubscribeToViewEvents()
        {
            // STATE-DRIVEN UPDATE PATTERN:
            // When user selects/deselects rows (state change not caused by action),
            // we need to manually notify the dispatcher to refresh CanExecute states.

            View.SelectionChanged += (sender, e) =>
            {
                // User selected/deselected rows - notify dispatcher
                // This is similar to WPF's ICommand.RaiseCanExecuteChanged()
                Dispatcher.RaiseCanExecuteChanged();
            };

            View.DataChanged += (sender, e) =>
            {
                // Data changed externally - notify dispatcher
                Dispatcher.RaiseCanExecuteChanged();
            };
        }

        #region Action Handlers - Action-Driven Updates

        // These handlers demonstrate ACTION-DRIVEN updates:
        // UI state automatically updates after action execution (no manual call needed)

        private void OnAddRow()
        {
            // Add row logic
            _messageService.ShowInfo("Row added");

            // UI state automatically updates after this action completes
            // (because ActionExecuted event is automatically triggered)
        }

        private void OnEditRow()
        {
            // Edit row logic
            _messageService.ShowInfo("Editing selected row");

            // UI state automatically updates
        }

        private void OnDeleteRow()
        {
            if (_messageService.ConfirmYesNo("Delete selected rows?", "Confirm Delete"))
            {
                // Delete logic
                _messageService.ShowInfo("Rows deleted");

                // UI state automatically updates
            }
        }

        private void OnExportData()
        {
            // Export logic
            _messageService.ShowInfo("Data exported");
        }

        #endregion
    }

    /// <summary>
    /// Another example showing async operations and state changes.
    /// </summary>
    public static class AsyncOperationActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("AsyncOp");

        public static readonly ViewAction StartOperation = Factory.Create("Start");
        public static readonly ViewAction CancelOperation = Factory.Create("Cancel");
    }

    /// <summary>
    /// View interface for async operation demo.
    /// Demonstrates proper MVP separation - no UI elements exposed.
    ///
    /// IMPORTANT:
    /// - No Button, ProgressBar, or any UI control types
    /// - Only data properties (bool for operation state)
    /// - BindActions method for ViewAction integration
    /// </summary>
    public interface IAsyncOperationView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        bool IsOperationRunning { get; set; }
    }

    /// <summary>
    /// Example showing how to handle async operations with state-driven updates.
    ///
    /// IMPORTANT:
    /// - Presenter has ZERO knowledge of Button, ProgressBar, or any UI elements
    /// - Uses BindActions() pattern for proper MVP separation
    /// - The handler is an <c>async Task</c>: the dispatcher disables Start while it runs
    ///   (repeated clicks are ignored) and re-queries every CanExecute when it starts and
    ///   finishes, so no manual RaiseCanExecuteChanged() is needed around the await.
    /// </summary>
    public class AsyncOperationPresenter : WindowPresenterBase<IAsyncOperationView>
    {
        private readonly IMessageService _messageService;

        // Needed only because a running Start must ENABLE another action (Cancel).
        private bool _isRunning;

        // Linked with the handler's token: Cancel cancels it, and so does closing the window.
        private CancellationTokenSource _operationCts;

        public AsyncOperationPresenter(IMessageService messageService)
        {
            _messageService = messageService;
        }

        protected override void OnViewAttached()
        {
            // Nothing to do here - View will handle UI binding internally
        }

        protected override void RegisterViewActions()
        {
            // Start: disabled automatically while it runs
            Dispatcher.Register(
                AsyncOperationActions.StartOperation,
                OnStartOperationAsync);

            // Cancel only enabled when running
            Dispatcher.Register(
                AsyncOperationActions.CancelOperation,
                OnCancelOperation,
                canExecute: () => _isRunning);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            _isRunning = false;
        }

        private async Task OnStartOperationAsync(CancellationToken cancellationToken)
        {
            // Set before the first await: the dispatcher re-queries CanExecute right after the
            // handler starts, which enables Cancel.
            _isRunning = true;
            View.IsOperationRunning = true;
            _operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Simulate async work
                await Task.Delay(3000, _operationCts.Token);

                _messageService.ShowInfo("Operation completed");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The user clicked Cancel (closing the window ends the action silently instead).
                _messageService.ShowInfo("Operation cancelled");
            }
            finally
            {
                // Reset before the handler completes: the dispatcher re-queries CanExecute
                // when it finishes, which disables Cancel again.
                _isRunning = false;
                View.IsOperationRunning = false;
                _operationCts.Dispose();
                _operationCts = null;
            }
        }

        private void OnCancelOperation()
        {
            _operationCts?.Cancel();
        }
    }
}
