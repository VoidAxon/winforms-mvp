using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests the built-in renderers' corner shape and that they paint without throwing. Pixel
    /// output is verified manually via the sample, not asserted here.
    /// </summary>
    public class ToastRendererTests
    {
        [Fact]
        public void DefaultRenderer_IsSquare()
        {
            Assert.Equal(0, new DefaultToastRenderer().CornerRadius);
        }

        [Fact]
        public void SoftRenderer_IsRounded()
        {
            Assert.True(new SoftToastRenderer().CornerRadius > 0);
        }

        [Fact]
        public void CardRenderer_IsRounded()
        {
            Assert.True(new CardToastRenderer().CornerRadius > 0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Renderers_PaintWithoutThrowing(bool showClose)
        {
            ToastRenderer[] renderers =
            {
                new DefaultToastRenderer(),
                new SoftToastRenderer(),
                new CardToastRenderer()
            };

            using (var bitmap = new Bitmap(350, 80))
            using (var g = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", 10f))
            {
                foreach (var renderer in renderers)
                {
                    foreach (ToastType type in new[] { ToastType.Info, ToastType.Success, ToastType.Warning, ToastType.Error })
                    {
                        var context = new ToastRenderContext(g, new Rectangle(0, 0, 350, 80), "Sample message", type, font, renderer.CornerRadius, showClose);
                        renderer.Render(context); // must not throw
                    }
                }
            }
        }
    }
}
