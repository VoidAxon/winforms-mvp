using WinformsMVP.Common;
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
    ///     <b>Push</b>: <c>OnSave</c> / <c>OnCancel</c> finalize the dirty flag and call
    ///     <c>this.RequestClose(...)</c>. The framework then closes the form.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Pull</b>: <see cref="CanClose"/> handles external close requests (user clicks X,
    ///     system shutdown, etc.). It inspects the <see cref="CloseReason"/> to decide whether
    ///     to prompt — system shutdowns must NEVER block.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The single-source-of-truth invariant: dirty-state checks live ONLY in
    /// <see cref="CanClose"/>. Push-direction handlers prepare the dirty flag (set it to false)
    /// before they request close, so the follow-up close check observes a clean state and lets
    /// the close proceed.
    /// </para>
    /// </remarks>
    public class WindowClosingDemoPresenter : WindowPresenterBase<IWindowClosingDemoView>,
                                               IRequestClose<string>
    {
        private string _baseline;          // The text snapshot used to compute dirty state.
        private bool IsDirty => View.Text != _baseline;

        protected override void OnViewAttached()
            // Detect user edits so the Save button's CanExecute can refresh.
            => View.EditChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();

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

        // ─── Pull direction ──────────────────────────────────────────────────────────

        protected override bool CanClose(CloseReason reason)
        {
            // Bypass the prompt on system-level shutdowns: the process is leaving anyway,
            // and a modal MessageBox here will hang application exit.
            if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
                return true;

            // Normal user-driven close (X / Alt+F4 / Form.Close()).
            if (!IsDirty) return true;

            bool discard = Messages.ConfirmYesNo(
                "You have unsaved changes. Discard and close?",
                "Unsaved Changes");

            if (!discard)
                View.StatusMessage = "Close cancelled. Continue editing.";

            return discard;
        }

        // ─── Push direction ──────────────────────────────────────────────────────────

        private void OnSave()
        {
            var saved = View.Text;
            _baseline = saved;            // Commit: dirty flag becomes false.
            View.StatusMessage = "Saving and closing…";
            this.RequestClose(saved, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            _baseline = View.Text;        // Treat current as committed; skip the dirty prompt.
            View.StatusMessage = "Cancelled.";
            this.RequestClose(null, InteractionStatus.Cancel);
        }
    }
}
