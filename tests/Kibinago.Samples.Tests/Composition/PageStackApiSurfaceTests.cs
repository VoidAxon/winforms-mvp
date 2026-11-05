using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>
    /// The page stack adds two public types and nothing else: ordinary presenters see no new member.
    /// (Kept apart from ApiSurfaceTests so it runs without WinForms.)
    /// </summary>
    public class PageStackApiSurfaceTests
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static string[] Visible(Type type) => type.GetMembers(All)
            .Where(m =>
            {
                switch (m)
                {
                    case ConstructorInfo c: return c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly;
                    case MethodInfo mi: return !mi.IsSpecialName && (mi.IsPublic || mi.IsFamily || mi.IsFamilyOrAssembly);
                    case PropertyInfo p: return p.GetAccessors(true).Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly);
                    case EventInfo e: return e.AddMethod.IsPublic || e.AddMethod.IsFamily || e.AddMethod.IsFamilyOrAssembly;
                    case FieldInfo f: return f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;
                    default: return false;
                }
            })
            .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        [Fact]
        public void PresenterBases_GainNoMember()
        {
            // Taken before the page stack was added (commit 6ab985a).
            Assert.Equal(new[]
            {
                "AttachChild", "Cleanup", "Dialogs", "DispatchAction", "Dispatcher", "Disposables", "Dispose", "Lifetime",
                "Logger", "Messages", "Navigator", "OnInitializeAsync", "OnViewActionTriggered", "OnViewAttached",
                "Services", "ShowBusy", "SubscribeViewEvent", "Toasts", "TrackChanges", "Validation", "View",
                "ViewInterfaceType",
            }, Visible(typeof(PresenterBase<IView>)));
            Assert.Equal(new[] { "CanClose", "CanCloseAsync", "RequestClose" }, Visible(typeof(WindowPresenterBaseCore<IWindowView>)));
            Assert.Equal(new[] { ".ctor", "OnInitialize", "RegisterViewActions" }, Visible(typeof(WindowPresenterBase<IWindowView>)));
            Assert.Equal(new[] { ".ctor", "OnInitialize", "Parameters", "RegisterViewActions" }, Visible(typeof(WindowPresenterBase<IWindowView, object>)));
            Assert.Equal(new[] { ".ctor", "OnInitialize", "RegisterViewActions" }, Visible(typeof(ControlPresenterBase<IControlView>)));
            Assert.Equal(new[] { ".ctor", "OnInitialize", "Parameters", "RegisterViewActions" }, Visible(typeof(ControlPresenterBase<IControlView, object>)));
        }

        [Fact]
        public void PageStack_IsSealed_WithExactlyItsMembers()
        {
            Assert.True(typeof(PageStack).IsSealed);
            Assert.True(typeof(PageStack).IsPublic);
            Assert.Equal(new[] { ".ctor", "CanCloseAllAsync", "Count", "IsBusy", "PopAsync", "PopToCountAsync", "Push" },
                Visible(typeof(PageStack)));
            Assert.Equal(new[] { typeof(IPresenter) }, typeof(PageStack).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
            Assert.Equal(2, typeof(PageStack).GetMethods().Count(m => m.Name == "Push"));
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("PopToCountAsync").ReturnType);
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("PopAsync").ReturnType);
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("CanCloseAllAsync").ReturnType);
            Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PageStack)));
        }

        [Fact]
        public void PageCloseGuard_HasOneAsyncMethod()
        {
            var guard = typeof(IPageCloseGuard);
            Assert.True(guard.IsInterface && guard.IsPublic);
            Assert.Equal("Kibinago.Presenters", guard.Namespace);
            var method = Assert.Single(guard.GetMembers());
            Assert.Equal("CanCloseAsync", method.Name);
            Assert.Equal(typeof(Task<bool>), ((MethodInfo)method).ReturnType);
            Assert.Empty(((MethodInfo)method).GetParameters());
        }

        [Fact]
        public void APageImplementingTheGuardExplicitly_HasNoPublicMembers()
        {
            Assert.DoesNotContain(typeof(GuardSample).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
                m => m.Name.Contains("CanClose"));
        }

        private sealed class GuardSample : ControlPresenterBase<IControlView>, IPageCloseGuard
        {
            Task<bool> IPageCloseGuard.CanCloseAsync() => Task.FromResult(true);
        }
    }
}
