using System.Drawing;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for the pure interaction-point decision used by the anchor-free forms of
    /// <c>AnchoredMessageService</c>. The input modality comes from the message-level
    /// <c>LastInputTracker</c>; the triggering control comes from <c>InteractionSource</c>
    /// (set by the binder). Keyboard anchors at the trigger, falling back to the focused
    /// control — they differ for button mnemonics, which click without moving focus.
    /// </summary>
    public class AnchorResolutionTests
    {
        private static readonly Rectangle Window = new Rectangle(100, 100, 800, 600);
        private static readonly Rectangle Screen = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle Focused = new Rectangle(300, 400, 80, 30);
        private static readonly Rectangle Trigger = new Rectangle(500, 200, 90, 32);

        [Fact]
        public void MouseInput_ReturnsCursor_IgnoringTriggerAndFocus()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, Trigger, Focused, Screen, lastInputWasKeyboard: false, inputLive: true);
            Assert.Equal(cursor, result);
        }

        [Fact]
        public void KeyboardInput_TriggerControlWins_OverFocusedControl()
        {
            // The mnemonic case (Alt+D): the Delete button is clicked while focus stays on Save.
            // The feedback must anchor at the ACTIVATED control, not the focused one.
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, Trigger, Focused, Screen, lastInputWasKeyboard: true, inputLive: true);
            Assert.Equal(new Point(Trigger.Left, Trigger.Bottom), result);
        }

        [Fact]
        public void KeyboardInput_NoTrigger_FallsBackToFocusedControl_EvenWhenCursorIsInsideWindow()
        {
            // Shortcut case (Ctrl+S while editing): no visible trigger control; the mouse is
            // parked inside the window — feedback anchors at the focused control, not the mouse.
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, null, Focused, Screen, lastInputWasKeyboard: true, inputLive: true);
            Assert.Equal(new Point(Focused.Left, Focused.Bottom), result);
        }

        [Fact]
        public void KeyboardInput_NoTriggerNoFocus_ReturnsWindowCenter()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, null, null, Screen, lastInputWasKeyboard: true, inputLive: true);
            Assert.Equal(new Point(Window.Left + Window.Width / 2, Window.Top + Window.Height / 2), result);
        }

        [Fact]
        public void UnknownModality_CursorInsideWindow_FallsBackToCursor()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, Trigger, Focused, Screen, lastInputWasKeyboard: null, inputLive: true);
            Assert.Equal(cursor, result);
        }

        [Fact]
        public void UnknownModality_CursorOutsideWindow_FallsBackToTriggerControl()
        {
            var cursor = new Point(1500, 50);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, Trigger, Focused, Screen, lastInputWasKeyboard: null, inputLive: true);
            Assert.Equal(new Point(Trigger.Left, Trigger.Bottom), result);
        }

        [Fact]
        public void ProgrammaticTrigger_WithTriggerControl_AnchorsAtIt()
        {
            // Code-driven PerformClick on a button (e.g. from a timer): the clicked control is
            // an explicit causal anchor even though no input is live.
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, Trigger, Focused, Screen, lastInputWasKeyboard: false, inputLive: false);
            Assert.Equal(new Point(Trigger.Left, Trigger.Bottom), result);
        }

        [Fact]
        public void ProgrammaticTrigger_NoTrigger_ReturnsWindowCenter_IgnoringStaleCursorAndFocus()
        {
            // Pure Dispatcher.Dispatch from a timer/async continuation: the stale cursor and the
            // current focus are causally unrelated — the dignified default is the window center.
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(
                cursor, Window, null, Focused, Screen, lastInputWasKeyboard: false, inputLive: false);
            Assert.Equal(new Point(Window.Left + Window.Width / 2, Window.Top + Window.Height / 2), result);
        }

        [Fact]
        public void NoActiveWindow_ReturnsScreenCenter_RegardlessOfModality()
        {
            var cursor = new Point(1500, 50);
            Assert.Equal(
                new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2),
                AnchoredMessageService.ResolveAnchorCore(cursor, null, Trigger, Focused, Screen, lastInputWasKeyboard: true, inputLive: true));
            Assert.Equal(
                new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2),
                AnchoredMessageService.ResolveAnchorCore(cursor, null, null, null, Screen, lastInputWasKeyboard: false, inputLive: true));
        }
    }
}
