using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class SelectableKeyComparerTests
    {
        private sealed class Entity : ISelectable
        {
            public int Id;
            public string Name;
            public Entity(int id, string name = null) { Id = id; Name = name; }
            public object Key { get { return Id; } }
        }

        private readonly SelectableKeyComparer<Entity> _cmp = SelectableKeyComparer<Entity>.Instance;

        [Fact]
        public void Equal_WhenKeysMatch_AcrossInstances()
        {
            Assert.True(_cmp.Equals(new Entity(1, "a"), new Entity(1, "b")));
            Assert.Equal(_cmp.GetHashCode(new Entity(1, "a")),
                         _cmp.GetHashCode(new Entity(1, "b")));
        }

        [Fact]
        public void NotEqual_WhenKeysDiffer()
        {
            Assert.False(_cmp.Equals(new Entity(1), new Entity(2)));
        }

        [Fact]
        public void Equal_WhenSameReference()
        {
            var e = new Entity(5);
            Assert.True(_cmp.Equals(e, e));
        }

        [Fact]
        public void NotEqual_WhenEitherNull()
        {
            Assert.False(_cmp.Equals(new Entity(1), null));
            Assert.False(_cmp.Equals(null, new Entity(1)));
        }

        [Fact]
        public void StoreAutoSelectsKeyComparer_ForISelectableEntity()
        {
            // No comparer passed: store should compare ISelectable entities by Key.
            var store = new SelectionStore<Entity>();
            store.Select(new Entity(7, "first"));

            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;
            store.Select(new Entity(7, "reloaded"));   // new instance, same Key

            Assert.Equal(0, raised);
        }

        [Fact]
        public void StoreAutoSelectsKeyComparer_ForSelectableItemValueType()
        {
            var store = new SelectionStore<SelectableItem<int>>();
            store.Select(SelectableItem.Of(2025));

            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;
            store.Select(SelectableItem.Of(2025, "different label"));   // same Value

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Store_NonSelectableType_FallsBackToDefaultComparer()
        {
            // string has good value equality and does NOT implement ISelectable.
            var store = new SelectionStore<string>();
            store.Select("x");

            int raised = 0;
            store.CurrentChanged += (s, e) => raised++;
            store.Select(string.Concat("x"));   // equal by value -> short-circuited

            Assert.Equal(0, raised);
        }
    }
}
