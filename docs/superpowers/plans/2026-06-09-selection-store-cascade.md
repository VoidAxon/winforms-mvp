# SelectionStore + Cascade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two framework primitives — `ISelectionStore<T>`/`SelectionStore<T>` and `Cascade` — that collapse N hand-written selection services and the per-level "subscribe → clear → reload" boilerplate into one generic store + a one-line `Cascade.Bind`, with automatic downstream clearing.

**Architecture:** Pure in-memory, synchronous, zero-dependency primitives in `WinformsMVP.Common` (same layer as `ChangeTracker`). A `SelectionStore<T>` holds one selection and raises `CurrentChanged`; `Cascade.Bind` subscribes a child store to a parent store, clears the child on parent change (which cascades further down via the same mechanism) and reloads the child's list. A 3-level `CascadeDemo` sample validates the real-control concern (list repopulation must not feed a synthetic selection back into the store).

**Tech Stack:** C# (net40;net48), WinForms, xUnit 2.9.3. Spec: `docs/superpowers/specs/2026-06-09-selection-store-cascade-design.md`.

---

## File Structure

**Framework primitives (shipped in `WinformsMVP`):**
- Create `src/WinformsMVP/Common/SelectionStore.cs` — `ISelectionStore<T>` + `SelectionStore<T>`.
- Create `src/WinformsMVP/Common/Cascade.cs` — static `Cascade` helper (`Bind` single-parent + `Combine` multi-parent).

**Tests:**
- Create `tests/WinformsMVP.Samples.Tests/Common/SelectionStoreTests.cs`
- Create `tests/WinformsMVP.Samples.Tests/Common/CascadeTests.cs`

**Sample `CascadeDemo` (not packaged):**
- Create `samples/WinformsMVP.Samples/CascadeDemo/CascadeModels.cs` — `Category`/`SubCategory`/`Product` + repository interfaces + in-memory impls.
- Create `samples/WinformsMVP.Samples/CascadeDemo/SelectListView.cs` — generic `ISelectListView<T>` + `SelectListControl<T>` (a ListBox-backed UserControl that suppresses its selection event during repopulation — spec §5.1).
- Create `samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs` — `CategoryListPresenter`/`SubCategoryListPresenter`/`ProductListPresenter`.
- Create `samples/WinformsMVP.Samples/CascadeDemo/CascadeForm.cs` — host Form + composition root.
- Create `samples/WinformsMVP.Samples/CascadeDemo/Program.cs` — `CascadeDemoProgram.Run()`.
- Modify `samples/WinformsMVP.Samples/SampleLauncherForm.cs` — add a launcher button.

**Docs:**
- Create `wiki/HowTo-Handle-Cascading-Selection.md` (Japanese).
- Modify `wiki/_Sidebar.md`, `wiki/HowTo-Implement-Master-Detail.md`, `wiki/HowTo-Communicate-Between-Presenters.md` — add links.
- Modify `CHANGELOG.md` — `[Unreleased]` Added entry.

---

## Task 1: `ISelectionStore<T>` + `SelectionStore<T>`

**Files:**
- Create: `src/WinformsMVP/Common/SelectionStore.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Common/SelectionStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/WinformsMVP.Samples.Tests/Common/SelectionStoreTests.cs`:

```csharp
using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class SelectionStoreTests
    {
        private sealed class Item
        {
            public int Id;
            public Item(int id) { Id = id; }
        }

        private sealed class ByIdComparer : IEqualityComparer<Item>
        {
            public bool Equals(Item x, Item y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                return x.Id == y.Id;
            }
            public int GetHashCode(Item obj) { return obj == null ? 0 : obj.Id; }
        }

        [Fact]
        public void Select_ChangingValue_RaisesCurrentChangedOnce()
        {
            var store = new SelectionStore<Item>();
            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;

            var a = new Item(1);
            store.Select(a);

            Assert.Same(a, store.Current);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void Select_SameReference_DoesNotRaise()
        {
            var store = new SelectionStore<Item>();
            var a = new Item(1);
            store.Select(a);

            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;
            store.Select(a);

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Select_Null_ClearsCurrent()
        {
            var store = new SelectionStore<Item>();
            store.Select(new Item(1));

            store.Select(null);

            Assert.Null(store.Current);
        }

        [Fact]
        public void Select_WithComparer_TreatsEqualValuesAsUnchanged()
        {
            var store = new SelectionStore<Item>(new ByIdComparer());
            store.Select(new Item(7));

            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;
            store.Select(new Item(7));   // different instance, equal by Id

            Assert.Equal(0, raised);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~SelectionStoreTests"`
Expected: FAIL — `SelectionStore` / `ISelectionStore` do not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `src/WinformsMVP/Common/SelectionStore.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Holds a single "current selection" of type <typeparamref name="T"/> and raises
    /// <see cref="CurrentChanged"/> when it changes. One generic store replaces the N
    /// same-shaped, hand-written selection services a cascading-selection screen would
    /// otherwise need (one per level). See <see cref="Cascade"/>.
    /// </summary>
    public interface ISelectionStore<T> where T : class
    {
        /// <summary>The current selection, or <c>null</c> if nothing is selected.</summary>
        T Current { get; }

        /// <summary>Sets the current selection. <c>Select(null)</c> clears it.</summary>
        void Select(T item);

        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        event EventHandler CurrentChanged;
    }

    /// <summary>
    /// Default in-memory <see cref="ISelectionStore{T}"/>. Synchronous; not thread-safe
    /// (selection is a UI-thread concern). Pass an <see cref="IEqualityComparer{T}"/> to
    /// compare by value instead of by reference.
    /// </summary>
    public sealed class SelectionStore<T> : ISelectionStore<T> where T : class
    {
        private readonly IEqualityComparer<T> _comparer;

        // Defaults to EqualityComparer<T>.Default. Give entities Id-based Equals (or pass a
        // comparer) so a reloaded list — which holds NEW instances — still matches "the same" row.
        public SelectionStore(IEqualityComparer<T> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Current { get; private set; }

        public void Select(T item)
        {
            // Short-circuit when unchanged so clearing an already-empty downstream level does not
            // fan out a pointless cascade. Null-safe: never calls the comparer with a null operand.
            bool same = (Current == null && item == null)
                     || (Current != null && item != null && _comparer.Equals(Current, item));
            if (same) return;

            Current = item;
            var handler = CurrentChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public event EventHandler CurrentChanged;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~SelectionStoreTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Common/SelectionStore.cs tests/WinformsMVP.Samples.Tests/Common/SelectionStoreTests.cs
git commit -m "feat(common): add ISelectionStore<T> / SelectionStore<T>"
```

---

## Task 2: `Cascade.Bind` and `Cascade.Combine`

**Files:**
- Create: `src/WinformsMVP/Common/Cascade.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Common/CascadeTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/WinformsMVP.Samples.Tests/Common/CascadeTests.cs`:

```csharp
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class CascadeTests
    {
        private sealed class Node
        {
            public int Id;
            public Node(int id) { Id = id; }
        }

        [Fact]
        public void Bind_ParentChange_ClearsChildAndReloadsWithParentValue()
        {
            var parent = new SelectionStore<Node>();
            var child = new SelectionStore<Node>();
            child.Select(new Node(99));                 // pre-existing child selection

            Node reloadedWith = null;
            int reloadCount = 0;
            // initialSync:false so we count only the change-driven reload
            Cascade.Bind(parent, child, p => { reloadCount++; reloadedWith = p; }, initialSync: false);

            var c = new Node(1);
            parent.Select(c);

            Assert.Null(child.Current);                 // child cleared
            Assert.Equal(1, reloadCount);
            Assert.Same(c, reloadedWith);               // reloaded with the new parent value
        }

        [Fact]
        public void Bind_InitialSync_ReloadsOnceAtBindWithCurrentParent()
        {
            var parent = new SelectionStore<Node>();
            var p0 = new Node(5);
            parent.Select(p0);
            var child = new SelectionStore<Node>();

            Node reloadedWith = null;
            int reloadCount = 0;
            Cascade.Bind(parent, child, p => { reloadCount++; reloadedWith = p; });   // initialSync default true

            Assert.Equal(1, reloadCount);
            Assert.Same(p0, reloadedWith);
        }

        [Fact]
        public void Bind_InitialSyncFalse_DoesNotReloadAtBind()
        {
            var parent = new SelectionStore<Node>();
            parent.Select(new Node(5));
            var child = new SelectionStore<Node>();
            int reloadCount = 0;

            Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_Dispose_StopsReacting()
        {
            var parent = new SelectionStore<Node>();
            var child = new SelectionStore<Node>();
            int reloadCount = 0;
            var binding = Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            binding.Dispose();
            parent.Select(new Node(1));

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_ThreeLevels_TopChange_ClearsAndReloadsEachLevelOnce()
        {
            var a = new SelectionStore<Node>();   // top
            var b = new SelectionStore<Node>();   // middle
            var c = new SelectionStore<Node>();   // leaf
            b.Select(new Node(20));
            c.Select(new Node(30));

            int reloadB = 0, reloadC = 0;
            Node bReloadArg = new Node(-1), cReloadArg = new Node(-1);
            Cascade.Bind(a, b, p => { reloadB++; bReloadArg = p; }, initialSync: false);
            Cascade.Bind(b, c, p => { reloadC++; cReloadArg = p; }, initialSync: false);

            a.Select(new Node(1));

            Assert.Null(b.Current);
            Assert.Null(c.Current);
            Assert.Equal(1, reloadB);
            Assert.Equal(1, reloadC);
            Assert.Same(a.Current, bReloadArg);   // b reloaded with the new top value
            Assert.Null(cReloadArg);              // c reloaded with b's value, which is now null
        }

        [Fact]
        public void Bind_ClearingAlreadyEmptyChild_DoesNotReloadGrandchildExtraTimes()
        {
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var c = new SelectionStore<Node>();
            // b and c start empty (null)

            int reloadC = 0;
            Cascade.Bind(a, b, p => { }, initialSync: false);
            Cascade.Bind(b, c, p => reloadC++, initialSync: false);

            a.Select(new Node(1));   // clears b (already null -> short-circuit, no fire) then reloads b

            Assert.Equal(0, reloadC);   // grandchild reload never triggered (b stayed null)
        }

        [Fact]
        public void Combine_EitherParentChange_ClearsTargetAndReloadsWithBoth()
        {
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var target = new SelectionStore<Node>();
            target.Select(new Node(7));

            Node lastA = null, lastB = null;
            int reloadCount = 0;
            Cascade.Combine(a, b, target,
                (av, bv) => { reloadCount++; lastA = av; lastB = bv; }, initialSync: false);

            var a1 = new Node(1);
            a.Select(a1);
            Assert.Null(target.Current);          // cleared on a change
            Assert.Equal(1, reloadCount);
            Assert.Same(a1, lastA);
            Assert.Null(lastB);

            var b1 = new Node(2);
            b.Select(b1);
            Assert.Equal(2, reloadCount);
            Assert.Same(a1, lastA);               // a's current still present
            Assert.Same(b1, lastB);
        }

        [Fact]
        public void Combine_Dispose_UnsubscribesBothParents()
        {
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var target = new SelectionStore<Node>();
            int reloadCount = 0;
            var binding = Cascade.Combine(a, b, target, (av, bv) => reloadCount++, initialSync: false);

            binding.Dispose();
            a.Select(new Node(1));
            b.Select(new Node(2));

            Assert.Equal(0, reloadCount);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~CascadeTests"`
Expected: FAIL — `Cascade` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `src/WinformsMVP/Common/Cascade.cs`:

```csharp
using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Wires parent-&gt;child (and multi-parent) selection cascades in one declaration. Use for
    /// master/detail and N-level cascading selection (category -&gt; subcategory -&gt; product, etc.).
    /// </summary>
    public static class Cascade
    {
        /// <summary>
        /// When <paramref name="from"/> changes: clears <paramref name="target"/> (whose
        /// own <c>CurrentChanged</c> cascades the clear further down) and then reloads
        /// <paramref name="target"/>'s list from the new parent value via <paramref name="reload"/>.
        /// </summary>
        /// <remarks>
        /// Synchronous and single-pass: the clear propagates depth-first before the reload runs.
        /// <paramref name="reload"/> receives <c>from.Current</c>, which may be <c>null</c> when the
        /// parent itself was cleared by its own parent (reload then empties the list). The clear
        /// happens before reload but does not touch <paramref name="from"/>, so the value passed is
        /// the new parent value. <paramref name="reload"/> should not throw; if it does, the child is
        /// already cleared but not reloaded. Dispose the returned token to unsubscribe.
        /// </remarks>
        public static IDisposable Bind<TParent, TChild>(
            ISelectionStore<TParent> from,
            ISelectionStore<TChild> target,
            Action<TParent> reload,
            bool initialSync = true)
            where TParent : class
            where TChild : class
        {
            if (from == null) throw new ArgumentNullException("from");
            if (target == null) throw new ArgumentNullException("target");
            if (reload == null) throw new ArgumentNullException("reload");

            EventHandler handler = delegate
            {
                target.Select(null);    // clear self -> fires target.CurrentChanged -> downstream Bind clears next
                reload(from.Current);    // from.Current may be null (parent cleared) -> reload empties
            };
            from.CurrentChanged += handler;
            // initialSync: reload once now so a child bound after its parent already has a value is
            // not left empty. Each level self-syncs; order-independent. Clears nothing (no selection yet).
            if (initialSync) reload(from.Current);
            return new Unsubscriber(delegate { from.CurrentChanged -= handler; });
        }

        /// <summary>
        /// Multi-parent cascade: <paramref name="target"/> depends on BOTH <paramref name="a"/> and
        /// <paramref name="b"/>. Either parent changing clears <paramref name="target"/> and reloads
        /// with both current values. (<see cref="Bind{TParent,TChild}"/> is the single-parent case.)
        /// </summary>
        public static IDisposable Combine<TA, TB, TChild>(
            ISelectionStore<TA> a,
            ISelectionStore<TB> b,
            ISelectionStore<TChild> target,
            Action<TA, TB> reload,
            bool initialSync = true)
            where TA : class
            where TB : class
            where TChild : class
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (target == null) throw new ArgumentNullException("target");
            if (reload == null) throw new ArgumentNullException("reload");

            EventHandler handler = delegate
            {
                target.Select(null);
                reload(a.Current, b.Current);
            };
            a.CurrentChanged += handler;
            b.CurrentChanged += handler;
            if (initialSync) reload(a.Current, b.Current);
            return new Unsubscriber(delegate
            {
                a.CurrentChanged -= handler;
                b.CurrentChanged -= handler;
            });
        }

        private sealed class Unsubscriber : IDisposable
        {
            private Action _dispose;
            public Unsubscriber(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;
                if (d != null) d();
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~CascadeTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Common/Cascade.cs tests/WinformsMVP.Samples.Tests/Common/CascadeTests.cs
git commit -m "feat(common): add Cascade.Bind / Cascade.Combine with initial sync"
```

---

## Task 3: CascadeDemo — models and repositories

**Files:**
- Create: `samples/WinformsMVP.Samples/CascadeDemo/CascadeModels.cs`

- [ ] **Step 1: Write the file**

Create `samples/WinformsMVP.Samples/CascadeDemo/CascadeModels.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace WinformsMVP.Samples.CascadeDemo
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class SubCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class Product
    {
        public int Id { get; set; }
        public int SubCategoryId { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public interface ICategoryRepository { IList<Category> GetAll(); }
    public interface ISubCategoryRepository { IList<SubCategory> GetByCategory(int categoryId); }
    public interface IProductRepository { IList<Product> GetBySubCategory(int subCategoryId); }

    /// <summary>In-memory sample data: 2 categories x 2 subcategories x 2 products.</summary>
    public sealed class InMemoryCatalog :
        ICategoryRepository, ISubCategoryRepository, IProductRepository
    {
        private readonly List<Category> _categories = new List<Category>
        {
            new Category { Id = 1, Name = "Electronics" },
            new Category { Id = 2, Name = "Books" },
        };

        private readonly List<SubCategory> _subCategories = new List<SubCategory>
        {
            new SubCategory { Id = 11, CategoryId = 1, Name = "Laptops" },
            new SubCategory { Id = 12, CategoryId = 1, Name = "Phones" },
            new SubCategory { Id = 21, CategoryId = 2, Name = "Fiction" },
            new SubCategory { Id = 22, CategoryId = 2, Name = "Tech" },
        };

        private readonly List<Product> _products = new List<Product>
        {
            new Product { Id = 111, SubCategoryId = 11, Name = "UltraBook 14" },
            new Product { Id = 112, SubCategoryId = 11, Name = "WorkStation 17" },
            new Product { Id = 121, SubCategoryId = 12, Name = "Phone X" },
            new Product { Id = 122, SubCategoryId = 12, Name = "Phone Mini" },
            new Product { Id = 211, SubCategoryId = 21, Name = "The Novel" },
            new Product { Id = 212, SubCategoryId = 21, Name = "Short Stories" },
            new Product { Id = 221, SubCategoryId = 22, Name = "Clean Code" },
            new Product { Id = 222, SubCategoryId = 22, Name = "The Pragmatic Programmer" },
        };

        public IList<Category> GetAll() { return _categories.ToList(); }
        public IList<SubCategory> GetByCategory(int categoryId)
        {
            return _subCategories.Where(s => s.CategoryId == categoryId).ToList();
        }
        public IList<Product> GetBySubCategory(int subCategoryId)
        {
            return _products.Where(p => p.SubCategoryId == subCategoryId).ToList();
        }
    }
}
```

- [ ] **Step 2: Build the samples project**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded (0 errors).

- [ ] **Step 3: Commit**

```bash
git add samples/WinformsMVP.Samples/CascadeDemo/CascadeModels.cs
git commit -m "feat(samples): add CascadeDemo models and in-memory catalog"
```

---

## Task 4: CascadeDemo — generic list view + control (spec §5.1 guard)

**Files:**
- Create: `samples/WinformsMVP.Samples/CascadeDemo/SelectListView.cs`

- [ ] **Step 1: Write the file**

Create `samples/WinformsMVP.Samples/CascadeDemo/SelectListView.cs`. The control suppresses its
own selection event while the presenter repopulates `Items`, which is the spec §5.1 fix (a list
repopulation must NOT raise a synthetic user-selection that feeds back into the store):

```csharp
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.CascadeDemo
{
    /// <summary>A list view of <typeparamref name="T"/> with single selection.</summary>
    public interface ISelectListView<T> : IViewBase where T : class
    {
        /// <summary>Replaces the list. Setting this MUST NOT raise <see cref="SelectionChanged"/>.</summary>
        IList<T> Items { set; }

        /// <summary>The user-selected item, or null.</summary>
        T Selected { get; }

        /// <summary>Raised only when the USER changes the selection (not on repopulation).</summary>
        event EventHandler SelectionChanged;
    }

    /// <summary>ListBox-backed control. A title label on top, the list filling the rest.</summary>
    public sealed class SelectListControl<T> : UserControl, ISelectListView<T> where T : class
    {
        private readonly ListBox _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label _title = new Label { Dock = DockStyle.Top, Height = 22 };
        private bool _suppress;

        public SelectListControl(string title)
        {
            _title.Text = title;
            Controls.Add(_list);
            Controls.Add(_title);
            // Spec §5.1: only a genuine user change should flow to the store.
            _list.SelectedIndexChanged += delegate
            {
                if (_suppress) return;
                var h = SelectionChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
        }

        public IList<T> Items
        {
            set
            {
                _suppress = true;
                try
                {
                    _list.Items.Clear();
                    if (value != null)
                        foreach (var item in value)
                            _list.Items.Add(item);
                    _list.ClearSelected();
                }
                finally { _suppress = false; }
            }
        }

        public T Selected { get { return _list.SelectedItem as T; } }

        public event EventHandler SelectionChanged;
    }
}
```

- [ ] **Step 2: Build the samples project**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded (0 errors).

- [ ] **Step 3: Commit**

```bash
git add samples/WinformsMVP.Samples/CascadeDemo/SelectListView.cs
git commit -m "feat(samples): add generic SelectListControl with reload-suppression (spec 5.1)"
```

---

## Task 5: CascadeDemo — presenters

**Files:**
- Create: `samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs`

- [ ] **Step 1: Write the file**

Create `samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs`. Note each presenter holds
only stores (shared Model) and a repository — never another presenter. The user-selection → store
wiring (spec §5.2) is explicit in `OnViewAttached`:

```csharp
using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.CascadeDemo
{
    // Top level: no parent. Loads its own list; publishes the user's selection to its store.
    public sealed class CategoryListPresenter : ControlPresenterBase<ISelectListView<Category>>
    {
        private readonly ISelectionStore<Category> _store;
        private readonly ICategoryRepository _repo;

        public CategoryListPresenter(ISelectionStore<Category> store, ICategoryRepository repo)
        {
            _store = store;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;   // spec §5.2: user selection -> store
        }

        protected override void OnInitialize()
        {
            View.Items = _repo.GetAll();   // initial list load belongs in OnInitialize
        }

        private void OnUserSelected(object sender, EventArgs e) { _store.Select(View.Selected); }

        protected override void Cleanup() { View.SelectionChanged -= OnUserSelected; }
    }

    // Middle level: subscribes to the parent (Category) store, reloads its own list.
    public sealed class SubCategoryListPresenter : ControlPresenterBase<ISelectListView<SubCategory>>
    {
        private readonly ISelectionStore<Category> _parent;
        private readonly ISelectionStore<SubCategory> _self;
        private readonly ISubCategoryRepository _repo;
        private IDisposable _bind;

        public SubCategoryListPresenter(
            ISelectionStore<Category> parent,
            ISelectionStore<SubCategory> self,
            ISubCategoryRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;

            if (_bind != null) _bind.Dispose();   // guard against double-attach
            _bind = Cascade.Bind(_parent, _self, category =>
            {
                try
                {
                    View.Items = category == null
                        ? new SubCategory[0]                   // net40: no Array.Empty<T>()
                        : _repo.GetByCategory(category.Id);
                }
                catch (Exception ex)
                {
                    View.Items = new SubCategory[0];           // reload self-recovers; Cascade does not roll back
                    Messages.ShowError("Failed to load subcategories: " + ex.Message, "Error");
                }
            });
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }

        protected override void Cleanup()
        {
            View.SelectionChanged -= OnUserSelected;
            if (_bind != null) _bind.Dispose();
        }
    }

    // Leaf level: subscribes to the SubCategory store. Same shape as the middle level.
    public sealed class ProductListPresenter : ControlPresenterBase<ISelectListView<Product>>
    {
        private readonly ISelectionStore<SubCategory> _parent;
        private readonly ISelectionStore<Product> _self;
        private readonly IProductRepository _repo;
        private IDisposable _bind;

        public ProductListPresenter(
            ISelectionStore<SubCategory> parent,
            ISelectionStore<Product> self,
            IProductRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;

            if (_bind != null) _bind.Dispose();
            _bind = Cascade.Bind(_parent, _self, sub =>
            {
                try
                {
                    View.Items = sub == null ? new Product[0] : _repo.GetBySubCategory(sub.Id);
                }
                catch (Exception ex)
                {
                    View.Items = new Product[0];
                    Messages.ShowError("Failed to load products: " + ex.Message, "Error");
                }
            });
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }

        protected override void Cleanup()
        {
            View.SelectionChanged -= OnUserSelected;
            if (_bind != null) _bind.Dispose();
        }
    }
}
```

- [ ] **Step 2: Build the samples project**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded (0 errors).

- [ ] **Step 3: Commit**

```bash
git add samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs
git commit -m "feat(samples): add CascadeDemo 3-level presenters"
```

---

## Task 6: CascadeDemo — host Form + composition root

**Files:**
- Create: `samples/WinformsMVP.Samples/CascadeDemo/CascadeForm.cs`
- Create: `samples/WinformsMVP.Samples/CascadeDemo/Program.cs`

- [ ] **Step 1: Write the host form (composition root)**

Create `samples/WinformsMVP.Samples/CascadeDemo/CascadeForm.cs`. The form creates the per-screen
stores (spec §5.3: stores are screen-scoped, not app-wide singletons), the repositories, the three
presenters, and `Connect`s each presenter to its control. Presenters are held in fields so they live
as long as the form:

```csharp
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.CascadeDemo
{
    public sealed class CascadeForm : Form
    {
        // held so they are not collected and are disposed with their controls
        private readonly CategoryListPresenter _categoryPresenter;
        private readonly SubCategoryListPresenter _subCategoryPresenter;
        private readonly ProductListPresenter _productPresenter;

        public CascadeForm()
        {
            Text = "Cascade Demo — Category > SubCategory > Product";
            Width = 720;
            Height = 420;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));

            var categoryControl = new SelectListControl<Category>("Category") { Dock = DockStyle.Fill };
            var subCategoryControl = new SelectListControl<SubCategory>("SubCategory") { Dock = DockStyle.Fill };
            var productControl = new SelectListControl<Product>("Product") { Dock = DockStyle.Fill };
            layout.Controls.Add(categoryControl, 0, 0);
            layout.Controls.Add(subCategoryControl, 1, 0);
            layout.Controls.Add(productControl, 2, 0);
            Controls.Add(layout);

            // Per-screen stores (spec §5.3 — not app-wide singletons).
            var categoryStore = new SelectionStore<Category>();
            var subCategoryStore = new SelectionStore<SubCategory>();
            var productStore = new SelectionStore<Product>();

            var catalog = new InMemoryCatalog();

            _categoryPresenter = new CategoryListPresenter(categoryStore, catalog);
            _subCategoryPresenter = new SubCategoryListPresenter(categoryStore, subCategoryStore, catalog);
            _productPresenter = new ProductListPresenter(subCategoryStore, productStore, catalog);

            _categoryPresenter.Connect(categoryControl);
            _subCategoryPresenter.Connect(subCategoryControl);
            _productPresenter.Connect(productControl);
        }
    }
}
```

- [ ] **Step 2: Write the entry point**

Create `samples/WinformsMVP.Samples/CascadeDemo/Program.cs`:

```csharp
namespace WinformsMVP.Samples.CascadeDemo
{
    public static class CascadeDemoProgram
    {
        public static void Run()
        {
            using (var form = new CascadeForm())
            {
                form.ShowDialog();
            }
        }
    }
}
```

- [ ] **Step 3: Build the samples project**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded (0 errors).

- [ ] **Step 4: Commit**

```bash
git add samples/WinformsMVP.Samples/CascadeDemo/CascadeForm.cs samples/WinformsMVP.Samples/CascadeDemo/Program.cs
git commit -m "feat(samples): add CascadeDemo host form and entry point"
```

---

## Task 7: Register CascadeDemo in the sample launcher

**Files:**
- Modify: `samples/WinformsMVP.Samples/SampleLauncherForm.cs`

- [ ] **Step 1: Read the launcher to find the registration pattern**

Read `samples/WinformsMVP.Samples/SampleLauncherForm.cs`. Locate how an existing demo is added
(look for `CreateDemoButton` calls or an equivalent list/section that maps a label to a launch
action — e.g. the entry that calls `ComplexInteractionDemoServiceBasedProgram.Run()`).

- [ ] **Step 2: Add the CascadeDemo entry**

Following the exact pattern found in Step 1, add an entry in the most appropriate category that
launches the demo. Add `using WinformsMVP.Samples.CascadeDemo;` if the launcher uses `using`s, or
fully-qualify. The launch action is:

```csharp
CascadeDemoProgram.Run();
```

Label it `"Cascade (N-level selection)"`. (If demos are registered as `CreateDemoButton("Label", () => Action())`, the call is `CreateDemoButton("Cascade (N-level selection)", () => CascadeDemoProgram.Run())`.)

- [ ] **Step 3: Build the samples project**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded (0 errors).

- [ ] **Step 4: Manual smoke test**

Run: `dotnet run --project samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Open the **Cascade (N-level selection)** demo. Verify:
1. Selecting a **Category** populates the SubCategory list and clears Product.
2. Selecting a **SubCategory** populates the Product list.
3. Selecting a different **Category** repopulates SubCategory AND clears the Product list (no stale products) — this exercises the §5.1 guard end-to-end (no infinite loop / no stale selection).
Close the window.

- [ ] **Step 5: Commit**

```bash
git add samples/WinformsMVP.Samples/SampleLauncherForm.cs
git commit -m "feat(samples): register CascadeDemo in the sample launcher"
```

---

## Task 8: Wiki page (Japanese)

**Files:**
- Create: `wiki/HowTo-Handle-Cascading-Selection.md`
- Modify: `wiki/_Sidebar.md`, `wiki/HowTo-Implement-Master-Detail.md`, `wiki/HowTo-Communicate-Between-Presenters.md`

- [ ] **Step 1: Author the page**

Create `wiki/HowTo-Handle-Cascading-Selection.md` in **Japanese** (wiki language). Base it on the
approved spec. It MUST include, in this order:

1. **何が難しいのか** — the two pain points (N-fold duplication; forgotten downstream clear).
2. **素朴な実装が破綻する理由** — anti-patterns: N hand-written same-shaped services; manual clear in each presenter; a coordinator presenter holding child presenters.
3. **解法: `ISelectionStore<T>` + `Cascade`** — the two collapses (N services → 1 generic; 3 steps → `Cascade.Bind`; auto downstream clear).
4. **フレームワークプリミティブ** — show the exact `ISelectionStore<T>` / `SelectionStore<T>` and `Cascade.Bind` / `Cascade.Combine` code from Tasks 1 & 2 (English code comments). Include the `initialSync` parameter and the **Id-equality ⚠️ note** (a reload swaps instances; compare by Id, not by reference).
5. **例: カテゴリ → サブカテゴリ → 商品 (3 段)** — the three presenters from Task 5 AND the composition root from Task 6. **Spec §5.2 fix:** the presenter examples MUST show `View.SelectionChanged += OnUserSelected;` wiring (not omit it). Include the synchronous cascade trace from spec §4.
6. **重載が選択を再発火しない契約 (spec §5.1)** — explain that repopulating the list must not raise the user-selection event; show the `_suppress` guard pattern from Task 4's control. State this is a View-side contract, intentionally NOT in `Cascade`.
7. **なぜ「Presenter が Presenter を持つ」にならないのか** — each presenter holds only stores + repo; link to [HowTo-Communicate-Between-Presenters § 共有 Model + イベント](HowTo-Communicate-Between-Presenters#共有-model--イベント) and to the "don't hold other presenters" anti-pattern. Keep the `_categoryStore` naming note.
8. **多父依赖 (`Cascade.Combine`)** — show `Cascade.Combine(a, b, target, (av, bv) => ...)` for a level that depends on TWO parents; either parent changing clears + reloads with both current values. Note 3+ parents = add an overload or fall back to manual multi-subscription. Give a short example.
9. **設計上の注意** — 判等は **Id で・引用ではなく**（reload が実例を入れ替える）+ 値判等の短路; `initialSync`（既定 true：消除「绑定时父已有值却停在空」、且只重载不清空→可用于状态恢复); `reload` は内部 try/catch で自我兜底（`Cascade` は回滚しない); 双绑守卫 `_bind?.Dispose()`; 初始列表加载放 `OnInitialize`、View 事件订阅放 `OnViewAttached`; **store は画面ごとにスコープ (spec §5.3, グローバル singleton にしない)**; DI 开放泛型注册注意; net40 (`new T[0]`, `IList<T>`, `EqualityComparer<T>.Default`); 将来异步化は `Func<TParent, Task>` 版 `Cascade.Bind` を追加して并存 (spec §8)。
10. **動的深度のとき** — out of scope; `DrillDownPath` escape hatch (spec §9).
11. **メリット / デメリット**, **アンチパターン**, **関連ページ** (link to Master-Detail, Communicate-Between-Presenters, DependencyInjection, Handle-Async-Operations, and サンプル `samples/WinformsMVP.Samples/CascadeDemo/`).

- [ ] **Step 2: Add links from sibling pages**

In `wiki/_Sidebar.md`: add a link to `HowTo-Handle-Cascading-Selection` near the other HowTo entries (match the existing list format).

In `wiki/HowTo-Implement-Master-Detail.md`, in the シナリオ 3 section, add a sentence pointing to the new page as the recommended N-level approach:
> 3 段以上の連鎖選択は [連鎖選択 (カスケード) を扱う](HowTo-Handle-Cascading-Selection) の `ISelectionStore<T>` + `Cascade` を使うと、レベルごとの重複と「下位クリア忘れ」を構造的に消せます。

In `wiki/HowTo-Communicate-Between-Presenters.md` 関連ページ section, add:
> - [連鎖選択 (カスケード) を扱う](HowTo-Handle-Cascading-Selection) — 主従/N 段連鎖を `ISelectionStore<T>` + `Cascade` で

- [ ] **Step 3: Commit**

```bash
git add wiki/HowTo-Handle-Cascading-Selection.md wiki/_Sidebar.md wiki/HowTo-Implement-Master-Detail.md wiki/HowTo-Communicate-Between-Presenters.md
git commit -m "docs(wiki): add HowTo-Handle-Cascading-Selection and cross-links"
```

---

## Task 9: CHANGELOG + full regression + remote wiki sync

**Files:**
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add the CHANGELOG entry**

In `CHANGELOG.md`, under `## [Unreleased]`, add an `### Added (追加)` section (create it if absent, above any existing subsection):

```markdown
### Added (追加)

- **`ISelectionStore<T>` / `SelectionStore<T>` / `Cascade`** (`WinformsMVP.Common`) — 主従/N 段の連鎖選択を簡潔に書くためのプリミティブ。N 個の同型選択 Service を 1 つのジェネリックストアに、各レベルの「上位購読 → 自分をクリア → 再読込」を `Cascade.Bind` 1 行に畳む（多父は `Cascade.Combine`）。下位クリアは通知連鎖で自動化され、書き忘れによる stale 選択が構造的に起きない。`samples/WinformsMVP.Samples/CascadeDemo/` に 3 段の例。
```

- [ ] **Step 2: Full build + test (regression gate)**

Run: `dotnet build winforms-mvp.sln -c Debug`
Expected: Build succeeded, 0 errors.

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: PASS, total = previous count + 12 (4 SelectionStore + 8 Cascade), 0 failures.

(If the sample app is still running from Task 7's smoke test and locks the output DLL, stop it first: `taskkill //F //IM WinformsMVP.Samples.exe`.)

- [ ] **Step 3: Commit**

```bash
git add CHANGELOG.md
git commit -m "docs(changelog): record ISelectionStore<T> / Cascade under Unreleased"
```

- [ ] **Step 4: Push and sync the wiki to the remote**

```bash
git push origin master
```

Then mirror the wiki per `reference-wiki-publishing` (clone the canonical wiki to a temp dir — NOT the stale `C:\workspace\winforms-mvp-wiki` — copy `wiki/*.md` except `DEPLOY.md`/`README.md`, commit, push):

```bash
cd /tmp && rm -rf wiki-sync && git clone -q git@github.com:VoidAxon/winforms-mvp.wiki.git wiki-sync && cd wiki-sync
for f in /c/workspace/github/winforms-mvp/wiki/*.md; do b=$(basename "$f"); case "$b" in DEPLOY.md|README.md) continue;; esac; cp "$f" "./$b"; done
git add -A && git diff --cached --stat && git diff --cached --check
git commit -m "docs: add cascading-selection page; cross-links"
git push origin master
cd /c/workspace/github/winforms-mvp && rm -rf /tmp/wiki-sync
```

Expected diff: the new `HowTo-Handle-Cascading-Selection.md` plus the three modified pages; EOL check clean.

---

## Notes for the implementer

- **Do NOT bump the package version or tag a release** here. This lands under `[Unreleased]`; releasing is a separate, tag-driven step the maintainer triggers.
- Keep all C# comments and XML docs in **English**; wiki prose in **Japanese**; commit messages in English.
- `WinformsMVP` core multi-targets `net40;net48` — avoid `Array.Empty<T>()`, `IReadOnlyList<T>`, and C# 8+ syntax in `src/` and in code shown in the sample.
