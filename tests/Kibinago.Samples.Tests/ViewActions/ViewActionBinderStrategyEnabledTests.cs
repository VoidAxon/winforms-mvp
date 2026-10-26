using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// A strategy can say how a component is enabled, so a third-party toolbar command (a Component, not a
    /// Control or ToolStripItem) follows canExecute like a button.
    /// </summary>
    public class ViewActionBinderStrategyEnabledTests
    {
        private static readonly ViewAction Run = ViewAction.Create("StrategyEnabled.Run");

        /// <summary>Like a third-party toolbar command: its own click delegate, its own Enabled.</summary>
        private sealed class ToolCommand : Component
        {
            public delegate void ToolClickHandler(object sender, ToolClickArgs e);
            public sealed class ToolClickArgs : EventArgs { }

            public bool Enabled { get; set; } = true;
            public int EnabledWrites { get; private set; }
            public event ToolClickHandler Click;
            public void PerformClick() => Click?.Invoke(this, new ToolClickArgs());
            public void SetEnabled(bool value) { EnabledWrites++; Enabled = value; }
        }

        private sealed class CustomButton : Control { public bool Shown = true; }

        private static void RegisterToolCommand(ViewActionBinder binder, bool withEnabled)
            => binder.RegisterStrategy<ToolCommand>((c, h) =>
                {
                    ToolCommand.ToolClickHandler adapter = (s, e) => h(s, e);
                    c.Click += adapter;
                    return () => c.Click -= adapter;
                },
                withEnabled ? (c, enabled) => c.SetEnabled(enabled) : (Action<ToolCommand, bool>)null);

        [Fact]
        public void WithSetEnabled_TheComponentFollowsCanExecute()
        {
            bool canRun = false;
            int runs = 0;
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Run, () => runs++, canExecute: () => canRun);
            var command = new ToolCommand();
            var binder = new ViewActionBinder();
            RegisterToolCommand(binder, withEnabled: true);
            binder.Add(Run, command);

            binder.Bind(dispatcher);
            Assert.False(command.Enabled);

            canRun = true;
            dispatcher.RaiseCanExecuteChanged();
            Assert.True(command.Enabled);

            command.PerformClick();
            Assert.Equal(1, runs);
        }

        [Fact]
        public void WithSetEnabled_AddedAfterBind_IsSetAtOnce()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Run, () => { }, canExecute: () => false);
            var binder = new ViewActionBinder();
            RegisterToolCommand(binder, withEnabled: true);
            binder.Bind(dispatcher);

            var command = new ToolCommand();
            binder.Add(Run, command);

            Assert.False(command.Enabled);
        }

        [Fact]
        public void WithoutSetEnabled_TheComponentIsLeftAlone()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Run, () => { }, canExecute: () => false);
            var command = new ToolCommand();
            var binder = new ViewActionBinder();
            RegisterToolCommand(binder, withEnabled: false);
            binder.Add(Run, command);

            binder.Bind(dispatcher);
            dispatcher.RaiseCanExecuteChanged();

            Assert.True(command.Enabled);
            Assert.Equal(0, command.EnabledWrites);
        }

        [Fact]
        public void SetEnabled_ReplacesTheDefaultForAControlType()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Run, () => { }, canExecute: () => false);
            using (var button = new CustomButton())
            {
                var binder = new ViewActionBinder();
                binder.RegisterStrategy<CustomButton>((c, h) => c.Click += h, (c, h) => c.Click -= h,
                    setEnabled: (c, enabled) => c.Shown = enabled);   // e.g. hide instead of grey out
                binder.Add(Run, button);

                binder.Bind(dispatcher);

                Assert.False(button.Shown);
                Assert.True(button.Enabled);
            }
        }
    }
}
