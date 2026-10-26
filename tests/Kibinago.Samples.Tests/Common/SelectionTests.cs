using System.Collections.Generic;
using Kibinago.Common;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    public class SelectionTests
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
            var selection = new Selection<Item>();
            int raised = 0;
            selection.CurrentChanged += (s, e) => raised++;

            var a = new Item(1);
            selection.Select(a);

            Assert.Same(a, selection.Current);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void Select_SameReference_DoesNotRaise()
        {
            var selection = new Selection<Item>();
            var a = new Item(1);
            selection.Select(a);

            int raised = 0;
            selection.CurrentChanged += (s, e) => raised++;
            selection.Select(a);

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Select_Null_ClearsCurrent()
        {
            var selection = new Selection<Item>();
            selection.Select(new Item(1));

            selection.Select(null);

            Assert.Null(selection.Current);
        }

        [Fact]
        public void Select_SameIdentityNewInstance_ReplacesCurrentAndRaisesOnlyCurrentChanged()
        {
            // A reload: the same record as a new object. Readers must see the new data; the
            // identity (which record is selected) did not change.
            var selection = new Selection<Item>(new ByIdComparer());
            selection.Select(new Item(7));

            int current = 0, identity = 0;
            selection.CurrentChanged += (s, e) => current++;
            selection.IdentityChanged += (s, e) => identity++;
            var reloaded = new Item(7);
            selection.Select(reloaded);

            Assert.Same(reloaded, selection.Current);
            Assert.Equal(1, current);
            Assert.Equal(0, identity);
        }

        [Fact]
        public void Select_DifferentIdentity_RaisesIdentityChangedThenCurrentChanged_AfterAssigning()
        {
            var selection = new Selection<Item>(new ByIdComparer());
            selection.Select(new Item(1));
            var next = new Item(2);

            var log = new List<string>();
            selection.IdentityChanged += (s, e) => log.Add("identity:" + selection.Current.Id);
            selection.CurrentChanged += (s, e) => log.Add("current:" + selection.Current.Id);
            selection.Select(next);

            Assert.Equal(new[] { "identity:2", "current:2" }, log);
        }

        [Fact]
        public void Select_Clear_RaisesBoth()
        {
            var selection = new Selection<Item>();
            selection.Select(new Item(1));

            int current = 0, identity = 0;
            selection.CurrentChanged += (s, e) => current++;
            selection.IdentityChanged += (s, e) => identity++;
            selection.Select(null);

            Assert.Equal(1, current);
            Assert.Equal(1, identity);
        }

        [Fact]
        public void Select_SameReference_RaisesNeither()
        {
            var selection = new Selection<Item>(new ByIdComparer());
            var a = new Item(1);
            selection.Select(a);

            int raised = 0;
            selection.CurrentChanged += (s, e) => raised++;
            selection.IdentityChanged += (s, e) => raised++;
            selection.Select(a);
            selection.Select(null);
            raised = 0;
            selection.Select(null);   // null -> null

            Assert.Equal(0, raised);
        }
    }
}
