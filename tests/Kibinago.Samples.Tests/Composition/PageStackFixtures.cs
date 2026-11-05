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
        public Action<PlainPage> OnInit;
        public Exception ThrowOnInitialize;
        public Exception ThrowOnCleanup;
        public bool Disposed;

        public PlainPage(string tag, List<string> log, bool ownsStack = false)
        {
            Tag = tag;
            Log = log;
            if (ownsStack) Inner = new PageStack(this);
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
            if (ThrowOnCleanup != null) throw ThrowOnCleanup;
        }

        public object GetMessagesForTest() => Messages;

        /// <summary>Plain AttachChild of a panel into this page (what a page's own code would do).</summary>
        public IDisposable AttachPanelForTest(PlainPage panel) => AttachChild(panel, new FakePageView(panel.Tag));

        /// <summary>A stack created by this page's own code (e.g. inside its guard).</summary>
        public PageStack CreateStackForTest() => new PageStack(this);

        /// <summary>Pushes onto this page's own stack (test access; a real page calls it from its own code).</summary>
        public void PushInner(PlainPage page, List<string> removed)
            => Inner.Push(page, new FakePageView(page.Tag), v => removed?.Add("remove:" + v.Tag));
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
        void RemovePage(IPageView page);
    }

    public sealed class FakeWorkspaceView : IWorkspaceView
    {
        public readonly List<string> Removed = new List<string>();
        public List<string> Log;
        public Exception ThrowOnRemove;
        public string ThrowOnRemoveTag;
        public Action<string> OnRemove;
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;

        public void RemovePage(IPageView page)
        {
            Removed.Add(page.Tag);
            Log?.Add("remove:" + page.Tag);
            OnRemove?.Invoke(page.Tag);
            if (ThrowOnRemove != null && (ThrowOnRemoveTag == null || ThrowOnRemoveTag == page.Tag)) throw ThrowOnRemove;
        }
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
            Stack = new PageStack(this);
        }

        protected override void RegisterViewActions()
            => Dispatcher.Register(WorkspaceActions.Breadcrumb, (int level) => Stack.PopToCountAsync(level + 1));

        protected override void OnInitialize() => OnInit?.Invoke(this);

        protected override Task<bool> CanCloseAsync(CloseReason reason)
        {
            _log?.Add("ask:window");
            return WindowAnswer(reason);
        }

        public void Push(PlainPage page) => Stack.Push(page, new FakePageView(page.Tag), View.RemovePage);

        public void Push(ParamPage page, string tag, PageParams parameters)
            => Stack.Push(page, new FakePageView(tag), parameters, View.RemovePage);

        /// <summary>Plain AttachChild (not a page), e.g. a panel that owns a stack of its own.</summary>
        public IDisposable AttachPanel(PlainPage panel)
        {
            var handle = AttachChild(panel, new FakePageView(panel.Tag));
            Handles.Add(handle);
            return handle;
        }

        public void RequestCloseForTest() => RequestClose(InteractionStatus.Cancel);
    }

    /// <summary>An <see cref="IPresenter"/> that is not a framework presenter.</summary>
    public sealed class ForeignPresenter : IPresenter
    {
        public Type ViewInterfaceType => typeof(IPageView);
        public void Dispose() { }
    }
}
