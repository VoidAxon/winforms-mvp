using Kibinago.Testing;
using Xunit;
using Kibinago.Services;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakeToastServiceTests
    {
        [Fact]
        public void Show_IsRecordedWithDuration()
        {
            var toasts = new FakeToastService();
            toasts.Show("Saved", ToastType.Success, 1500);
            toasts.Show("Hi", ToastType.Info);

            Assert.Equal(2, toasts.Calls.Count);
            Assert.Equal("Saved", toasts.Calls[0].Text);
            Assert.Equal(ToastType.Success, toasts.Calls[0].Type);
            Assert.Equal(1500, toasts.Calls[0].Duration);
            Assert.Null(toasts.Calls[1].Duration);

            toasts.ClearCalls();
            Assert.Empty(toasts.Calls);
        }
    }
}
