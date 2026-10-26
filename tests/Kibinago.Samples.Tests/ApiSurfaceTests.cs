using System;
using System.Collections;
using System.Reflection;
using Kibinago.Configuration;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests
{
    /// <summary>
    /// Pins public-API decisions that the compiler alone would not keep: what applications may
    /// derive from, construct or call.
    /// </summary>
    public class ApiSurfaceTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Theory]
        [InlineData(typeof(PresenterBase<IViewBase>))]
        [InlineData(typeof(WindowPresenterBaseCore<IWindowView>))]
        public void InternalPresenterBases_CannotBeDerivedFromByApplications(Type type)
        {
            var ctor = type.GetConstructor(Instance, null, Type.EmptyTypes, null);

            Assert.NotNull(ctor);
            Assert.True(ctor.IsFamilyAndAssembly, type.Name + " constructor must be private protected");
        }

        [Fact]
        public void SetView_IsNotAvailableToApplicationPresenters()
        {
            var setView = typeof(PresenterBase<IViewBase>).GetMethod("SetView", Instance);

            Assert.True(setView.IsFamilyAndAssembly);
        }

        [Fact]
        public void KibinagoBuilder_IsCreatedOnlyByTheFramework()
        {
            Assert.Empty(typeof(KibinagoBuilder).GetConstructors());
        }

        [Fact]
        public void ViewActionBinder_IsNotACollection()
        {
            Assert.False(typeof(IEnumerable).IsAssignableFrom(typeof(ViewActionBinder)));
        }

        [Fact]
        public void LoggerFactory_ImplementersWriteOneMethod()
        {
            Assert.Single(typeof(ILoggerFactory).GetMethods());
        }
    }
}
