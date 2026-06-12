using System.Drawing;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for the pure interaction-point decision used by the cursor-anchored forms of
    /// <c>AnchoredMessageService</c>. Convention mirrors Windows' WM_CONTEXTMENU handling:
    /// mouse (cursor inside the active window) → exact cursor point; keyboard (cursor outside)
    /// → the focused control; fallbacks → window center, then screen center.
    /// </summary>
    public class AnchorResolutionTests
    {
        private static readonly Rectangle Window = new Rectangle(100, 100, 800, 600);
        private static readonly Rectangle Screen = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle Focused = new Rectangle(300, 400, 80, 30);

        [Fact]
        public void CursorInsideActiveWindow_ReturnsCursor()
        {
            var cursor = new Point(500, 300);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen);
            Assert.Equal(cursor, result);   // mouse click: exact point wins even when focus info exists
        }

        [Fact]
        public void CursorOutsideWindow_WithFocusedControl_ReturnsControlBottomLeft()
        {
            var cursor = new Point(1500, 50);   // mouse parked elsewhere → keyboard-triggered
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, Focused, Screen);
            Assert.Equal(new Point(Focused.Left, Focused.Bottom), result);
        }

        [Fact]
        public void CursorOutsideWindow_NoFocusedControl_ReturnsWindowCenter()
        {
            var cursor = new Point(1500, 50);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, Window, null, Screen);
            Assert.Equal(new Point(Window.Left + Window.Width / 2, Window.Top + Window.Height / 2), result);
        }

        [Fact]
        public void NoActiveWindow_ReturnsScreenCenter()
        {
            var cursor = new Point(1500, 50);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, null, null, Screen);
            Assert.Equal(new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2), result);
        }

        [Fact]
        public void NoActiveWindow_FocusInfoIgnored_StillScreenCenter()
        {
            // Focus bounds without an active window is an inconsistent reading; the safe center wins.
            var cursor = new Point(10, 10);
            var result = AnchoredMessageService.ResolveAnchorCore(cursor, null, Focused, Screen);
            Assert.Equal(new Point(Screen.Left + Screen.Width / 2, Screen.Top + Screen.Height / 2), result);
        }
    }
}
