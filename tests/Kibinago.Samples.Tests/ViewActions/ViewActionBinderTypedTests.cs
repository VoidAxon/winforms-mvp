using System.Windows.Forms;
using Kibinago.Common.Events;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// Typed value bindings: <c>Add(ViewAction&lt;T&gt;, control, c =&gt; T)</c>.
    /// </summary>
    public class ViewActionBinderTypedTests
    {
        private static readonly ViewAction<int> Zoom = ViewAction.Create<int>("BinderTypedTest.Zoom");

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
    }
}
