using Kibinago.Common;
using Xunit;

namespace Kibinago.Samples.Tests.Common
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
        public void SelectionAutoSelectsKeyComparer_ForISelectableEntity()
        {
            // No comparer passed: the selection should compare ISelectable entities by Key.
            var selection = new Selection<Entity>();
            selection.Select(new Entity(7, "first"));

            int identity = 0;
            selection.IdentityChanged += (s, e) => identity++;
            var reloaded = new Entity(7, "reloaded");
            selection.Select(reloaded);   // new instance, same Key: same identity, new data

            Assert.Equal(0, identity);
            Assert.Same(reloaded, selection.Current);
        }

        [Fact]
        public void SelectionAutoSelectsKeyComparer_ForSelectableItemValueType()
        {
            var selection = new Selection<SelectableItem<int>>();
            selection.Select(SelectableItem.Of(2025));

            int identity = 0;
            selection.IdentityChanged += (s, e) => identity++;
            selection.Select(SelectableItem.Of(2025, "different label"));   // same Value

            Assert.Equal(0, identity);
            Assert.Equal("different label", selection.Current.Text);
        }

        [Fact]
        public void Selection_NonSelectableType_FallsBackToDefaultComparer()
        {
            // string has good value equality and does NOT implement ISelectable.
            var selection = new Selection<string>();
            selection.Select("x");

            int identity = 0;
            selection.IdentityChanged += (s, e) => identity++;
            selection.Select(new string('x', 1));   // equal by value -> same identity

            Assert.Equal(0, identity);
        }
    }
}
