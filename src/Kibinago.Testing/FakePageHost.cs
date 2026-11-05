using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Views;

namespace Kibinago.Testing
{
    /// <summary>
    /// An <see cref="IPageHost"/> for presenter tests: creates the mock page views registered with
    /// <see cref="Register{TView}"/> and records what the <c>PageStack</c> did. Expose it from the mock parent view
    /// (<c>public IPageHost Pages =&gt; Host;</c>) and assert on <see cref="Pages"/>, <see cref="PageTitles"/> and
    /// <see cref="Removed"/>; drive a page through its mock view (<see cref="Page{T}(int)"/>).
    /// </summary>
    public sealed class FakePageHost : IPageHost
    {
        private readonly Dictionary<Type, Func<IControlView>> _factories = new Dictionary<Type, Func<IControlView>>();
        private readonly List<IControlView> _pages = new List<IControlView>();
        private readonly List<string> _titles = new List<string>();
        private readonly List<IControlView> _removed = new List<IControlView>();

        /// <summary>Makes <paramref name="create"/> the source of new views for <typeparamref name="TView"/>.</summary>
        /// <typeparam name="TView">The page's view interface (as the presenter's <c>Push</c> names it).</typeparam>
        /// <param name="create">Creates a new mock view on every <see cref="AddPage"/>.</param>
        /// <returns>This host, for chaining.</returns>
        public FakePageHost Register<TView>(Func<TView> create) where TView : class, IControlView
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            _factories[typeof(TView)] = () => create();
            return this;
        }

        /// <summary>The pages shown, bottom first.</summary>
        public IReadOnlyList<IControlView> Pages => _pages;

        /// <summary>The pages' breadcrumb texts, bottom first.</summary>
        public IReadOnlyList<string> PageTitles => _titles;

        /// <summary>The views removed, in removal order.</summary>
        public IReadOnlyList<IControlView> Removed => _removed;

        /// <summary>The view at <paramref name="level"/> (0 = the first page) as <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The mock view type.</typeparam>
        /// <param name="level">The page's level.</param>
        /// <returns>The view.</returns>
        public T Page<T>(int level) where T : class => (T)_pages[level];

        /// <inheritdoc />
        public IControlView AddPage(Type viewInterface, string title, IServiceProvider services)
        {
            if (viewInterface == null) throw new ArgumentNullException(nameof(viewInterface));
            if (!_factories.TryGetValue(viewInterface, out var create))
                throw new InvalidOperationException(
                    "No mock view is registered for " + viewInterface.Name + ": call Register<" + viewInterface.Name + ">(() => new …) on the FakePageHost.");
            var view = create() ?? throw new InvalidOperationException("The mock view factory for " + viewInterface.Name + " returned null.");
            _pages.Add(view);
            _titles.Add(title);
            return view;
        }

        /// <inheritdoc />
        public void RemovePage(IControlView page)
        {
            int index = _pages.FindIndex(p => ReferenceEquals(p, page));
            if (index < 0) throw new ArgumentException("This host did not add that page (or removed it already).", nameof(page));
            _pages.RemoveAt(index);
            _titles.RemoveAt(index);
            _removed.Add(page);
        }
    }
}
