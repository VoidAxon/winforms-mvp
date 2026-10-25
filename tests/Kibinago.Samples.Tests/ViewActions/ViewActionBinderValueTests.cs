using System;
using System.Windows.Forms;
using Kibinago.Common.Events;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// Covers the value-carrying binding overload: <c>Add(action, control, valueSelector)</c>.
    /// A bound control's current value is read at trigger time and flows to the handler as the
    /// dispatch payload (implicit pattern) or as a value-carrying <see cref="ActionRequestEventArgs{T}"/>
    /// (explicit pattern), with no presenter-side or control-name plumbing.
    /// </summary>
    public class ViewActionBinderValueTests
    {
        private static readonly ViewAction ValueAction = ViewAction.Create("BinderValueTest.Value");
        private static readonly ViewAction PlainAction = ViewAction.Create("BinderValueTest.Plain");

        [Fact]
        public void Implicit_CheckBoxValueSelector_DispatchesControlValueAsPayload()
        {
            using (var checkBox = new CheckBox())
            {
                bool received = false;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register<bool>(ValueAction, v => received = v);

                var binder = new ViewActionBinder();
                binder.Add(ValueAction, checkBox, c => c.Checked);
                binder.Bind(dispatcher);

                checkBox.Checked = true; // raises CheckedChanged; selector reads the current value

                Assert.True(received);
            }
        }

        [Fact]
        public void Implicit_ButtonValueSelector_DispatchesComputedPayload()
        {
            using (var button = new Button())
            {
                string received = null;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register<string>(ValueAction, v => received = v);

                var binder = new ViewActionBinder();
                binder.Add(ValueAction, button, _ => "hello");
                binder.Bind(dispatcher);

                button.PerformClick();

                Assert.Equal("hello", received);
            }
        }

        [Fact]
        public void Explicit_ValueSelector_RaisesValueCarryingEventArgs()
        {
            using (var checkBox = new CheckBox { Checked = true })
            {
                ActionRequestEventArgs captured = null;
                var binder = new ViewActionBinder();
                binder.Add(ValueAction, checkBox, c => c.Checked);
                binder.ActionTriggered += (s, e) => captured = e;
                binder.Bind(); // event-only (explicit)

                checkBox.Checked = false; // selector reads the now-current value

                var withValue = Assert.IsAssignableFrom<IActionRequestEventArgsWithValue>(captured);
                Assert.Equal(false, withValue.GetValue());
            }
        }

        [Fact]
        public void Explicit_NoValueSelector_RaisesPlainEventArgs()
        {
            using (var button = new Button())
            {
                ActionRequestEventArgs captured = null;
                var binder = new ViewActionBinder();
                binder.Add(PlainAction, button);
                binder.ActionTriggered += (s, e) => captured = e;
                binder.Bind();

                button.PerformClick();

                Assert.NotNull(captured);
                Assert.IsType<ActionRequestEventArgs>(captured); // exact base type, no value carried
                Assert.False(captured is IActionRequestEventArgsWithValue);
            }
        }

        [Fact]
        public void Implicit_NoValueSelector_DispatchesNullPayload_BackwardCompatible()
        {
            using (var button = new Button())
            {
                bool ran = false;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(PlainAction, () => ran = true);

                var binder = new ViewActionBinder();
                binder.Add(PlainAction, button);
                binder.Bind(dispatcher);

                button.PerformClick();

                Assert.True(ran);
            }
        }

        [Fact]
        public void Default_NumericUpDownValueChanged_DispatchesValueWithoutRegisterStrategy()
        {
            using (var numeric = new NumericUpDown { Minimum = 0, Maximum = 100 })
            {
                decimal received = -1;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register<decimal>(ValueAction, v => received = v);

                var binder = new ViewActionBinder();
                binder.Add(ValueAction, numeric, n => n.Value); // no RegisterStrategy needed
                binder.Bind(dispatcher);

                numeric.Value = 42; // raises ValueChanged via the default strategy

                Assert.Equal(42m, received);
            }
        }

        [Fact]
        public void Default_DateTimePickerValueChanged_DispatchesValueWithoutRegisterStrategy()
        {
            using (var picker = new DateTimePicker())
            {
                DateTime received = default;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register<DateTime>(ValueAction, v => received = v);

                var binder = new ViewActionBinder();
                binder.Add(ValueAction, picker, p => p.Value); // no RegisterStrategy needed
                binder.Bind(dispatcher);

                var newDate = new DateTime(2030, 1, 15);
                picker.Value = newDate; // raises ValueChanged via the default strategy

                Assert.Equal(newDate, received);
            }
        }

        [Fact]
        public void Default_MonthCalendarDateChanged_DispatchesValueWithoutRegisterStrategy()
        {
            using (var calendar = new MonthCalendar())
            {
                DateTime received = default;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register<DateTime>(ValueAction, v => received = v);

                var binder = new ViewActionBinder();
                binder.Add(ValueAction, calendar, c => c.SelectionStart); // no RegisterStrategy needed
                binder.Bind(dispatcher);

                var newDate = new DateTime(2030, 2, 1);
                calendar.SetDate(newDate); // raises DateChanged (DateRangeEventHandler) via default strategy

                Assert.Equal(newDate, received);
            }
        }

        [Fact]
        public void SubscribeStrategy_UnbindStopsTriggering()
        {
            using (var calendar = new MonthCalendar())
            {
                int count = 0;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(PlainAction, () => count++);

                var binder = new ViewActionBinder();
                binder.Add(PlainAction, calendar);
                binder.Bind(dispatcher);

                calendar.SetDate(new DateTime(2030, 2, 1));
                int afterBind = count;
                Assert.True(afterBind >= 1, "DateChanged should fire while bound");

                binder.Unbind();
                calendar.SetDate(new DateTime(2030, 3, 1)); // must not dispatch after Unbind

                Assert.Equal(afterBind, count);
            }
        }
    }
}
