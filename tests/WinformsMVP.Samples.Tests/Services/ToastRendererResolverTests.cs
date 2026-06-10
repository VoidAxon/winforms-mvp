using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests <see cref="ToastRendererResolver"/>, the pure precedence logic that picks a toast
    /// painter most-specific-first. No windows are created.
    /// </summary>
    public class ToastRendererResolverTests
    {
        private sealed class StubRenderer : ToastRenderer
        {
            public override void Render(ToastRenderContext context) { }
        }

        [Fact]
        public void PerCallRenderer_WinsOverEverything()
        {
            var perCall = new StubRenderer();
            var result = ToastRendererResolver.Resolve(perCall, ToastStyle.Card, new StubRenderer(), ToastStyle.Soft);
            Assert.Same(perCall, result);
        }

        [Fact]
        public void PerCallStyle_WinsOverDefaults_WhenNoPerCallRenderer()
        {
            var result = ToastRendererResolver.Resolve(null, ToastStyle.Soft, new StubRenderer(), ToastStyle.Card);
            Assert.IsType<SoftToastRenderer>(result);
        }

        [Fact]
        public void DefaultRenderer_WinsOverDefaultStyle_WhenNoPerCallValues()
        {
            var defaultRenderer = new StubRenderer();
            var result = ToastRendererResolver.Resolve(null, null, defaultRenderer, ToastStyle.Soft);
            Assert.Same(defaultRenderer, result);
        }

        [Fact]
        public void DefaultStyle_IsUsed_WhenNothingElseSet()
        {
            var result = ToastRendererResolver.Resolve(null, null, null, ToastStyle.Card);
            Assert.IsType<CardToastRenderer>(result);
        }

        [Fact]
        public void ForStyle_Solid_ReturnsDefaultRenderer()
        {
            Assert.IsType<DefaultToastRenderer>(ToastRendererResolver.ForStyle(ToastStyle.Solid));
        }

        [Fact]
        public void ForStyle_Soft_ReturnsSoftRenderer()
        {
            Assert.IsType<SoftToastRenderer>(ToastRendererResolver.ForStyle(ToastStyle.Soft));
        }
    }
}
