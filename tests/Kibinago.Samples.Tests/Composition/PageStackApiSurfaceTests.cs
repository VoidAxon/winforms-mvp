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
            .Select(Signature).Where(sig => sig != null).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        private static string[] Names(Type type) => type.GetMembers(All)
            .Where(m => Signature(m) != null).Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // "<visibility> <kind> <name>`<arity>(<parameter types>)" for members applications can see.
        private static string Signature(MemberInfo member)
        {
            switch (member)
            {
                case MethodBase m when !m.IsSpecialName || m is ConstructorInfo:
                    return Visibility(m) == null ? null
                        : Visibility(m) + (m is ConstructorInfo ? " ctor " : " method ") + m.Name +
                          (m.IsGenericMethod ? "`" + m.GetGenericArguments().Length : "") +
                          "(" + string.Join(",", m.GetParameters().Select(p => TypeName(p.ParameterType))) + ")";
                case PropertyInfo p:
                    var accessor = p.GetAccessors(true).Where(a => Visibility(a) != null).OrderBy(a => a.IsPublic ? 0 : 1).FirstOrDefault();
                    return accessor == null ? null
                        : Visibility(accessor) + " property " + TypeName(p.PropertyType) + " " + p.Name +
                          (p.GetIndexParameters().Length > 0 ? "[" + string.Join(",", p.GetIndexParameters().Select(i => TypeName(i.ParameterType))) + "]" : "");
                case EventInfo e:
                    return Visibility(e.AddMethod) == null ? null : Visibility(e.AddMethod) + " event " + e.Name;
                case FieldInfo f:
                    return f.IsPublic ? "public field " + f.Name : f.IsFamily || f.IsFamilyOrAssembly ? "protected field " + f.Name : null;
                default:
                    return null;
            }
        }

        private static string Visibility(MethodBase m)
            => m.IsPublic ? "public" : m.IsFamily || m.IsFamilyOrAssembly ? "protected" : null;

        private static string TypeName(Type t)
        {
            if (t.IsGenericParameter) return t.Name;
            if (t.IsByRef) return TypeName(t.GetElementType()) + "&";
            if (t.IsArray) return TypeName(t.GetElementType()) + "[]";
            if (!t.IsGenericType) return t.Name;
            var name = t.Name.Substring(0, t.Name.IndexOf('`'));
            return name + "<" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + ">";
        }

        [Fact]
        public void PresenterBases_GainNoMember()
        {
            // Signatures taken before the page stack was added (commit 6ab985a); overloads count separately.
            Assert.Equal(PresenterBaseSurface, Visible(typeof(PresenterBase<IView>)));
            Assert.Equal(WindowCoreSurface, Visible(typeof(WindowPresenterBaseCore<IWindowView>)));
            Assert.Equal(Window1Surface, Visible(typeof(WindowPresenterBase<IWindowView>)));
            Assert.Equal(Window2Surface, Visible(typeof(WindowPresenterBase<IWindowView, object>)));
            Assert.Equal(Control1Surface, Visible(typeof(ControlPresenterBase<IControlView>)));
            Assert.Equal(Control2Surface, Visible(typeof(ControlPresenterBase<IControlView, object>)));
        }

        private static readonly string[] PresenterBaseSurface =
        {
            "protected method AttachChild`1(ControlPresenterBase<TChildView>,TChildView)",
            "protected method AttachChild`2(ControlPresenterBase<TChildView,TParam>,TChildView,TParam)",
            "protected method Cleanup()",
            "protected method DispatchAction(ActionRequestEventArgs)",
            "protected method OnInitializeAsync(CancellationToken)",
            "protected method OnViewActionTriggered(Object,ActionRequestEventArgs)",
            "protected method OnViewAttached()",
            "protected method ShowBusy(String)",
            "protected method SubscribeViewEvent(Action<EventHandler>,Action<EventHandler>,Action)",
            "protected method SubscribeViewEvent`1(Action<EventHandler<TEventArgs>>,Action<EventHandler<TEventArgs>>,Action<TEventArgs>)",
            "protected method TrackChanges`1(T)",
            "protected method TrackChanges`1(T,ChangeTrackingPolicy<T>)",
            "protected method TrackChanges`1(T,Func<T,T,Boolean>)",
            "protected property CancellationToken Lifetime",
            "protected property CompositeDisposable Disposables",
            "protected property IDialogProvider Dialogs",
            "protected property ILogger Logger",
            "protected property IMessageService Messages",
            "protected property IServiceProvider Services",
            "protected property IToastService Toasts",
            "protected property IView View",
            "protected property IWindowNavigator Navigator",
            "protected property ValidationState Validation",
            "protected property ViewActionDispatcher Dispatcher",
            "public method Dispose()",
            "public property Type ViewInterfaceType",
        };

        private static readonly string[] WindowCoreSurface =
        {
            "protected method CanClose(CloseReason)",
            "protected method CanCloseAsync(CloseReason)",
            "protected method RequestClose(InteractionStatus)",
            "protected method RequestClose`1(TResult,InteractionStatus)",
        };

        private static readonly string[] Window1Surface =
        {
            "protected ctor .ctor()",
            "protected method OnInitialize()",
            "protected method RegisterViewActions()",
        };

        private static readonly string[] Window2Surface =
        {
            "protected ctor .ctor()",
            "protected method OnInitialize(Object)",
            "protected method RegisterViewActions()",
            "protected property Object Parameters",
        };

        private static readonly string[] Control1Surface =
        {
            "protected ctor .ctor()",
            "protected method OnInitialize()",
            "protected method RegisterViewActions()",
        };

        private static readonly string[] Control2Surface =
        {
            "protected ctor .ctor()",
            "protected method OnInitialize(Object)",
            "protected method RegisterViewActions()",
            "protected property Object Parameters",
        };
        [Fact]
        public void PageStack_IsSealed_WithExactlyItsMembers()
        {
            Assert.True(typeof(PageStack).IsSealed);
            Assert.True(typeof(PageStack).IsPublic);
            Assert.Equal(new[] { ".ctor", "CanCloseAllAsync", "Count", "IsBusy", "PopAsync", "PopToCountAsync", "Push" },
                Names(typeof(PageStack)));
            Assert.Equal(new[] { typeof(IPresenter), typeof(Func<IPageHost>) },
                typeof(PageStack).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
            var pushes = typeof(PageStack).GetMethods().Where(m => m.Name == "Push").ToList();
            Assert.Equal(2, pushes.Count);
            Assert.All(pushes, m => Assert.Equal(typeof(bool), m.ReturnType));
            Assert.All(pushes, m => Assert.Equal("title", m.GetParameters().Last().Name));
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("PopToCountAsync").ReturnType);
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("PopAsync").ReturnType);
            Assert.Equal(typeof(Task<bool>), typeof(PageStack).GetMethod("CanCloseAllAsync").ReturnType);
            Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PageStack)));
        }

        [Fact]
        public void PageHost_IsAViewCapability_WithTwoMethods()
        {
            var host = typeof(IPageHost);
            Assert.True(host.IsInterface && host.IsPublic);
            Assert.Equal("Kibinago.Views", host.Namespace);
            Assert.Equal(new[] { "AddPage", "RemovePage" }, host.GetMembers().Select(m => m.Name).OrderBy(n => n));
            Assert.Equal(typeof(IControlView), host.GetMethod("AddPage").ReturnType);
            Assert.Equal(new[] { typeof(Type), typeof(string), typeof(IServiceProvider) },
                host.GetMethod("AddPage").GetParameters().Select(p => p.ParameterType));
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
