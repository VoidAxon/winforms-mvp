using System;
using System.Windows.Forms;
using System.Windows.Input;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Typed value bindings (<c>Add(ViewAction&lt;T&gt;, control, c =&gt; T)</c>) and the ICommand
    /// view of an action (<c>binder.CommandFor(key)</c>), for WinForms' own Command property
    /// (.NET 7+) or any ICommand consumer.
    /// </summary>
    public class ViewActionBinderCommandTests
    {
        private static readonly ViewAction Save = ViewAction.Create("BinderCommandTest.Save");
        private static readonly ViewAction<int> Zoom = ViewAction.Create<int>("BinderCommandTest.Zoom");

        // ---- typed value binding ---------------------------------------------------------

        [Fact]
        public void TypedAdd_DispatchesTheTypedPayload()
        {
            using (var numeric = new NumericUpDown { Maximum = 500 })
            {
                int received = 0;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Zoom, z => received = z);

                var binder = new ViewActionBinder();
                binder.Add(Zoom, numeric, n => (int)n.Value);
                binder.Bind(dispatcher);

                numeric.Value = 150;

                Assert.Equal(150, received);
            }
        }

        [Fact]
        public void TypedAdd_Explicit_RaisesValueCarryingEventArgs()
        {
            using (var numeric = new NumericUpDown { Maximum = 500 })
            {
                object value = null;
                var binder = new ViewActionBinder();
                binder.Add(Zoom, numeric, n => (int)n.Value);
                binder.ActionTriggered += (s, e) => value = (e as IActionRequestEventArgsWithValue)?.GetValue();
                binder.Bind();

                numeric.Value = 80;

                Assert.Equal(80, value);
            }
        }

        // ---- ICommand ---------------------------------------------------------------------

        [Fact]
        public void CommandFor_ReturnsTheSameInstanceForAKey()
        {
            var binder = new ViewActionBinder();

            Assert.Same(binder.CommandFor(Save), binder.CommandFor(Save));
            Assert.Same(binder.CommandFor(Zoom), binder.CommandFor(Zoom));
        }

        [Fact]
        public void Command_BeforeBind_CannotExecute_AndExecuteDoesNothing()
        {
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);

            Assert.False(command.CanExecute(null));
            command.Execute(null);   // no dispatcher yet: ignored, no exception
        }

        [Fact]
        public void Command_Execute_DispatchesTheAction()
        {
            int calls = 0;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => calls++);
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);
            binder.Bind(dispatcher);

            command.Execute(null);

            Assert.Equal(1, calls);
        }

        [Fact]
        public void Command_CanExecute_FollowsTheDispatcher_AndRaisesCanExecuteChanged()
        {
            bool allowed = false;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => { }, canExecute: () => allowed);
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);
            int changed = 0;
            command.CanExecuteChanged += (s, e) => changed++;
            binder.Bind(dispatcher);
            changed = 0;

            Assert.False(command.CanExecute(null));
            allowed = true;
            dispatcher.RaiseCanExecuteChanged();

            Assert.True(command.CanExecute(null));
            Assert.True(changed >= 1);
        }

        [Fact]
        public void TypedCommand_PassesCommandParameterAsPayload()
        {
            int received = 0;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Zoom, z => received = z);
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Zoom);
            binder.Bind(dispatcher);

            command.Execute(125);

            Assert.Equal(125, received);
        }

        [Fact]
        public void TypedCommand_ParameterOfTheWrongType_Throws()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Zoom, z => { });
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Zoom);
            binder.Bind(dispatcher);

            Assert.Throws<ArgumentException>(() => command.Execute("not an int"));
        }

        [Fact]
        public void TypedCommand_KeepsItsPayloadRules_WhenTheUntypedCommandWasCreatedFirst()
        {
            int? received = null;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Zoom, z => received = z);
            var binder = new ViewActionBinder();
            var untyped = binder.CommandFor(Zoom.Key);
            var typed = binder.CommandFor(Zoom);
            binder.Bind(dispatcher);

            typed.Execute(null);   // default(int)

            Assert.NotSame(untyped, typed);
            Assert.Equal(0, received);
            Assert.Throws<ArgumentException>(() => typed.Execute("not an int"));
        }

        [Fact]
        public void Command_ExplicitPattern_RaisesActionTriggered()
        {
            ViewAction? triggered = null;
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);
            binder.ActionTriggered += (s, e) => triggered = e.ActionKey;
            binder.Bind();   // explicit: no dispatcher, events only

            command.Execute(null);

            Assert.Equal(Save, triggered);
        }

        [Fact]
        public void Command_ExplicitPattern_CanExecute_SoItsControlStaysEnabled()
        {
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);
            int changed = 0;
            command.CanExecuteChanged += (s, e) => changed++;

            binder.Bind();

            Assert.True(command.CanExecute(null));
            Assert.True(changed >= 1);
        }

        [Fact]
        public void Command_AfterUnbind_CannotExecute_AndDoesNotDispatch()
        {
            int calls = 0;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => calls++);
            var binder = new ViewActionBinder();
            var command = binder.CommandFor(Save);
            binder.Bind(dispatcher);

            binder.Unbind();
            command.Execute(null);

            Assert.False(command.CanExecute(null));
            Assert.Equal(0, calls);
        }

#if NET7_0_OR_GREATER
        [Fact]
        public void WinFormsButtonCommand_ClickDispatches()
        {
            using (var button = new Button())
            {
                int calls = 0;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => calls++);
                var binder = new ViewActionBinder();
                button.Command = binder.CommandFor(Save);
                binder.Bind(dispatcher);

                button.PerformClick();

                Assert.Equal(1, calls);
            }
        }
#endif
    }
}
