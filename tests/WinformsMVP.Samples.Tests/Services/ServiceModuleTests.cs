using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class ServiceModuleTests
    {
        public interface IBar { }
        private sealed class Bar : IBar { }

        private sealed class BarModule : IServiceModule
        {
            public void RegisterServices(IServiceRegistry registry)
                => registry.RegisterInstance<IBar>(new Bar());
        }

        [Fact]
        public void Module_RegistersIntoRegistry_WithoutExternalDI()
        {
            var sp = new DefaultServiceProvider();
            new BarModule().RegisterServices(sp);
            Assert.NotNull(sp.GetService<IBar>());
        }
    }
}
