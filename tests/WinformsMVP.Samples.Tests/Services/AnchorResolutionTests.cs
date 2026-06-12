using System.Drawing;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for the pure interaction-point decision used by the anchor-free forms of
    /// <c>AnchoredMessageService</c>. The input modality comes from the message-level
    /// <c>LastInputTracker</c> (keydown vs mouse-button-down); when it has not observed any
    /// input yet (<c>null</c>), the decision falls back to the geometric inference
    /// (cursor inside the active window → mouse).
    /// </summary>
    public class AnchorResolutionTests
    {
        private static readonly Rectangle Window = new Rectangle(100, 100, 800, 600);
        private static readonly Rectangle Screen = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle Focused = new Rectangle(300, 400, 80, 30);

        [Fact]
        public void MouseInput_ReturnsCursor_EvenWhenFocusInfoExists()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen, lastInputWasKeyboard: false);
            Assert.Equal(cursor, result);
        }

        [Fact]
        public void KeyboardInput_ReturnsFocusedControlBottomLeft_EvenWhenCursorIsInsideWindow()
        {
            // The decisive case: the mouse is parked inside the window (it almost always is),
            // but the trigger was a keyboard shortcut — anchor at the focused control.
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen, lastInputWasKeyboard: true);
            Assert.Equal(new Point(Focused.Left, Focused.Bottom), result);
        }

        [Fact]
        public void KeyboardInput_NoFocusedControl_ReturnsWindowCenter()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, null, Screen, lastInputWasKeyboard: true);
            Assert.Equal(new Point(Window.Left + Window.Width / 2, Window.Top + Window.Height / 2), result);
        }

        [Fact]
        public void UnknownModality_CursorInsideWindow_FallsBackToCursor()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen, lastInputWasKeyboard: null);
            Assert.Equal(cursor, result);
        }

        [Fact]
        public void UnknownModality_CursorOutsideWindow_FallsBackToFocusedControl()
        {
            var cursor = new Point(1500, 50);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen, lastInputWasKeyboard: null);
            Assert.Equal(new Point(Focused.Left, Focused.Bottom), result);
        }

        [Fact]
        public void NoActiveWindow_ReturnsScreenCenter_RegardlessOfModality()
        {
            var cursor = new Point(1500, 50);
            Assert.Equal(
                new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2),
                AnchoredMessageService.ResolveAnchorCore(cursor, null, Focused, Screen, lastInputWasKeyboard: true));
            Assert.Equal(
                new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2),
                AnchoredMessageService.ResolveAnchorCore(cursor, null, null, Screen, lastInputWasKeyboard: false));
        }
    }
}
