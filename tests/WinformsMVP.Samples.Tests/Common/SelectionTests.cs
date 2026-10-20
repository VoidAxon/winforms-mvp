using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
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
        public void Select_WithComparer_TreatsEqualValuesAsUnchanged()
        {
            var selection = new Selection<Item>(new ByIdComparer());
            selection.Select(new Item(7));

            int raised = 0;
            selection.CurrentChanged += (s, e) => raised++;
            selection.Select(new Item(7));   // different instance, equal by Id

            Assert.Equal(0, raised);
        }
    }
}
