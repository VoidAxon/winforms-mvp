using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.WindowClosingDemo
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
    /// Presenter showcasing the framework's window-closing pattern: one gate, two initiators.
    /// </summary>
    /// <remarks>
    /// Two initiators of a close are demonstrated:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Push</b>: <c>OnSave</c> / <c>OnCancel</c> finalize the dirty flag and call
    ///     <c>RequestClose(...)</c>. The close goes through <see cref="CanClose"/> like X.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Pull</b>: user clicks X / Alt+F4, or the owner closes. System shutdown and Task
    ///     Manager closes are allowed by the framework without asking <see cref="CanClose"/>.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The single-source-of-truth invariant: dirty-state checks live ONLY in
    /// <see cref="CanClose"/>. Push-direction handlers prepare the dirty flag (set it to false)
    /// before they request close, so the gate that close goes through observes a clean state and
    /// lets it proceed without prompting. A plain <c>RequestClose(InteractionStatus.Cancel)</c>
    /// without that step would prompt like X — what a "Close" button wants.
    /// </para>
    /// </remarks>
    public class WindowClosingDemoPresenter : WindowPresenterBase<IWindowClosingDemoView>
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
            // System shutdown / Task Manager never reach here (the framework allows them without asking).
            // Every other close: X / Alt+F4, owner closing, and this presenter's RequestClose.
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
            RequestClose(saved, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            _baseline = View.Text;        // Discard: treat current as committed so the gate does not prompt.
            View.StatusMessage = "Cancelled.";
            RequestClose(InteractionStatus.Cancel);
        }
    }
}
