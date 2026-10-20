using System;
using System.Collections.Generic;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Unit tests for ChangeTracker<T>
    ///
    /// Test coverage (the two usage styles have their own file, ChangeTrackerUsageStyleTests):
    /// - Construction and snapshot independence
    /// - AcceptChanges / AcceptChanges(value)
    /// - Deep copy of nested objects
    /// - IsChangedWith
    /// - Custom comparers
    /// - IsChanged computed on every read
    /// </summary>
    public class ChangeTrackerTests
    {
        #region Test Models

        private class TestModel : ICloneable, IEquatable<TestModel>
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public object Clone()
            {
                return new TestModel
                {
                    Id = this.Id,
                    Name = this.Name
                };
            }

            public bool Equals(TestModel other)
            {
                if (other == null) return false;
                return Id == other.Id && Name == other.Name;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as TestModel);
            }

            public override int GetHashCode()
            {
                return (Id, Name).GetHashCode();
            }
        }

        private class NestedModel : ICloneable, IEquatable<NestedModel>
        {
            public string Title { get; set; }
            public ChildModel Child { get; set; }

            public object Clone()
            {
                return new NestedModel
                {
                    Title = this.Title,
                    Child = this.Child?.Clone() as ChildModel  // Deep copy
                };
            }

            public bool Equals(NestedModel other)
            {
                if (other == null) return false;
                return Title == other.Title &&
                       (Child == null && other.Child == null ||
                        Child != null && Child.Equals(other.Child));
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as NestedModel);
            }

            public override int GetHashCode()
            {
                return (Title, Child).GetHashCode();
            }
        }

        private class ChildModel : ICloneable, IEquatable<ChildModel>
        {
            public string Value { get; set; }

            public object Clone()
            {
                return new ChildModel { Value = this.Value };
            }

            public bool Equals(ChildModel other)
            {
                if (other == null) return false;
                return Value == other.Value;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as ChildModel);
            }

            public override int GetHashCode()
            {
                return Value?.GetHashCode() ?? 0;
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullInitialValue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<TestModel>((TestModel)null));
        }

        [Fact]
        public void Constructor_WithValidValue_StartsUnchanged()
        {
            var tracker = new ChangeTracker<TestModel>(new TestModel { Id = 1, Name = "Test" });

            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void Constructor_TracksTheObject_SnapshotIsIndependent()
        {
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            model.Name = "Modified";   // the caller edits its own object

            Assert.True(tracker.IsChanged);                                              // seen by the tracker
            Assert.False(tracker.IsChangedWith(new TestModel { Id = 1, Name = "Original" }));   // snapshot untouched
        }

        #endregion

        #region AcceptChanges Tests

        [Fact]
        public void AcceptChanges_MakesCurrentStateTheNewSnapshot()
        {
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);
            model.Name = "Modified";

            tracker.AcceptChanges();

            Assert.False(tracker.IsChanged);
            Assert.True(tracker.IsChangedWith(new TestModel { Id = 1, Name = "Original" }));
        }

        [Fact]
        public void AcceptChanges_SnapshotIsADeepCopy()
        {
            var model = new NestedModel { Title = "t", Child = new ChildModel { Value = "a" } };
            var tracker = new ChangeTracker<NestedModel>(model);

            tracker.AcceptChanges();
            model.Child.Value = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void AcceptChangesWithValue_NullThrows()
        {
            var tracker = new ChangeTracker<TestModel>(new TestModel());

            Assert.Throws<ArgumentNullException>(() => tracker.AcceptChanges(null));
        }

        #endregion

        #region Deep Copy Tests

        [Fact]
        public void DeepCopy_NestedObjects_AreIndependent()
        {
            var model = new NestedModel { Title = "Parent", Child = new ChildModel { Value = "Original" } };
            var tracker = new ChangeTracker<NestedModel>(model);

            model.Child.Value = "Modified";

            Assert.True(tracker.IsChanged);   // the snapshot's child was copied, not shared
        }

        #endregion

        #region IsChangedWith Tests

        [Fact]
        public void IsChangedWith_ComparesAgainstSnapshot()
        {
            var tracker = new ChangeTracker<TestModel>(new TestModel { Id = 1, Name = "Original" });

            Assert.True(tracker.IsChangedWith(new TestModel { Id = 1, Name = "Different" }));
        }

        [Fact]
        public void IsChangedWith_SameValues_ReturnsFalse()
        {
            var tracker = new ChangeTracker<TestModel>(new TestModel { Id = 1, Name = "Test" });

            Assert.False(tracker.IsChangedWith(new TestModel { Id = 1, Name = "Test" }));
        }

        #endregion

        #region Custom Comparer Tests

        [Fact]
        public void CustomComparer_UsedForChangeDetection()
        {
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model, (a, b) => a.Id == b.Id);

            model.Name = "Modified";

            Assert.False(tracker.IsChanged);   // the comparer ignores Name
        }

        [Fact]
        public void CustomComparer_DetectsRelevantChanges()
        {
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model, (a, b) => a.Id == b.Id);

            model.Id = 2;

            Assert.True(tracker.IsChanged);
        }

        #endregion

        #region IsChanged Tests

        [Fact]
        public void IsChanged_IsComputedOnEveryRead()
        {
            var model = new TestModel { Id = 1, Name = "a" };
            var tracker = new ChangeTracker<TestModel>(model);

            Assert.False(tracker.IsChanged);
            model.Name = "b";
            Assert.True(tracker.IsChanged);
            model.Name = "a";
            Assert.False(tracker.IsChanged);
        }

        #endregion
    }
}
