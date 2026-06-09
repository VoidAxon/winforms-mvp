using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="ToastLayout"/>, the pure stacking math behind toast notifications.
    /// Because the calculation takes the working area as a plain <see cref="Rectangle"/> and returns
    /// points — no <c>Screen</c>, no windows — it can be verified directly here.
    /// </summary>
    public class ToastLayoutTests
    {
        // A 1000x1000 working area keeps the arithmetic easy to follow.
        private static readonly Rectangle Area = new Rectangle(0, 0, 1000, 1000);

        private const int Margin = 20;
        private const int Gap = 10;

        private static ToastBox Box(ToastPosition position, int width = 350, int height = 80)
        {
            return new ToastBox { Position = position, Width = width, Height = height };
        }

        [Fact]
        public void SingleBottomRight_SitsAtBottomRightCorner()
        {
            var points = ToastLayout.Arrange(new[] { Box(ToastPosition.BottomRight) }, Area, Margin, Gap);

            // Right edge: 1000 - 350 - 20 = 630. Bottom edge: 1000 - 20 - 80 = 900.
            Assert.Equal(new Point(630, 900), points[0]);
        }

        [Fact]
        public void SingleTopLeft_SitsAtTopLeftCorner()
        {
            var points = ToastLayout.Arrange(new[] { Box(ToastPosition.TopLeft) }, Area, Margin, Gap);

            Assert.Equal(new Point(20, 20), points[0]);
        }

        [Fact]
        public void BottomCorner_NewestHugsEdge_OlderStacksUpward()
        {
            // Oldest-first input: index 0 is oldest, index 1 is newest.
            var points = ToastLayout.Arrange(
                new[] { Box(ToastPosition.BottomRight), Box(ToastPosition.BottomRight) },
                Area, Margin, Gap);

            // Newest (index 1) hugs the bottom: y = 1000 - 20 - 80 = 900.
            Assert.Equal(900, points[1].Y);
            // Older (index 0) sits one toast height + gap above: 900 - 80 - 10 = 810.
            Assert.Equal(810, points[0].Y);
            // Same column.
            Assert.Equal(points[1].X, points[0].X);
        }

        [Fact]
        public void TopCorner_NewestHugsEdge_OlderStacksDownward()
        {
            var points = ToastLayout.Arrange(
                new[] { Box(ToastPosition.TopLeft), Box(ToastPosition.TopLeft) },
                Area, Margin, Gap);

            // Newest (index 1) hugs the top: y = 20.
            Assert.Equal(20, points[1].Y);
            // Older (index 0) sits one toast height + gap below: 20 + 80 + 10 = 110.
            Assert.Equal(110, points[0].Y);
        }

        [Fact]
        public void DifferentCorners_StackIndependently()
        {
            var points = ToastLayout.Arrange(
                new[]
                {
                    Box(ToastPosition.BottomRight),
                    Box(ToastPosition.BottomLeft),
                    Box(ToastPosition.BottomRight),
                },
                Area, Margin, Gap);

            // Two BottomRight toasts stack; the lone BottomLeft is unaffected by them.
            Assert.Equal(new Point(20, 900), points[1]);   // BottomLeft, alone -> bottom edge
            Assert.Equal(900, points[2].Y);                // newest BottomRight hugs bottom
            Assert.Equal(810, points[0].Y);                // older BottomRight one slot up
        }

        [Fact]
        public void VariableHeights_AccumulateActualHeight()
        {
            // Newest is taller (120px); the older one above it must clear the taller toast.
            var points = ToastLayout.Arrange(
                new[]
                {
                    Box(ToastPosition.BottomRight, height: 80),
                    Box(ToastPosition.BottomRight, height: 120),
                },
                Area, Margin, Gap);

            // Newest (120 tall) hugs bottom: y = 1000 - 20 - 120 = 860.
            Assert.Equal(860, points[1].Y);
            // Older sits above the taller one: 1000 - 20 - (120 + 10) - 80 = 770.
            Assert.Equal(770, points[0].Y);
        }

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            var points = ToastLayout.Arrange(new ToastBox[0], Area, Margin, Gap);

            Assert.Empty(points);
        }

        // --- Anchor (single, tooltip-style point placement) ---

        [Fact]
        public void Anchor_RoomBelowAndRight_ExtendsDownRightFromAnchor()
        {
            var p = ToastLayout.Anchor(new Point(100, 100), new Size(350, 80), Area, Margin);

            // Plenty of room: the toast's top-left sits right at the anchor.
            Assert.Equal(new Point(100, 100), p);
        }

        [Fact]
        public void Anchor_NearRightEdge_FlipsToLeftOfAnchor()
        {
            // Anchor close to the right edge: 900 + 350 would overflow, so flip left.
            var p = ToastLayout.Anchor(new Point(900, 100), new Size(350, 80), Area, Margin);

            // Flipped: x = 900 - 350 = 550 (still on screen, so no further clamp).
            Assert.Equal(550, p.X);
            Assert.Equal(100, p.Y);
        }

        [Fact]
        public void Anchor_NearBottomEdge_FlipsAboveAnchor()
        {
            var p = ToastLayout.Anchor(new Point(100, 950), new Size(350, 80), Area, Margin);

            // Flipped up: y = 950 - 80 = 870.
            Assert.Equal(100, p.X);
            Assert.Equal(870, p.Y);
        }

        [Fact]
        public void Anchor_OffScreenPoint_IsClampedFullyOnScreen()
        {
            // A wildly off-screen anchor must still produce a fully-visible toast.
            var size = new Size(350, 80);
            var p = ToastLayout.Anchor(new Point(5000, 5000), size, Area, Margin);

            // Clamped to the bottom-right inside the margin: x = 1000-20-350=630, y = 1000-20-80=900.
            Assert.Equal(new Point(630, 900), p);
            // Sanity: the whole toast rectangle is inside the area.
            Assert.True(p.X >= Area.Left + Margin);
            Assert.True(p.Y >= Area.Top + Margin);
            Assert.True(p.X + size.Width <= Area.Right - Margin);
            Assert.True(p.Y + size.Height <= Area.Bottom - Margin);
        }

        [Fact]
        public void Anchor_NegativePoint_IsClampedToTopLeftMargin()
        {
            var p = ToastLayout.Anchor(new Point(-500, -500), new Size(350, 80), Area, Margin);

            Assert.Equal(new Point(Margin, Margin), p);
        }
    }
}
