using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.WindowClosingDemo
{
    /// <summary>
    /// Static <see cref="ViewAction"/> keys for the Window Closing demo.
    /// </summary>
    public static class WindowClosingDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("WindowClosingDemo");

        public static readonly ViewAction Save = Factory.Create("Save");
        public static readonly ViewAction Cancel = Factory.Create("Cancel");
    }

    /// <summary>
    /// Presenter showcasing the framework's two-direction window-closing pattern.
    /// </summary>
    /// <remarks>
    /// Two directions of close are demonstrated:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Push</b>: <c>OnSave</c> / <c>OnCancel</c> finalize the dirty flag and raise
    ///     <see cref="IRequestClose{TResult}.CloseRequested"/> via the local <c>RaiseClose</c>
    ///     helper. The framework then closes the form.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Pull</b>: <c>OnViewClosing</c> handles external close requests (user clicks X,
    ///     system shutdown, etc.). It inspects <see cref="WindowClosingEventArgs.Reason"/> to
    ///     decide whether to prompt — system shutdowns must NEVER block.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The single-source-of-truth invariant: dirty-state checks live ONLY in
    /// <c>OnViewClosing</c>. Push-direction handlers prepare the dirty flag (set it to false)
    /// before they call <c>RaiseClose</c>, so the follow-up Closing event observes a clean
    /// state and lets the close proceed.
    /// </para>
    /// </remarks>
    public class WindowClosingDemoPresenter : WindowPresenterBase<IWindowClosingDemoView>,
                                               IRequestClose<string>
    {
        public event EventHandler<CloseRequestedEventArgs<string>> CloseRequested;

        private string _baseline;          // The text snapshot used to compute dirty state.
        private bool IsDirty => View.Text != _baseline;

        protected override void OnViewAttached()
        {
            // Pull-direction subscription. This is the ONLY place dirty state is checked
            // for closing — both user-driven closes and the framework's follow-up close
            // after RaiseClose flow through here.
            View.Closing += OnViewClosing;

            // Detect user edits so the Save button's CanExecute can refresh.
            View.EditChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
        }

        protected override void OnInitialize()
        {
            _baseline = "(type something here)";
            View.Text = _baseline;
            View.StatusMessage = "Ready. Edit the text, then Save / Cancel / close the window.";
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(WindowClosingDemoActions.Save, OnSave,
                canExecute: () => IsDirty);
            Dispatcher.Register(WindowClosingDemoActions.Cancel, OnCancel);
        }

        // ─── Push direction ──────────────────────────────────────────────────────────

        private void OnSave()
        {
            var saved = View.Text;
            _baseline = saved;            // Commit: dirty flag becomes false.
            View.StatusMessage = "Saving and closing…";
            RaiseClose(saved, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            _baseline = View.Text;        // Treat current as committed; skip the dirty prompt.
            View.StatusMessage = "Cancelled.";
            RaiseClose(null, InteractionStatus.Cancel);
        }

        private void RaiseClose(string result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<string>(result, status));

        // ─── Pull direction ──────────────────────────────────────────────────────────

        private void OnViewClosing(object sender, WindowClosingEventArgs args)
        {
            // Bypass the prompt on system-level shutdowns: the process is leaving anyway,
            // and a modal MessageBox here will hang application exit.
            if (args.Reason == CloseReason.SystemShutdown ||
                args.Reason == CloseReason.TaskManager)
            {
                return;
            }

            // Normal user-driven close (X / Alt+F4 / Form.Close()).
            if (IsDirty)
            {
                bool discard = Messages.ConfirmYesNo(
                    "You have unsaved changes. Discard and close?",
                    "Unsaved Changes");

                if (!discard)
                {
                    args.Cancel = true;   // Block the close.
                    View.StatusMessage = "Close cancelled. Continue editing.";
                }
            }
        }
    }
}
