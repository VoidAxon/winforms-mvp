using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.AsyncDemo
{
    /// <summary>
    /// Demonstrates async/await patterns in MVP:
    /// - Long-running operations with progress tracking
    /// - Cancellation support
    /// - Error handling in async methods
    /// - UI state management during async operations
    /// </summary>
    public class AsyncDemoPresenter : WindowPresenterBase<IAsyncDemoView>
    {
        private CancellationTokenSource _cancellationTokenSource;

        protected override void OnViewAttached()
        {
            // Nothing to subscribe to
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(
                AsyncActions.StartSimpleOperation,
                OnStartSimpleOperation,
                canExecute: () => !View.IsOperationRunning);

            _dispatcher.Register(
                AsyncActions.StartLongOperation,
                OnStartLongOperation,
                canExecute: () => !View.IsOperationRunning);

            _dispatcher.Register(
                AsyncActions.StartWithProgress,
                OnStartWithProgress,
                canExecute: () => !View.IsOperationRunning);

            _dispatcher.Register(
                AsyncActions.StartWithError,
                OnStartWithError,
                canExecute: () => !View.IsOperationRunning);

            _dispatcher.Register(
                AsyncActions.Cancel,
                OnCancel,
                canExecute: () => View.IsOperationRunning);

            _dispatcher.Register(
                AsyncActions.Clear,
                OnClear);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.StatusMessage = "Ready. Click any button to start an async operation.";
            View.ShowProgress = false;
            View.ProgressPercentage = 0;
            View.IsOperationRunning = false;
        }

        private async void OnStartSimpleOperation()
        {
            View.StatusMessage = "Starting simple async operation...";
            View.IsOperationRunning = true;
            _dispatcher.RaiseCanExecuteChanged();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Simulate async operation
                await Task.Delay(2000);

                stopwatch.Stop();
                View.StatusMessage = "Simple operation completed successfully!";
                View.ElapsedTime = $"Elapsed: {stopwatch.ElapsedMilliseconds}ms";

                Messages.ShowInfo("Simple async operation completed!", "Success");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                View.StatusMessage = $"Error: {ex.Message}";
                Messages.ShowError($"Operation failed: {ex.Message}", "Error");
            }
            finally
            {
                View.IsOperationRunning = false;
                _dispatcher.RaiseCanExecuteChanged();
            }
        }

        private async void OnStartLongOperation()
        {
            View.StatusMessage = "Starting long-running operation...";
            View.IsOperationRunning = true;
            _dispatcher.RaiseCanExecuteChanged();

            _cancellationTokenSource = new CancellationTokenSource();
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
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                View.StatusMessage = "Operation was cancelled by user.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";
                Messages.ShowWarning("Operation was cancelled.", "Cancelled");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                View.StatusMessage = $"Error: {ex.Message}";
                Messages.ShowError($"Operation failed: {ex.Message}", "Error");
            }
            finally
            {
                View.IsOperationRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _dispatcher.RaiseCanExecuteChanged();
            }
        }

        private async void OnStartWithProgress()
        {
            View.StatusMessage = "Starting operation with progress tracking...";
            View.ShowProgress = true;
            View.ProgressPercentage = 0;
            View.IsOperationRunning = true;
            _dispatcher.RaiseCanExecuteChanged();

            _cancellationTokenSource = new CancellationTokenSource();
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
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                View.StatusMessage = "Operation was cancelled by user.";
                View.ElapsedTime = $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s";
                Messages.ShowWarning("Operation was cancelled.", "Cancelled");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                View.StatusMessage = $"Error: {ex.Message}";
                Messages.ShowError($"Operation failed: {ex.Message}", "Error");
            }
            finally
            {
                View.ShowProgress = false;
                View.IsOperationRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _dispatcher.RaiseCanExecuteChanged();
            }
        }

        private async void OnStartWithError()
        {
            View.StatusMessage = "Starting operation that will fail...";
            View.IsOperationRunning = true;
            _dispatcher.RaiseCanExecuteChanged();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Simulate operation that fails
                await Task.Delay(1000);
                throw new InvalidOperationException("This is a simulated error to demonstrate error handling!");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                View.StatusMessage = $"Error occurred: {ex.Message}";
                View.ElapsedTime = $"Elapsed: {stopwatch.ElapsedMilliseconds}ms";

                Messages.ShowError(
                    $"Operation failed with error:\n\n{ex.Message}\n\nThis demonstrates proper async error handling.",
                    "Error Demonstration");
            }
            finally
            {
                View.IsOperationRunning = false;
                _dispatcher.RaiseCanExecuteChanged();
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
