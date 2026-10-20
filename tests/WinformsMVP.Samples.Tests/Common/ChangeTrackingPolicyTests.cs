using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ChangeTrackingPolicyTests
    {
        private class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        // No ICloneable, no Equals override.
        private class Plain
        {
            public string Name { get; set; }
        }

        // ICloneable with a shallow Clone(): .NET does not promise a deep copy.
        private class ShallowRoot : ICloneable
        {
            public Item Child { get; set; }
            public object Clone() => MemberwiseClone();
        }

        [Fact]
        public void ArrayRoot_ElementEdit_IsDetected()
        {
            var items = new[] { new Item { Name = "a" } };
            var tracker = new ChangeTracker<Item[]>(items);

            items[0].Name = "b";

            Assert.True(tracker.IsChanged);   // Array.Clone() is shallow and must not be used
        }

        [Fact]
        public void CloneableRoot_IsNotTrusted()
        {
            var root = new ShallowRoot { Child = new Item { Name = "a" } };
            var tracker = new ChangeTracker<ShallowRoot>(root);

            root.Child.Name = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void PlainModel_NotChangedAfterConstruction_DetectsEdit()
        {
            var plain = new Plain { Name = "a" };
            var tracker = new ChangeTracker<Plain>(plain);
            Assert.False(tracker.IsChanged);

            plain.Name = "b";
            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void Policy_IsUsedForCloneAndCompare()
        {
            int clones = 0, compares = 0;
            var policy = new ChangeTrackingPolicy<Item>(
                i => { clones++; return new Item { Id = i.Id, Name = i.Name }; },
                (a, b) => { compares++; return a.Name == b.Name; });

            var tracker = new ChangeTracker<Item>(new Item { Name = "a" }, policy);
            Assert.Equal(1, clones);   // only the snapshot; the tracked object is not copied

            var changed = tracker.IsChanged;
            Assert.False(changed);
            Assert.Equal(1, compares);
        }

        [Fact]
        public void ComparerOnlyCtor_UsesBuiltInCloner()
        {
            var item = new Item { Id = 1, Name = "a" };
            var tracker = new ChangeTracker<Item>(item, (a, b) => a.Id == b.Id);

            item.Name = "b";

            Assert.False(tracker.IsChanged);       // comparer looks at Id only
            item.Id = 2;
            Assert.True(tracker.IsChanged);        // and the snapshot was a real copy
        }

        [Fact]
        public void EqualityComparerCtor_IsUsed()
        {
            var item = new Item { Id = 1, Name = "a" };
            var tracker = new ChangeTracker<Item>(item, new IdComparer());

            item.Name = "b";

            Assert.False(tracker.IsChanged);
        }

        private sealed class IdComparer : IEqualityComparer<Item>
        {
            public bool Equals(Item x, Item y) => x.Id == y.Id;
            public int GetHashCode(Item obj) => obj.Id;
        }

        [Fact]
        public void PolicyCtor_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<Item>(new Item(), (ChangeTrackingPolicy<Item>)null));
        }

        [Fact]
        public void ComparerCtor_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<Item>(new Item(), (Func<Item, Item, bool>)null));
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<Item>(new Item(), (IEqualityComparer<Item>)null));
        }

        [Fact]
        public void PolicyCtor_NullDelegatesThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new ChangeTrackingPolicy<Item>(null, (a, b) => true));
            Assert.Throws<ArgumentNullException>(() => new ChangeTrackingPolicy<Item>(i => i, null));
        }

        [Fact]
        public void BuiltInEngines_WrapObjectClonerAndComparer()
        {
            var src = new Item { Id = 1, Name = "a" };

            var copy = (Item)BuiltInObjectCloner.Instance.DeepClone(src);

            Assert.NotSame(src, copy);
            Assert.True(BuiltInObjectContentComparer.Instance.ContentEquals(src, copy, ChangeTrackingIgnores.None));
            copy.Name = "b";
            Assert.False(BuiltInObjectContentComparer.Instance.ContentEquals(src, copy, ChangeTrackingIgnores.None));
        }

        [Fact]
        public void PolicyFrom_Engines_UsesThem()
        {
            var policy = ChangeTrackingPolicy<Item>.From(new CountingCloner(), new AlwaysEqual());
            var item = new Item { Name = "a" };
            var tracker = new ChangeTracker<Item>(item, policy);

            item.Name = "b";

            Assert.False(tracker.IsChanged);
        }

        private sealed class CountingCloner : IObjectCloner
        {
            public int Count;
            public object DeepClone(object value) { Count++; return BuiltInObjectCloner.Instance.DeepClone(value); }
        }

        private sealed class AlwaysEqual : IObjectContentComparer
        {
            public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores) => true;
        }
    }
}
