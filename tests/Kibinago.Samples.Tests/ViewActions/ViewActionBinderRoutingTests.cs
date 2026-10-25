using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
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
        public void Unbind_StopsDispatchingAndRefreshing_RebindWorks()
        {
            using (var button = new Button())
            {
                int runs = 0;
                bool enabled = true;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => runs++, canExecute: () => enabled);

                var binder = new ViewActionBinder();
                binder.Add(Save, button);
                binder.Bind(dispatcher);

                binder.Unbind();
                binder.Unbind();                                // idempotent
                button.PerformClick();
                enabled = false;
                dispatcher.RaiseCanExecuteChanged();

                Assert.Equal(0, runs);
                Assert.True(button.Enabled);                    // no longer refreshed

                enabled = true;
                binder.Bind(dispatcher);
                button.PerformClick();
                Assert.Equal(1, runs);
            }
        }

        [Fact]
        public void SwitchingToEventOnlyBind_DetachesThePreviousDispatcher()
        {
            using (var button = new Button())
            {
                bool enabled = true;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => { }, canExecute: () => enabled);

                var binder = new ViewActionBinder();
                binder.Add(Save, button);
                binder.Bind(dispatcher);
                binder.Bind();                                   // event-only from now on

                enabled = false;
                dispatcher.RaiseCanExecuteChanged();

                Assert.True(button.Enabled);                     // the old dispatcher no longer drives the control
            }
        }

        [Fact]
        public void UnbindDuringARefresh_StopsTheRefresh()
        {
            var open = ViewAction.Create("BinderRouting.Open");
            using (var first = new Button())
            using (var second = new Button())
            {
                bool enabled = true;
                var dispatcher = new ViewActionDispatcher();
                dispatcher.Register(Save, () => { }, canExecute: () => enabled);
                dispatcher.Register(open, () => { }, canExecute: () => enabled);

                var binder = new ViewActionBinder();
                binder.Add(Save, first);
                binder.Add(open, second);
                binder.Bind(dispatcher);
                first.EnabledChanged += (s, e) => binder.Unbind();   // e.g. the presenter is disposed here

                enabled = false;
                dispatcher.RaiseCanExecuteChanged();                  // must not throw

                Assert.True(second.Enabled);                          // not refreshed after the unbind
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
