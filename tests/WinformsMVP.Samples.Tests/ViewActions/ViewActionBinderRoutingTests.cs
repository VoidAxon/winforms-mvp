using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Routing is decided by the Bind overload, never by who listens to ActionTriggered.
    /// </summary>
    public class ViewActionBinderRoutingTests
    {
        private static readonly ViewAction Save = ViewAction.Create("BinderRouting.Save");

        [Fact]
        public void BindToDispatcher_WithActionTriggeredSubscriber_StillDispatches()
        {
            using (var button = new Button())
            {
                var order = new List<string>();
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => order.Add("handler"));

                var binder = new ViewActionBinder();
                binder.Add(Save, button);
                binder.ActionTriggered += (s, e) => order.Add("notification:" + e.ActionKey);   // e.g. a status bar
                binder.Bind(dispatcher);

                button.PerformClick();

                Assert.Equal(new[] { "handler", "notification:BinderRouting.Save" }, order);
            }
        }

        [Fact]
        public void BindToDispatcher_ThrowingNotificationSubscriber_DoesNotPreventTheDispatch()
        {
            using (var button = new Button())
            {
                bool handled = false;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => handled = true);

                var binder = new ViewActionBinder();
                binder.Add(Save, button);
                binder.ActionTriggered += (s, e) => throw new InvalidOperationException("listener bug");
                binder.Bind(dispatcher);

                Assert.Throws<InvalidOperationException>(() => button.PerformClick());
                Assert.True(handled);
            }
        }

        [Fact]
        public void EventOnlyBind_RaisesActionTriggeredOnly()
        {
            using (var button = new Button())
            {
                ViewAction? raised = null;
                var binder = new ViewActionBinder();
                binder.Add(Save, button);
                binder.ActionTriggered += (s, e) => raised = e.ActionKey;
                binder.Bind();

                button.PerformClick();

                Assert.Equal(Save, raised);
            }
        }
    }
}
