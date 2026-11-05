using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            SubscribeViewEvent<string>(h => View.CategorySelected += h, h => View.CategorySelected -= h,
                id => Dispatcher.Dispatch(ShellActions.SelectCategory, id));
            SubscribeViewEvent<string>(h => View.DemoSelected += h, h => View.DemoSelected -= h, OnDemoSelected);

            View.ShowCategories(_categories);
            var start = _categories.FirstOrDefault(c => c.Id == _startCategoryId) ?? _categories.FirstOrDefault();
            if (start != null)
                View.SelectCategory(start.Id);
        }

        protected override void Cleanup() => DetachDemos();

        private async Task SwitchToAsync(string categoryId)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null || category == _current)
                return;

            // Ask every demo that can refuse before disposing any (the newest first, like disposal).
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
                    if (_current != null) View.SelectCategory(_current.Id);   // the sidebar goes back
                    return;
                }
            }

            DetachDemos();
            _current = category;
            View.ShowCategory(category);
            View.ShowSource(category.SourceFiles.Concat(category.Demos.SelectMany(d => d.SourceFiles)).Distinct().ToList());
        }

        private void OnDemoSelected(string demoId)
        {
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
        private void DetachDemos()
        {
            for (var i = _attached.Count - 1; i >= 0; i--)
                _attached[i].Handle.Dispose();
            _attached.Clear();
            View?.RemoveDemoViews();
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
