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
            var tracker = new ChangeTracker<Item[]>(new[] { new Item { Name = "a" } });

            tracker.CurrentValue[0].Name = "b";

            Assert.True(tracker.IsChanged);   // Array.Clone() is shallow and must not be used
        }

        [Fact]
        public void CloneableRoot_IsNotTrusted()
        {
            var tracker = new ChangeTracker<ShallowRoot>(new ShallowRoot { Child = new Item { Name = "a" } });

            tracker.CurrentValue.Child.Name = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void PlainModel_NotChangedAfterConstruction_DetectsEdit_AndRejectRestores()
        {
            var tracker = new ChangeTracker<Plain>(new Plain { Name = "a" });
            Assert.False(tracker.IsChanged);

            tracker.CurrentValue.Name = "b";
            Assert.True(tracker.IsChanged);

            tracker.RejectChanges();
            Assert.False(tracker.IsChanged);
            Assert.Equal("a", tracker.CurrentValue.Name);
        }

        [Fact]
        public void Policy_IsUsedForCloneAndCompare()
        {
            int clones = 0, compares = 0;
            var policy = new ChangeTrackingPolicy<Item>(
                i => { clones++; return new Item { Id = i.Id, Name = i.Name }; },
                (a, b) => { compares++; return a.Name == b.Name; });

            var tracker = new ChangeTracker<Item>(new Item { Name = "a" }, policy);
            Assert.Equal(2, clones);   // current + original snapshots

            var changed = tracker.IsChanged;
            Assert.False(changed);
            Assert.Equal(1, compares);
        }

        [Fact]
        public void ComparerOnlyCtor_UsesBuiltInCloner()
        {
            var tracker = new ChangeTracker<Item>(new Item { Id = 1, Name = "a" }, (a, b) => a.Id == b.Id);

            tracker.CurrentValue.Name = "b";

            Assert.False(tracker.IsChanged);       // comparer looks at Id only
            tracker.RejectChanges();
            Assert.Equal("a", tracker.CurrentValue.Name);   // but the snapshot was a real copy
        }

        [Fact]
        public void EqualityComparerCtor_IsUsed()
        {
            var tracker = new ChangeTracker<Item>(new Item { Id = 1, Name = "a" }, new IdComparer());

            tracker.CurrentValue.Name = "b";

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
            Assert.True(BuiltInObjectContentComparer.Instance.ContentEquals(src, copy));
            copy.Name = "b";
            Assert.False(BuiltInObjectContentComparer.Instance.ContentEquals(src, copy));
        }

        [Fact]
        public void PolicyFrom_Engines_UsesThem()
        {
            var policy = ChangeTrackingPolicy<Item>.From(new CountingCloner(), new AlwaysEqual());
            var tracker = new ChangeTracker<Item>(new Item { Name = "a" }, policy);

            tracker.CurrentValue.Name = "b";

            Assert.False(tracker.IsChanged);
        }

        private sealed class CountingCloner : IObjectCloner
        {
            public int Count;
            public object DeepClone(object value) { Count++; return BuiltInObjectCloner.Instance.DeepClone(value); }
        }

        private sealed class AlwaysEqual : IObjectContentComparer
        {
            public bool ContentEquals(object left, object right) => true;
        }
    }
}
