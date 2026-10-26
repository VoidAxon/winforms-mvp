using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// V-25: text boxes trigger on TextChanged; a control with no strategy is rejected at Add instead of
    /// silently waiting for a click. V-24: controls added after Bind work; Remove and disposal unbind.
    /// </summary>
    public class ViewActionBinderDynamicTests
    {
        private static readonly ViewAction Run = ViewAction.Create("BinderDynamic.Run");
        private static readonly ViewAction Other = ViewAction.Create("BinderDynamic.Other");
        private static readonly ViewAction<string> SetText = ViewAction.Create<string>("BinderDynamic.SetText");

        private sealed class CustomEditor : Control { }        // a third-party editor deriving from Control
        private sealed class EditorPanel : Panel { }            // derives from a click control: not a click control itself

        private static (ViewActionDispatcher dispatcher, List<object> calls) Dispatcher(Func<bool> canRun = null)
        {
            var calls = new List<object>();
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Run, () => calls.Add("run"), canExecute: canRun);
            dispatcher.Register(Other, () => calls.Add("other"));
            dispatcher.Register(SetText, (string v) => calls.Add(v));
            return (dispatcher, calls);
        }

        // ---- V-25: text boxes and the click fallback ----

        [Fact]
        public void TextBox_TriggersOnTextChanged_WithItsValue()
        {
            using (var text = new TextBox())
            {
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();
                binder.Add(SetText, text, t => t.Text);
                binder.Bind(dispatcher);

                text.Text = "abc";

                Assert.Equal(new object[] { "abc" }, calls);
            }
        }

        [Fact]
        public void RichAndMaskedTextBoxes_AreTextBoxesToo()
        {
            using (var rich = new RichTextBox())
            using (var masked = new MaskedTextBox())
            {
                // A RichTextBox raises TextChanged only once it has a window handle (on a shown form it has one).
                rich.CreateControl();
                var _ = rich.Handle;
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();
                binder.Add(Run, rich);
                binder.Add(Other, masked);
                binder.Bind(dispatcher);

                rich.Text = "x";
                masked.Text = "y";

                Assert.Equal(new object[] { "run", "other" }, calls);
            }
        }

        [Fact]
        public void ControlWithoutAStrategy_IsRejectedAtAdd_WithAHint()
        {
            using (var editor = new CustomEditor { Name = "editor" })
            {
                var binder = new ViewActionBinder();
                var ex = Assert.Throws<InvalidOperationException>(() => binder.Add(Run, editor));
                Assert.Contains("RegisterStrategy", ex.Message);
                Assert.Contains("editor", ex.Message);
            }
        }

        [Fact]
        public void SubclassOfAClickControl_IsNotAClickControl()
        {
            using (var panel = new EditorPanel())
                Assert.Throws<InvalidOperationException>(() => new ViewActionBinder().Add(Run, panel));
        }

        [Fact]
        public void ClickControls_StillTriggerOnClick()
        {
            using (var label = new Label())
            using (var picture = new PictureBox())
            using (var panel = new Panel())
            {
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();
                binder.Add(Run, label, picture, panel);
                binder.Bind(dispatcher);

                InvokeClick(label);
                InvokeClick(picture);
                InvokeClick(panel);

                Assert.Equal(3, calls.Count);
            }
        }

        [Fact]
        public void RegisteredStrategy_MakesACustomControlBindable()
        {
            using (var editor = new CustomEditor())
            {
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();
                binder.RegisterStrategy<CustomEditor>((c, h) => c.TextChanged += h, (c, h) => c.TextChanged -= h);
                binder.Add(Run, editor);
                binder.Bind(dispatcher);

                editor.Text = "typed";

                Assert.Equal(new object[] { "run" }, calls);
            }
        }

        [Fact]
        public void ToolStripEditors_AreNotClicks_SeparatorsAreRejected()
        {
            using (var strip = new ToolStrip())
            {
                var text = new ToolStripTextBox();
                var separator = new ToolStripSeparator();
                strip.Items.AddRange(new ToolStripItem[] { text, separator });
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();

                binder.Add(Run, text);
                Assert.Throws<InvalidOperationException>(() => binder.Add(Other, separator));
                binder.Bind(dispatcher);
                text.Text = "q";

                Assert.Equal(new object[] { "run" }, calls);
            }
        }

        [Fact]
        public void ARejectedControl_LeavesNothingHalfAdded()
        {
            using (var button = new Button())
            using (var editor = new CustomEditor())
            {
                var binder = new ViewActionBinder();
                Assert.Throws<InvalidOperationException>(() => binder.Add(Run, button, editor));
                binder.Add(Other, button);   // the button was not recorded by the failed call
            }
        }

        [Fact]
        public void TheSameControlTwiceInOneCall_IsRejected()
        {
            using (var button = new Button())
                Assert.Throws<InvalidOperationException>(() => new ViewActionBinder().Add(Run, button, button));
        }

        // ---- V-24: Add after Bind, Remove, disposal ----

        [Fact]
        public void AddAfterBind_TriggersAtOnce_AndFollowsCanExecute()
        {
            using (var early = new Button())
            using (var late = new Button())
            {
                bool enabled = false;
                var (dispatcher, calls) = Dispatcher(() => enabled);
                var binder = new ViewActionBinder();
                binder.Add(Other, early);
                binder.Bind(dispatcher);

                binder.Add(Run, late);                 // e.g. a child view creates its choices in OnInitialize
                Assert.False(late.Enabled);            // canExecute applied at once

                enabled = true;
                dispatcher.RaiseCanExecuteChanged();
                late.PerformClick();
                early.PerformClick();

                Assert.Equal(new object[] { "run", "other" }, calls);   // each fires once
            }
        }

        [Fact]
        public void AddAfterEventOnlyBind_RaisesActionTriggered()
        {
            using (var late = new Button())
            {
                var raised = new List<ViewAction>();
                var binder = new ViewActionBinder();
                binder.ActionTriggered += (s, e) => raised.Add(e.ActionKey);
                binder.Bind();

                binder.Add(Run, late);
                late.PerformClick();

                Assert.Equal(new[] { Run }, raised);
            }
        }

        [Fact]
        public void Remove_StopsTriggeringAndRefreshing_AndAllowsReAddingToAnotherAction()
        {
            using (var button = new Button())
            using (var sibling = new Button())
            {
                bool enabled = true;
                var (dispatcher, calls) = Dispatcher(() => enabled);
                var binder = new ViewActionBinder();
                binder.Add(Run, button, sibling);
                binder.Bind(dispatcher);

                binder.Remove(button);
                binder.Remove(button);                 // twice: harmless
                button.PerformClick();
                enabled = false;
                dispatcher.RaiseCanExecuteChanged();

                Assert.Empty(calls);
                Assert.True(button.Enabled);           // no longer managed
                Assert.False(sibling.Enabled);         // the sibling still is

                binder.Add(Other, button);
                button.PerformClick();
                Assert.Equal(new object[] { "other" }, calls);
            }
        }

        [Fact]
        public void DisposedControl_IsRemovedAutomatically_WhetherBoundOrNot()
        {
            var (dispatcher, calls) = Dispatcher();
            var binder = new ViewActionBinder();
            var before = new Button();
            binder.Add(Run, before);
            before.Dispose();                          // disposed before Bind
            binder.Bind(dispatcher);

            var after = new Button();
            binder.Add(Run, after);
            after.Dispose();                           // disposed while bound
            dispatcher.RaiseCanExecuteChanged();       // must not touch disposed controls

            Assert.Equal(0, binder.BindingCount);
        }

        [Fact]
        public void BoundToolStripComboBox_CanBeDisposed()
        {
            var (dispatcher, calls) = Dispatcher();
            var binder = new ViewActionBinder();
            var combo = new ToolStripComboBox();       // the host clears its ComboBox before Disposed
            binder.Add(Run, combo);
            binder.Bind(dispatcher);

            combo.Dispose();

            Assert.Equal(0, binder.BindingCount);
        }

        [Fact]
        public void ADisposedControl_CannotBeAdded()
        {
            var button = new Button();
            button.Dispose();
            Assert.Throws<ObjectDisposedException>(() => new ViewActionBinder().Add(Run, button));
        }

        [Fact]
        public void UnbindThenAddThenBind_SubscribesEverythingOnce()
        {
            using (var a = new Button())
            using (var b = new Button())
            {
                var (dispatcher, calls) = Dispatcher();
                var binder = new ViewActionBinder();
                binder.Add(Run, a);
                binder.Bind(dispatcher);
                binder.Unbind();

                binder.Add(Other, b);                  // recorded while unbound
                b.PerformClick();
                Assert.Empty(calls);

                binder.Bind(dispatcher);
                a.PerformClick();
                b.PerformClick();
                Assert.Equal(new object[] { "run", "other" }, calls);
            }
        }

        [Fact]
        public void RemovingAControlFromCanExecute_DuringARefresh_IsSafe()
        {
            using (var a = new Button())
            using (var b = new Button())
            {
                ViewActionBinder binder = null;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Run, () => { }, canExecute: () => { binder.Remove(b); return true; });
                binder = new ViewActionBinder();
                binder.Add(Run, a, b);

                binder.Bind(dispatcher);               // refresh runs canExecute, which removes b
                dispatcher.RaiseCanExecuteChanged();
            }
        }

        private static void InvokeClick(Control control)
            => typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(control, new object[] { EventArgs.Empty });
    }
}
