using System;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class ActionDispatcherConfigurerTests
    {
        [Fact]
        public void Configure_InvokesWrappedAction_WithGivenDispatcher()
        {
            ViewActionDispatcher received = null;
            var dispatcher = new ViewActionDispatcher();
            var configurer = new ActionDispatcherConfigurer(d => received = d);

            configurer.Configure(dispatcher);

            Assert.Same(dispatcher, received);
        }

        [Fact]
        public void Constructor_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ActionDispatcherConfigurer(null));
        }
    }
}
