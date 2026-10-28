using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Presenters;
using Kibinago.Views;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// The sample's main window. Each embedded demo is a child presenter (<c>AttachChild</c>) created the
    /// first time its tab is opened; switching to another category disposes them (presenters first, then
    /// their views), so coming back starts every demo afresh.
    /// </summary>
    public sealed class ShellPresenter : WindowPresenterBase<IShellView>
    {
        private readonly IReadOnlyList<SampleCategory> _categories;
        private readonly string _startCategoryId;
        private readonly List<(string DemoId, IDisposable Handle)> _attached = new List<(string, IDisposable)>();   // in attach order
        private SampleCategory _current;

        /// <param name="categories">The sidebar, in order.</param>
        /// <param name="startCategoryId">The category selected at start (default: the first).</param>
        public ShellPresenter(IReadOnlyList<SampleCategory> categories, string startCategoryId = null)
        {
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _startCategoryId = startCategoryId;
        }

        protected override void OnInitialize()
        {
            SubscribeViewEvent<string>(h => View.CategorySelected += h, h => View.CategorySelected -= h, OnCategorySelected);
            SubscribeViewEvent<string>(h => View.DemoSelected += h, h => View.DemoSelected -= h, OnDemoSelected);

            View.ShowCategories(_categories);
            var start = _categories.FirstOrDefault(c => c.Id == _startCategoryId) ?? _categories.FirstOrDefault();
            if (start != null)
                View.SelectCategory(start.Id);
        }

        protected override void Cleanup() => DetachDemos();

        private void OnCategorySelected(string categoryId)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null || category == _current)
                return;

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
                _attached.Add((demo.Id, demo.Attach(new Attacher(this), view)));
            }
            catch
            {
                View.RemoveDemoView(demo.Id);   // AttachChild disposed the presenter; the control is ours to remove
                throw;
            }
        }

        // Presenters first (their Cleanup may still use the views), then the controls.
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

            public IDisposable Attach<TView>(ControlPresenterBase<TView> presenter, TView view) where TView : IViewBase
                => _owner.AttachChild(presenter, view);
        }
    }
}
