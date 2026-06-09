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
