using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.AsyncDemo
{
    /// <summary>
    /// Demonstrates async/await patterns in MVP:
    /// - Async action handlers (<c>async Task</c>, registered with <c>Dispatcher.Register</c>)
    /// - Long-running operations with progress tracking
    /// - Cancellation: a user "Cancel" button linked with the token the framework passes in
    ///   (cancelled when the window closes)
    /// - Error handling in async handlers
    /// </summary>
    /// <remarks>
    /// The dispatcher disables a running async action by itself and ignores repeated triggers,
    /// and it re-queries every CanExecute when an async action starts and when it finishes. The
    /// <c>_isBusy</c> flag exists only because a running operation must also disable the OTHER
    /// start buttons and enable Cancel — no manual <c>RaiseCanExecuteChanged()</c> is needed.
    /// </remarks>
    public class AsyncDemoPresenter : ControlPresenterBase<IAsyncDemoView>
    {
        // Linked with the handler's token: the Cancel button cancels it, and so does closing the window.
        private CancellationTokenSource _cancellationTokenSource;

        // True while any start operation runs; disables the other start actions and enables Cancel.
        private bool _isBusy;

        protected override void OnViewAttached()
        {
            // Nothing to subscribe to
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(
                AsyncActions.StartSimpleOperation,
                OnStartSimpleOperationAsync,
                canExecute: () => !_isBusy);

            Dispatcher.Register(
                AsyncActions.StartLongOperation,
                OnStartLongOperationAsync,
                canExecute: () => !_isBusy);

            Dispatcher.Register(
                AsyncActions.StartWithProgress,
                OnStartWithProgressAsync,
                canExecute: () => !_isBusy);

            Dispatcher.Register(
                AsyncActions.StartWithError,
                OnStartWithErrorAsync,
                canExecute: () => !_isBusy);

            Dispatcher.Register(
                AsyncActions.Cancel,
                OnCancel,
                canExecute: () => _isBusy);

            Dispatcher.Register(
                AsyncActions.Clear,
                OnClear);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.StatusMessage = "Ready. Click any button to start an async operation.";
            View.ShowProgress = false;
            View.ProgressPercentage = 0;
        }

        private async Task OnStartSimpleOperationAsync(CancellationToken cancellationToken)
        {
            View.StatusMessage = "Starting simple async operation...";
            _isBusy = true;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Simulate async operation. No catch: an unexpected failure propagates and the
                // dispatcher logs it; closing the window cancels the delay silently.
                await Task.Delay(2000, cancellationToken);

                stopwatch.Stop();
                View.StatusMessage = "Simple operation completed successfully!";
                View.ElapsedTime = $"Elapsed: {stopwatch.ElapsedMilliseconds}ms";

                Messages.ShowInfo("Simple async operation completed!", "Success");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task OnStartLongOperationAsync(CancellationToken cancellationToken)
        {
            View.StatusMessage = "Starting long-running operation...";
            _isBusy = true;

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var results = await PerformLongOperationAsync(_cancellationTokenSource.Token);

                stopwatch.Stop();
                View.ResultData = results;
                View.StatusMessage = $"Long operation completed! Processed {results.Count} items.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";

                Messages.ShowInfo($"Processed {results.Count} items successfully!", "Success");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The user clicked Cancel. (If the window is closing instead, the exception
                // propagates and the dispatcher ends the action silently.)
                stopwatch.Stop();
                View.StatusMessage = "Operation was cancelled by user.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";
                Messages.ShowWarning("Operation was cancelled.", "Cancelled");
            }
            finally
            {
                _isBusy = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task OnStartWithProgressAsync(CancellationToken cancellationToken)
        {
            View.StatusMessage = "Starting operation with progress tracking...";
            View.ShowProgress = true;
            View.ProgressPercentage = 0;
            _isBusy = true;

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    View.ProgressPercentage = percent;
                    View.StatusMessage = $"Processing... {percent}% complete";
                });

                var results = await PerformOperationWithProgressAsync(progress, _cancellationTokenSource.Token);

                stopwatch.Stop();
                View.ResultData = results;
                View.StatusMessage = $"Operation with progress completed! Processed {results.Count} items.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";
                View.ProgressPercentage = 100;

                Messages.ShowInfo($"Processed {results.Count} items successfully!", "Success");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The user clicked Cancel (closing the window ends the action silently).
                stopwatch.Stop();
                View.StatusMessage = "Operation was cancelled by user.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";
                Messages.ShowWarning("Operation was cancelled.", "Cancelled");
            }
            finally
            {
                View.ShowProgress = false;
                _isBusy = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task OnStartWithErrorAsync(CancellationToken cancellationToken)
        {
            View.StatusMessage = "Starting operation that will fail...";
            _isBusy = true;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Simulate an operation that fails
                await SimulateFailingOperationAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                // An EXPECTED failure is caught here, where the presenter can tell the user about it.
                // Anything else (or cancellation on window close) propagates: the dispatcher logs it
                // and re-enables the action, so no catch-all is needed in the handler.
                stopwatch.Stop();
                View.StatusMessage = $"Error occurred: {ex.Message}";
                View.ElapsedTime = $"Elapsed: {stopwatch.ElapsedMilliseconds}ms";

                Messages.ShowError(
                    $"Operation failed with error:\n\n{ex.Message}\n\nThis demonstrates proper async error handling.",
                    "Error Demonstration");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void OnCancel()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                View.StatusMessage = "Cancelling operation...";
                _cancellationTokenSource.Cancel();
            }
        }

        private void OnClear()
        {
            View.ResultData = Enumerable.Empty<string>();
            View.StatusMessage = "Results cleared. Ready for next operation.";
            View.ElapsedTime = string.Empty;
            View.ProgressPercentage = 0;
        }

        private static async Task SimulateFailingOperationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1000, cancellationToken);
            throw new InvalidOperationException("This is a simulated error to demonstrate error handling!");
        }

        private async Task<List<string>> PerformLongOperationAsync(CancellationToken cancellationToken)
        {
            var results = new List<string>();
            const int itemCount = 20;

            for (int i = 1; i <= itemCount; i++)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Simulate work
                await Task.Delay(300, cancellationToken);

                results.Add($"Item {i} processed at {DateTime.Now:HH:mm:ss}");
            }

            return results;
        }

        private async Task<List<string>> PerformOperationWithProgressAsync(
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<string>();
            const int itemCount = 50;

            for (int i = 1; i <= itemCount; i++)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Simulate work
                await Task.Delay(100, cancellationToken);

                results.Add($"Item {i} processed at {DateTime.Now:HH:mm:ss.fff}");

                // Report progress
                var percentComplete = (i * 100) / itemCount;
                progress?.Report(percentComplete);
            }

            return results;
        }
    }

    public static class AsyncActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Async");

        public static readonly ViewAction StartSimpleOperation = Factory.Create("StartSimple");
        public static readonly ViewAction StartLongOperation = Factory.Create("StartLong");
        public static readonly ViewAction StartWithProgress = Factory.Create("StartWithProgress");
        public static readonly ViewAction StartWithError = Factory.Create("StartWithError");
        public static readonly ViewAction Cancel = Factory.Create("Cancel");
        public static readonly ViewAction Clear = Factory.Create("Clear");
    }
}
