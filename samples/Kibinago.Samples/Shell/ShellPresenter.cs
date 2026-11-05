using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.Shell
{
    public static class ShellActions
    {
        /// <summary>A category was chosen in the sidebar; payload = its id.</summary>
        public static readonly ViewAction<string> SelectCategory = ViewAction.Create<string>("Shell.SelectCategory");
    }

    /// <summary>
    /// The sample's main window. Each embedded demo is a child presenter (<c>AttachChild</c>) created the
    /// first time its tab is opened; switching to another category disposes them (presenters first, then
    /// their views), so coming back starts every demo afresh. A demo that can refuse to be discarded
    /// (<see cref="IPageCloseGuard"/>, e.g. unsaved edits in the page navigation demo) is asked first; if one
    /// refuses, every demo stays and the sidebar goes back.
    /// </summary>
    public sealed class ShellPresenter : WindowPresenterBase<IShellView>
    {
        private readonly IReadOnlyList<SampleCategory> _categories;
        private readonly string _startCategoryId;
        // In attach order; Guard is null for a demo whose presenter cannot refuse.
        private readonly List<(string DemoId, IDisposable Handle, IPageCloseGuard Guard)> _attached =
            new List<(string, IDisposable, IPageCloseGuard)>();
        private SampleCategory _current;

        /// <param name="categories">The sidebar, in order.</param>
        /// <param name="startCategoryId">The category selected at start (default: the first).</param>
        public ShellPresenter(IReadOnlyList<SampleCategory> categories, string startCategoryId = null)
        {
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _startCategoryId = startCategoryId;
        }

        // An async action: while a switch is deciding (a demo's question is open) further switches are ignored.
        protected override void RegisterViewActions()
            => Dispatcher.Register(ShellActions.SelectCategory, (string id) => SwitchToAsync(id));

        protected override void OnInitialize()
        {
            SubscribeViewEvent<string>(h => View.CategorySelected += h, h => View.CategorySelected -= h, id =>
            {
                _sidebarSelection = id;   // what the sidebar shows now, whether or not the switch happens
                Dispatcher.Dispatch(ShellActions.SelectCategory, id);
            });
            SubscribeViewEvent<string>(h => View.DemoSelected += h, h => View.DemoSelected -= h, OnDemoSelected);

            View.ShowCategories(_categories);
            var start = _categories.FirstOrDefault(c => c.Id == _startCategoryId) ?? _categories.FirstOrDefault();
            if (start != null)
                View.SelectCategory(start.Id);
        }

        protected override void Cleanup() => Throw(DetachDemos());

        // While a switch is deciding (a demo's question is open) the window does not close either: the demos
        // that agreed would be disposed by the close without the others having answered.
        protected override bool CanClose(CloseReason reason) => !_switching;

        private bool _switching;
        private string _sidebarSelection;
        private string _demoSelectedWhileSwitching;

        private async Task SwitchToAsync(string categoryId)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null || category == _current)
                return;

            _switching = true;
            try
            {
                // Ask every demo that can refuse before disposing any (the newest first, like disposal).
                // No demo is attached meanwhile (OnDemoSelected waits), so this is every demo to be disposed.
                foreach (var guard in _attached.Select(a => a.Guard).Where(g => g != null).Reverse().ToList())
                {
                    bool agreed;
                    try { agreed = await (guard.CanCloseAsync() ?? Task.FromResult(false)); }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "A demo failed to answer whether it may be closed; staying in the category");
                        agreed = false;
                    }
                    if (Lifetime.IsCancellationRequested) return;   // the window closed meanwhile
                    if (!agreed)
                    {
                        _switching = false;
                        SyncSidebar();   // the sidebar goes back
                        // A demo tab chosen while the switch was deciding is shown now: open its demo.
                        var deferred = _demoSelectedWhileSwitching;
                        _demoSelectedWhileSwitching = null;
                        if (deferred != null) OnDemoSelected(deferred);
                        return;
                    }
                }

                var failures = DetachDemos();
                if (Lifetime.IsCancellationRequested) { Throw(failures); return; }
                _current = category;
                View.ShowCategory(category);
                View.ShowSource(category.SourceFiles.Concat(category.Demos.SelectMany(d => d.SourceFiles)).Distinct().ToList());
                SyncSidebar();
                Throw(failures);
            }
            finally
            {
                _switching = false;
                _demoSelectedWhileSwitching = null;
            }
        }

        // A click ignored while a switch was deciding left the sidebar on another category: show the current one.
        private void SyncSidebar()
        {
            if (_current != null && _sidebarSelection != _current.Id) View.SelectCategory(_current.Id);
        }

        private void OnDemoSelected(string demoId)
        {
            if (_switching)
            {
                // No demo joins the ones a deciding switch is about to dispose; opened if the switch is refused.
                _demoSelectedWhileSwitching = demoId;
                return;
            }
            var demo = _current?.Demos.FirstOrDefault(d => d.Id == demoId);
            if (demo == null)
                return;

            View.ShowSource(demo.SourceFiles);
            if (demo.Kind != DemoKind.Embedded || _attached.Any(a => a.DemoId == demo.Id))
                return;

            var view = View.CreateDemoView(demo.Id);
            try
            {
                var attacher = new Attacher(this);
                var handle = demo.Attach(attacher, view);
                _attached.Add((demo.Id, handle, attacher.Presenter as IPageCloseGuard));
            }
            catch
            {
                View.RemoveDemoView(demo.Id);   // AttachChild disposed the presenter; the control is ours to remove
                throw;
            }
        }

        // Presenters first (their Cleanup may still use the views), then the controls. Disposal cannot be
        // vetoed: closing the window (Cleanup) disposes every demo; the window's close gate has asked the pages.
        // The bookkeeping is cleared first and every step runs even when one throws; the failures are returned.
        private List<Exception> DetachDemos()
        {
            var attached = _attached.ToList();
            _attached.Clear();
            var failures = new List<Exception>();
            for (var i = attached.Count - 1; i >= 0; i--)
            {
                try { attached[i].Handle.Dispose(); }
                catch (Exception ex) { failures.Add(ex); }
            }
            try { View?.RemoveDemoViews(); }
            catch (Exception ex) { failures.Add(ex); }
            return failures;
        }

        private static void Throw(List<Exception> failures)
        {
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        // Lets a catalog entry call the protected AttachChild with its own presenter and view types.
        private sealed class Attacher : IDemoAttacher
        {
            private readonly ShellPresenter _owner;

            public Attacher(ShellPresenter owner) => _owner = owner;

            /// <summary>The presenter the catalog entry attached (to keep its guard next to the handle).</summary>
            public object Presenter { get; private set; }

            public IDisposable Attach<TView>(ControlPresenterBase<TView> presenter, TView view) where TView : IControlView
            {
                var handle = _owner.AttachChild(presenter, view);
                Presenter = presenter;
                return handle;
            }
        }
    }
}
