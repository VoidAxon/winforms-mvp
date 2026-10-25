using System;
using Microsoft.Extensions.DependencyInjection;
using Kibinago;
using Kibinago.Common;
using Kibinago.Common.EventAggregator;
using Kibinago.Configuration;
using Kibinago.DependencyInjection;
using Kibinago.Logging;
using Kibinago.ViewActions;
using Kibinago.Services;
using Kibinago.Services.Implementations;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    [Collection("ServiceLocator")]
    public class KibinagoBuilderTests : IDisposable
    {
        public KibinagoBuilderTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        internal sealed class FakeBackend : IMessageDialogBackend
        {
            public IFoo Foo;
            public MessageDialogResult Show(MessageDialogRequest request) => MessageDialogResult.Ok;
        }

        public interface IFoo { }
        private sealed class Foo : IFoo { }

        private sealed class AlwaysEqual : IObjectContentComparer
        {
            public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores) => true;
        }

        public class Order { public string Name { get; set; } }

        [Fact]
        public void NoFramework_IsEquivalentToServiceLocatorConfigure()
        {
            ServiceLocator.Configure(_ => { });
            var classic = ServiceLocator.Current;
            var classicTypes = new[]
            {
                classic.Resolve<IMessageService>()?.GetType(), classic.Resolve<IMessageDialogBackend>()?.GetType(),
                classic.Resolve<IToastService>()?.GetType(), classic.Resolve<IWinFormsToastService>()?.GetType(),
                classic.Resolve<IDialogProvider>()?.GetType(), classic.Resolve<IEventAggregator>()?.GetType(),
                classic.Resolve<IObjectCloner>()?.GetType(), classic.Resolve<IObjectContentComparer>()?.GetType(),
                classic.Resolve<ILoggerFactory>()?.GetType(), classic.Resolve<IWindowNavigator>()?.GetType(),
            };

            var unified = MvpApplication.Configure();
            var unifiedTypes = new[]
            {
                unified.Resolve<IMessageService>()?.GetType(), unified.Resolve<IMessageDialogBackend>()?.GetType(),
                unified.Resolve<IToastService>()?.GetType(), unified.Resolve<IWinFormsToastService>()?.GetType(),
                unified.Resolve<IDialogProvider>()?.GetType(), unified.Resolve<IEventAggregator>()?.GetType(),
                unified.Resolve<IObjectCloner>()?.GetType(), unified.Resolve<IObjectContentComparer>()?.GetType(),
                unified.Resolve<ILoggerFactory>()?.GetType(), unified.Resolve<IWindowNavigator>()?.GetType(),
            };

            Assert.Equal(classicTypes, unifiedTypes);
            Assert.DoesNotContain(null, unifiedTypes);
            Assert.Same(unified, ServiceLocator.Current);
        }

        [Fact]
        public void BuilderChoice_OverridesBuiltInDefault()
        {
            var backend = new FakeBackend();
            var comparer = new AlwaysEqual();
            var events = new EventAggregator();

            var sp = MvpApplication.Configure(framework: mvp =>
            {
                mvp.Messages.UseDialogs(backend);
                mvp.ChangeTracking.UseComparer(comparer);
                mvp.Events.Use(events);
            });

            Assert.Same(backend, sp.Resolve<IMessageDialogBackend>());
            Assert.Same(comparer, sp.Resolve<IObjectContentComparer>());
            Assert.Same(events, sp.Resolve<IEventAggregator>());
        }

        [Fact]
        public void BuilderOptions_ReachTheServices()
        {
            var sp = MvpApplication.Configure(framework: mvp =>
            {
                mvp.Messages.DefaultCaption = "MyApp";
                mvp.Toasts.Duration = 4500;
                mvp.Dialogs.OpenFileDialogTitle = "Pick a file";
            });

            Assert.Equal("MyApp", sp.Resolve<MessageDialogOptions>().DefaultCaption);
            Assert.Equal(4500, sp.Resolve<ToastHostOptions>().Duration);
            Assert.Equal("Pick a file", sp.Resolve<DialogProviderOptions>().OpenFileDialogTitle);
        }

        [Fact]
        public void DirectRegistration_WinsOverBuilder()
        {
            var a = new FakeBackend();
            var b = new FakeBackend();

            var sp = MvpApplication.Configure(
                framework: mvp => mvp.Messages.UseDialogs(a),
                services: reg => reg.RegisterInstance<IMessageDialogBackend>(b));

            Assert.Same(b, sp.Resolve<IMessageDialogBackend>());
        }

        [Fact]
        public void DirectOptionsRegistration_ReplacesBuilderOptionsWholesale()
        {
            var sp = MvpApplication.Configure(
                framework: mvp => mvp.Toasts.Duration = 4500,
                services: reg => reg.RegisterInstance(new ToastHostOptions { Gap = 3 }));

            var options = sp.Resolve<ToastHostOptions>();
            Assert.Equal(3000, options.Duration);   // the builder's 4500 is not merged in
            Assert.Equal(3, options.Gap);
        }

        [Fact]
        public void FactoryOverload_ReceivesProvider()
        {
            var foo = new Foo();

            var sp = MvpApplication.Configure(
                framework: mvp => mvp.Messages.UseDialogs(p => new FakeBackend { Foo = p.Resolve<IFoo>() }),
                services: reg => reg.RegisterInstance<IFoo>(foo));

            Assert.Same(foo, ((FakeBackend)sp.Resolve<IMessageDialogBackend>()).Foo);
        }

        [Fact]
        public void ChangeTrackingFor_RegistersTypePolicy()
        {
            var policy = new ChangeTrackingPolicy<Order>(o => new Order { Name = o.Name }, (x, y) => true);

            var sp = MvpApplication.Configure(framework: mvp => mvp.ChangeTracking.For(policy));

            Assert.Same(policy, sp.Resolve<ChangeTrackingPolicy<Order>>());
        }

        [Fact]
        public void Logging_Use_RegistersFactory()
        {
            var factory = new TestHelpers.CapturingLoggerFactory();

            var sp = MvpApplication.Configure(framework: mvp => mvp.Logging.Use(factory));

            Assert.Same(factory, sp.Resolve<ILoggerFactory>());
        }

        [Fact]
        public void Actions_Configure_And_StrictValidation_ComposeIntoOneConfigurer()
        {
            int calls = 0;
            var sp = MvpApplication.Configure(framework: mvp =>
            {
                mvp.Actions.Configure(d => calls++);
                mvp.Actions.Configure(d => calls++);
                mvp.Actions.StrictValidation();
            });

            var dispatcher = new ViewActionDispatcher();
            sp.Resolve<IDispatcherConfigurer>().Configure(dispatcher);

            Assert.Equal(2, calls);
            Assert.Equal(DispatchValidationMode.Strict, dispatcher.ValidationMode);
        }

        [Fact]
        public void NoActionsConfigured_RegistersNoConfigurer()
        {
            Assert.Null(MvpApplication.Configure().Resolve<IDispatcherConfigurer>());
        }

        [Fact]
        public void Toasts_Use_ReplacesBothToastInterfaces()
        {
            using (var mine = new WinFormsToastService(new ToastHostOptions()))   // caller-owned
            {
                var sp = MvpApplication.Configure(framework: mvp => mvp.Toasts.Use(mine));

                Assert.Same(mine, sp.Resolve<IWinFormsToastService>());
                Assert.Same(mine, ((ToastServiceForwarder)sp.Resolve<IToastService>()).Target);
            }
        }

        [Fact]
        public void DirectToastRegistration_ReachesPresentersToo()
        {
            using (var a = new WinFormsToastService(new ToastHostOptions()))
            using (var b = new WinFormsToastService(new ToastHostOptions()))
            {
                var sp = MvpApplication.Configure(
                    framework: mvp => mvp.Toasts.Use(a),
                    services: reg => reg.RegisterInstance<IWinFormsToastService>(b));

                Assert.Same(b, sp.Resolve<IWinFormsToastService>());
                Assert.Same(b, ((ToastServiceForwarder)sp.Resolve<IToastService>()).Target);
            }
        }

        [Fact]
        public void Configure_AfterLazyDefaultWasCreated_DisposesItAndInstallsNew()
        {
            var lazyDefault = ServiceLocator.Current;
            var oldEvents = (EventAggregator)lazyDefault.Resolve<IEventAggregator>();

            var sp = MvpApplication.Configure();

            Assert.NotSame(lazyDefault, sp);
            Assert.Throws<ObjectDisposedException>(() => oldEvents.Subscribe<string>(_ => { }));
        }

        [Fact]
        public void Builder_ModifiedAfterBuild_Throws()
        {
            KibinagoBuilder captured = null;
            MvpApplication.Configure(framework: mvp => captured = mvp);

            Assert.Throws<InvalidOperationException>(() => captured.Toasts.Duration = 1);
            Assert.Throws<InvalidOperationException>(() => captured.Messages.UseDialogs(new FakeBackend()));
        }

        [Fact]
        public void UseNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MvpApplication.Configure(framework: mvp => mvp.Messages.UseDialogs((IMessageDialogBackend)null)));
        }
    }

    [Collection("ServiceLocator")]
    public class RegistrationParityTests
    {
        private static void Customize(KibinagoBuilder mvp)
        {
            mvp.Messages.DefaultCaption = "MyApp";
            mvp.Toasts.Duration = 4500;
            mvp.ChangeTracking.UseComparer(BuiltInObjectContentComparer.Instance);
            mvp.Actions.StrictValidation();
        }

        [Fact]
        public void SameBuilder_ResolvesSameImplementationTypes_OnBothPaths()
        {
            var types = new[]
            {
                typeof(IMessageService), typeof(IMessageDialogBackend), typeof(MessageDialogOptions),
                typeof(IToastService), typeof(IWinFormsToastService), typeof(ToastHostOptions),
                typeof(IDialogProvider), typeof(IWinFormsDialogProvider), typeof(DialogProviderOptions),
                typeof(IObjectCloner), typeof(IObjectContentComparer), typeof(IEventAggregator),
                typeof(ILoggerFactory), typeof(IDispatcherConfigurer), typeof(IFileService),
                typeof(IWindowNavigator), typeof(IViewMappingRegister),
            };

            var classicTypes = new System.Collections.Generic.Dictionary<Type, Type>();
            try
            {
                var classic = MvpApplication.Configure(framework: Customize);
                foreach (var type in types) classicTypes[type] = classic.GetService(type)?.GetType();
            }
            finally { ServiceLocator.Reset(); }   // disposes that provider, so resolve before this

            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister(), Customize);
            using var di = services.BuildServiceProvider();

            foreach (var type in types)
            {
                var b = di.GetService(type);
                Assert.True(classicTypes[type] != null, type.Name + " missing on the ServiceLocator path");
                Assert.True(b != null, type.Name + " missing on the M.E.DI path");
                Assert.Equal(classicTypes[type], b.GetType());
            }
            Assert.Equal(4500, di.GetRequiredService<ToastHostOptions>().Duration);
            Assert.Equal("MyApp", di.GetRequiredService<MessageDialogOptions>().DefaultCaption);
        }

        [Fact]
        public void AddKibinago_PreRegisteredService_Wins()
        {
            var mine = new KibinagoBuilderTests.FakeBackend();
            var services = new ServiceCollection();
            services.AddSingleton<IMessageDialogBackend>(mine);

            services.AddKibinago(new ViewMappingRegister(), mvp => mvp.Messages.UseDialogs(new KibinagoBuilderTests.FakeBackend()));

            Assert.Same(mine, services.BuildServiceProvider().GetService<IMessageDialogBackend>());
        }

        private sealed class OwnedToasts : IWinFormsToastService, IDisposable
        {
            public bool Disposed;
            public void Show(string text, ToastType type, int? duration = null) { }
            public void Show(string text, ToastType type, ToastOptions options) { }
            public void ShowAnchored(string text, ToastType type, System.Drawing.Point anchor, ToastOptions options = null) { }
            public void Dispose() => Disposed = true;
        }

        [Fact]
        public void ToastsUseInstance_IsNotDisposedByTheContainer_WhicheverInterfaceWasResolved()
        {
            var mine = new OwnedToasts();
            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister(), mvp => mvp.Toasts.Use(mine));
            var provider = services.BuildServiceProvider();

            Assert.Same(mine, ((ToastServiceForwarder)provider.GetService<IToastService>()).Target);
            provider.Dispose();

            Assert.False(mine.Disposed);   // the caller passed it in, so the caller owns it
        }

        [Fact]
        public void AddKibinago_LaterRegistration_Wins()
        {
            var mine = new KibinagoBuilderTests.FakeBackend();
            var services = new ServiceCollection();

            services.AddKibinago(new ViewMappingRegister(), mvp => mvp.Messages.UseDialogs(new KibinagoBuilderTests.FakeBackend()));
            services.AddSingleton<IMessageDialogBackend>(mine);

            Assert.Same(mine, services.BuildServiceProvider().GetService<IMessageDialogBackend>());
        }
    }
}

namespace Kibinago.Samples.Tests.Services
{
    [Xunit.Collection("ServiceLocator")]
    public class MvpApplicationShutdownTests : System.IDisposable
    {
        public MvpApplicationShutdownTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Xunit.Fact]
        public void Shutdown_DisposesTheConfiguredRoot()
        {
            var sp = MvpApplication.Configure();
            var events = sp.Resolve<Kibinago.Common.EventAggregator.IEventAggregator>();

            MvpApplication.Shutdown();

            Xunit.Assert.Throws<System.ObjectDisposedException>(() => events.Subscribe<string>(_ => { }));
        }

        [Xunit.Fact]
        public void Shutdown_DoesNotDisposeAnAssignedProvider()
        {
            var mine = new DefaultServiceProvider();
            ServiceLocator.Current = mine;

            MvpApplication.Shutdown();

            Xunit.Assert.Null(mine.Resolve<string>());   // still usable: an assigned provider belongs to its owner
        }

        [Xunit.Fact]
        public void Run_DisposesTheRootWhenTheMainFormCloses()
        {
            System.Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var sp = MvpApplication.Configure();
                    var events = sp.Resolve<Kibinago.Common.EventAggregator.IEventAggregator>();
                    var form = new System.Windows.Forms.Form { ShowInTaskbar = false, Opacity = 0 };
                    form.Shown += (s, e) => form.BeginInvoke((System.Action)form.Close);

                    MvpApplication.Run(form);

                    Xunit.Assert.Throws<System.ObjectDisposedException>(() => events.Subscribe<string>(_ => { }));
                }
                catch (System.Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            Xunit.Assert.True(thread.Join(System.TimeSpan.FromSeconds(30)), "UI thread timed out");
            if (failure != null) throw new System.Exception("UI thread failed", failure);
        }
    }
}
