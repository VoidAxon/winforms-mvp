using System;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// A button bound to an action never closes a modal window by its own <c>DialogResult</c>: WinForms
    /// sets the form's DialogResult before <c>Click</c> (and <c>Form.CancelButton</c> silently gives its
    /// button <c>DialogResult.Cancel</c>), so the modal loop would close the window even when the handler
    /// decided not to. The binder resets a bound button's DialogResult to None when it subscribes.
    /// </summary>
    public class ViewActionBinderDialogResultTests
    {
        private static readonly ViewAction Cancel = ViewAction.Create("BinderDialogResultTest.Cancel");
        private static readonly ViewAction Other = ViewAction.Create("BinderDialogResultTest.Other");

        private sealed class EscForm : Form
        {
            public bool PressEscape() => ProcessDialogKey(Keys.Escape);
        }

        [Fact]
        public void CancelButton_BoundWithDispatcher_DialogResultResetToNone()
        {
            using (var form = new Form())
            {
                var button = new Button();
                form.Controls.Add(button);
                form.CancelButton = button;                     // WinForms: DialogResult -> Cancel
                Assert.Equal(DialogResult.Cancel, button.DialogResult);

                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Cancel, () => { });
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);
                binder.Bind(dispatcher);

                Assert.Equal(DialogResult.None, button.DialogResult);
            }
        }

        [Fact]
        public void EventOnlyBind_ResetsToo()
        {
            using (var button = new Button { DialogResult = DialogResult.OK })
            {
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);
                binder.Bind();

                Assert.Equal(DialogResult.None, button.DialogResult);
            }
        }

        [Fact]
        public void DialogResultSetBetweenAddAndBind_IsResetAtBind()
        {
            using (var form = new Form())
            {
                var button = new Button();
                form.Controls.Add(button);
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);
                form.CancelButton = button;                     // after Add, before Bind

                binder.Bind(new ViewActionDispatcher());

                Assert.Equal(DialogResult.None, button.DialogResult);
            }
        }

        [Fact]
        public void AddAfterBind_ResetsAtOnce()
        {
            using (var button = new Button { DialogResult = DialogResult.Cancel })
            {
                var binder = new ViewActionBinder();
                binder.Bind(new ViewActionDispatcher());
                binder.Add(Cancel, button);

                Assert.Equal(DialogResult.None, button.DialogResult);
            }
        }

        [Fact]
        public void AddBeforeBind_LeavesTheButtonUntilBind()
        {
            using (var button = new Button { DialogResult = DialogResult.Cancel })
            {
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);

                Assert.Equal(DialogResult.Cancel, button.DialogResult);   // only subscribing changes it
            }
        }

        [Fact]
        public void UnboundButton_IsUntouched()
        {
            using (var bound = new Button())
            using (var plain = new Button { DialogResult = DialogResult.Cancel })
            {
                var binder = new ViewActionBinder();
                binder.Add(Other, bound);
                binder.Bind(new ViewActionDispatcher());

                Assert.Equal(DialogResult.Cancel, plain.DialogResult);
            }
        }

        [Fact]
        public void Unbind_DoesNotRestoreTheDialogResult()
        {
            using (var button = new Button { DialogResult = DialogResult.Cancel })
            {
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);
                binder.Bind(new ViewActionDispatcher());
                binder.Unbind();

                Assert.Equal(DialogResult.None, button.DialogResult);
            }
        }

        [Fact]
        public void ModalWindow_EscapeOnABoundCancelButton_HandlerDecidesNotToClose_StaysOpen()
        {
            using (var form = new EscForm())
            {
                var button = new Button();
                form.Controls.Add(button);
                form.CancelButton = button;

                int handled = 0;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Cancel, () => handled++);       // e.g. "discard?" answered No: no RequestClose
                var binder = new ViewActionBinder();
                binder.Add(Cancel, button);
                binder.Bind(dispatcher);

                bool openAfterEscape = false;
                form.Shown += (s, e) =>
                {
                    form.PressEscape();                             // Esc clicks the CancelButton
                    form.BeginInvoke(new Action(() =>
                    {
                        openAfterEscape = form.Visible;             // the modal loop has run in between
                        form.Close();                               // end the test either way
                    }));
                };

                form.ShowDialog();

                Assert.Equal(1, handled);
                Assert.True(openAfterEscape);
            }
        }
    }
}
