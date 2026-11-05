using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Composition
{
    // Pages, a window that owns a stack, and plain-object views (no Forms) for the PageStack tests.

    public interface IPageView : IControlView
    {
        string Tag { get; }
    }

    public sealed class FakePageView : IPageView
    {
        public FakePageView(string tag) { Tag = tag; }
        public string Tag { get; }
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
        public override string ToString() => Tag;
    }

    public static class PageActions
    {
        public static readonly ViewAction Save = ViewAction.Create("Page.Save");
    }

    /// <summary>A page that may own a stack of its own; its guard is optional (see <see cref="GuardedPage"/>).</summary>
    public class PlainPage : ControlPresenterBase<IPageView>
    {
        protected readonly List<string> Log;
        public readonly string Tag;
        public PageStack Inner;
        public readonly TestPageHost InnerHost = new TestPageHost();
        public Action<PlainPage> OnInit;
        public Exception ThrowOnInitialize;
        public Exception ThrowOnCleanup;
        public Action OnCleanup;
        public bool Disposed;

        public PlainPage(string tag, List<string> log, bool ownsStack = false)
        {
            Tag = tag;
            Log = log;
            if (ownsStack) Inner = new PageStack(this, () => InnerHost);
        }

        protected override void OnInitialize()
        {
            if (ThrowOnInitialize != null) throw ThrowOnInitialize;
            OnInit?.Invoke(this);
        }

        protected override void Cleanup()
        {
            Disposed = true;
            Log?.Add("dispose:" + Tag);
            OnCleanup?.Invoke();
            if (ThrowOnCleanup != null) throw ThrowOnCleanup;
        }

        internal object GetMessagesForTest() => Messages;

        /// <summary>Plain AttachChild of a panel into this page (what a page's own code would do).</summary>
        internal IDisposable AttachPanelForTest(PlainPage panel) => AttachChild(panel, new FakePageView(panel.Tag));

        /// <summary>A stack created by this page's own code (e.g. inside its guard).</summary>
        internal PageStack CreateStackForTest() => new PageStack(this, () => InnerHost);

        /// <summary>Pushes onto this page's own stack (test access; a real page calls it from its own code).</summary>
        internal bool PushInner(PlainPage page, List<string> removed)
        {
            if (removed != null) InnerHost.Log = removed;
            return Inner.Push(page, page.Tag);
        }
    }

    /// <summary>A page with a guard whose answer each test sets.</summary>
    public class GuardedPage : PlainPage, IPageCloseGuard
    {
        public Func<Task<bool>> Answer = () => Task.FromResult(true);
        public int AskCount;

        public GuardedPage(string tag, List<string> log, bool ownsStack = false) : base(tag, log, ownsStack) { }

        Task<bool> IPageCloseGuard.CanCloseAsync()
        {
            AskCount++;
            Log?.Add("ask:" + Tag);
            return Answer();
        }
    }

    /// <summary>A guarded page that claims to equal every other one (value equality must not merge answers).</summary>
    public sealed class EqualPage : GuardedPage
    {
        public EqualPage(string tag, List<string> log) : base(tag, log) { }
        public override bool Equals(object obj) => obj is EqualPage;
        public override int GetHashCode() => 1;
    }

    /// <summary>A panel page that forwards its guard to the stack it owns.</summary>
    public sealed class ForwardingPage : PlainPage, IPageCloseGuard
    {
        public int AskCount;

        public ForwardingPage(string tag, List<string> log) : base(tag, log, ownsStack: true) { }

        Task<bool> IPageCloseGuard.CanCloseAsync()
        {
            AskCount++;
            Log?.Add("ask:" + Tag);
            return Inner.CanCloseAllAsync();
        }
    }

    public sealed class PageParams
    {
        public Action<string> Saved;
    }

    /// <summary>A page with parameters that reports a result through a callback, from its own action.</summary>
    public sealed class ParamPage : ControlPresenterBase<IPageView, PageParams>
    {
        public PageParams Received;

        protected override void RegisterViewActions()
            => Dispatcher.Register(PageActions.Save, () => Received.Saved?.Invoke(View.Tag));

        protected override void OnInitialize(PageParams parameters) => Received = parameters;
    }

    public static class WorkspaceActions
    {
        public static readonly ViewAction<int> Breadcrumb = ViewAction.Create<int>("Workspace.Breadcrumb");
    }

    public interface IWorkspaceView : IWindowView
    {
        IPageHost Pages { get; }
    }

    /// <summary>
    /// An IPageHost for the PageStack tests: makes a <see cref="FakePageView"/> tagged with the title (or returns
    /// <see cref="NextView"/> once) and records every removal ("remove:tag" in <see cref="Log"/>).
    /// </summary>
    public class TestPageHost : IPageHost
    {
        public readonly List<string> Removed = new List<string>();
        public readonly List<IControlView> Added = new List<IControlView>();
        public List<string> Log;
        public Exception ThrowOnRemove;
        public string ThrowOnRemoveTag;
        public Action<string> OnRemove;
        public Exception ThrowOnAdd;
        public Action OnAdd;
        public IControlView NextView;
        public IServiceProvider LastServices;

        public IControlView AddPage(Type viewInterface, string title, IServiceProvider services)
        {
            LastServices = services;
            OnAdd?.Invoke();
            if (ThrowOnAdd != null) throw ThrowOnAdd;
            var view = NextView ?? new FakePageView(title);
            NextView = null;
            Added.Add(view);
            return view;
        }

        public void RemovePage(IControlView page)
        {
            var tag = (page as IPageView)?.Tag ?? page.GetType().Name;
            Removed.Add(tag);
            Log?.Add("remove:" + tag);
            OnRemove?.Invoke(tag);
            if (ThrowOnRemove != null && (ThrowOnRemoveTag == null || ThrowOnRemoveTag == tag)) throw ThrowOnRemove;
        }
    }

    public sealed class FakeWorkspaceView : TestPageHost, IWorkspaceView
    {
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
        public IPageHost Pages => this;
    }

    /// <summary>A window that owns one stack (created in its constructor) and exposes it to the test.</summary>
    public sealed class WorkspacePresenter : WindowPresenterBase<IWorkspaceView>
    {
        private readonly List<string> _log;
        public readonly PageStack Stack;
        public Action<WorkspacePresenter> OnInit;
        public Func<CloseReason, Task<bool>> WindowAnswer = _ => Task.FromResult(true);
        public readonly List<IDisposable> Handles = new List<IDisposable>();

        public WorkspacePresenter(List<string> log = null)
        {
            _log = log;
            Stack = new PageStack(this, () => View.Pages);
        }

        protected override void RegisterViewActions()
            => Dispatcher.Register(WorkspaceActions.Breadcrumb, (int level) => Stack.PopToCountAsync(level + 1));

        protected override void OnInitialize() => OnInit?.Invoke(this);

        protected override Task<bool> CanCloseAsync(CloseReason reason)
        {
            _log?.Add("ask:window");
            return WindowAnswer(reason);
        }

        internal bool Push(PlainPage page) => Stack.Push(page, page.Tag);

        internal bool Push(ParamPage page, string tag, PageParams parameters) => Stack.Push(page, parameters, tag);

        /// <summary>Plain AttachChild (not a page), e.g. a panel that owns a stack of its own.</summary>
        internal IDisposable AttachPanel(PlainPage panel)
        {
            var handle = AttachChild(panel, new FakePageView(panel.Tag));
            Handles.Add(handle);
            return handle;
        }

        internal void RequestCloseForTest() => RequestClose(InteractionStatus.Cancel);
    }

    /// <summary>An <see cref="IPresenter"/> that is not a framework presenter.</summary>
    public sealed class ForeignPresenter : IPresenter
    {
        public Type ViewInterfaceType => typeof(IPageView);
        public void Dispose() { }
    }
}
