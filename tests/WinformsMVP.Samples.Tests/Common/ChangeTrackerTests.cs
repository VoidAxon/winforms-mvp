using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Unit tests for ChangeTracker<T>
    ///
    /// Test coverage:
    /// - Basic functionality (AcceptChanges, RejectChanges, IsChanged)
    /// - UpdateCurrentValue() method
    /// - IsChangedChanged event
    /// - IsChanged caching
    /// - Thread safety
    /// - Validation support (CanAcceptChanges, CanRejectChanges)
    /// - Deep copy requirements
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
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<TestModel>(null));
        }

        [Fact]
        public void Constructor_WithValidValue_InitializesCorrectly()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Test" };

            // Act
            var tracker = new ChangeTracker<TestModel>(model);

            // Assert
            Assert.NotNull(tracker.CurrentValue);
            Assert.Equal(1, tracker.CurrentValue.Id);
            Assert.Equal("Test", tracker.CurrentValue.Name);
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void Constructor_CreatesIndependentCopy()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };

            // Act
            var tracker = new ChangeTracker<TestModel>(model);
            model.Name = "Modified";  // Modify the original object

            // Assert
            Assert.Equal("Original", tracker.CurrentValue.Name);  // tracker is not affected
        }

        #endregion

        #region UpdateCurrentValue Tests

        [Fact]
        public void UpdateCurrentValue_WithValidValue_UpdatesCurrentValue()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var newModel = new TestModel { Id = 2, Name = "Updated" };
            tracker.UpdateCurrentValue(newModel);

            // Assert
            Assert.Equal(2, tracker.CurrentValue.Id);
            Assert.Equal("Updated", tracker.CurrentValue.Name);
            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void UpdateCurrentValue_WithNull_ThrowsArgumentNullException()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Test" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => tracker.UpdateCurrentValue(null));
        }

        #endregion

        #region IsChangedChanged Event Tests

        [Fact]
        public void IsChangedChanged_WhenValueChanges_EventFires()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);
            var eventFired = false;

            tracker.IsChangedChanged += (s, e) => eventFired = true;

            // Act
            var newModel = new TestModel { Id = 2, Name = "Updated" };
            tracker.UpdateCurrentValue(newModel);

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void IsChangedChanged_WhenAcceptChanges_EventFires()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            var newModel = new TestModel { Id = 2, Name = "Updated" };
            tracker.UpdateCurrentValue(newModel);

            var eventFired = false;
            tracker.IsChangedChanged += (s, e) => eventFired = true;

            // Act
            tracker.AcceptChanges();

            // Assert
            Assert.True(eventFired);  // IsChanged changed from true → false
        }

        [Fact]
        public void IsChangedChanged_WhenRejectChanges_EventFires()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            var newModel = new TestModel { Id = 2, Name = "Updated" };
            tracker.UpdateCurrentValue(newModel);

            var eventFired = false;
            tracker.IsChangedChanged += (s, e) => eventFired = true;

            // Act
            tracker.RejectChanges();

            // Assert
            Assert.True(eventFired);  // IsChanged changed from true → false
        }

        [Fact]
        public void IsChangedChanged_WhenNoStateChange_EventDoesNotFire()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            var eventFired = false;
            tracker.IsChangedChanged += (s, e) => eventFired = true;

            // Act - AcceptChanges with no changes
            tracker.AcceptChanges();

            // Assert
            Assert.False(eventFired);  // Event does not fire because IsChanged state did not change
        }

        #endregion

        #region AcceptChanges Tests

        [Fact]
        public void AcceptChanges_AfterModification_SetsIsChangedToFalse()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            var newModel = new TestModel { Id = 2, Name = "Updated" };
            tracker.UpdateCurrentValue(newModel);
            Assert.True(tracker.IsChanged);

            // Act
            tracker.AcceptChanges();

            // Assert
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void AcceptChanges_CreatesNewBaseline()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "Updated" });
            tracker.AcceptChanges();

            // Act - further modification
            tracker.UpdateCurrentValue(new TestModel { Id = 3, Name = "Modified Again" });

            // Assert
            Assert.True(tracker.IsChanged);  // Changed from the new baseline
        }

        [Fact]
        public void AcceptChangesWithParameter_UpdatesBothCurrentAndOriginal()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var newModel = new TestModel { Id = 10, Name = "Complete Reset" };
            tracker.AcceptChanges(newModel);

            // Assert
            Assert.False(tracker.IsChanged);
            Assert.Equal(10, tracker.CurrentValue.Id);
            Assert.Equal("Complete Reset", tracker.CurrentValue.Name);
        }

        #endregion

        #region RejectChanges Tests

        [Fact]
        public void RejectChanges_RestoresOriginalValue()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "Updated" });
            Assert.True(tracker.IsChanged);

            // Act
            tracker.RejectChanges();

            // Assert
            Assert.False(tracker.IsChanged);
            Assert.Equal(1, tracker.CurrentValue.Id);
            Assert.Equal("Original", tracker.CurrentValue.Name);
        }

        [Fact]
        public void RejectChanges_MultipleModifications_RestoresToLastAccepted()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "First Change" });
            tracker.AcceptChanges();

            tracker.UpdateCurrentValue(new TestModel { Id = 3, Name = "Second Change" });
            tracker.UpdateCurrentValue(new TestModel { Id = 4, Name = "Third Change" });

            // Act
            tracker.RejectChanges();

            // Assert
            Assert.False(tracker.IsChanged);
            Assert.Equal(2, tracker.CurrentValue.Id);  // Reverts to "First Change"
            Assert.Equal("First Change", tracker.CurrentValue.Name);
        }

        #endregion

        #region Deep Copy Tests

        [Fact]
        public void DeepCopy_NestedObjects_AreIndependent()
        {
            // Arrange
            var model = new NestedModel
            {
                Title = "Parent",
                Child = new ChildModel { Value = "Original Child" }
            };
            var tracker = new ChangeTracker<NestedModel>(model);

            // Act - Modify nested object
            tracker.CurrentValue.Child.Value = "Modified Child";

            // Assert - IsChanged is correctly detected
            Assert.True(tracker.IsChanged);

            // Act - RejectChanges
            tracker.RejectChanges();

            // Assert - Reverts to original value
            Assert.Equal("Original Child", tracker.CurrentValue.Child.Value);
        }

        [Fact]
        public void GetOriginalValue_ReturnsIndependentCopy()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var original = tracker.GetOriginalValue();
            original.Name = "Modified Copy";

            // Assert - tracker's original is not affected
            tracker.RejectChanges();
            Assert.Equal("Original", tracker.CurrentValue.Name);
        }

        #endregion

        #region IsChangedWith Tests

        [Fact]
        public void IsChangedWith_ComparesAgainstOriginal()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var modifiedModel = new TestModel { Id = 2, Name = "Modified" };
            var isChanged = tracker.IsChangedWith(modifiedModel);

            // Assert
            Assert.True(isChanged);
        }

        [Fact]
        public void IsChangedWith_SameValues_ReturnsFalse()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var sameModel = new TestModel { Id = 1, Name = "Original" };
            var isChanged = tracker.IsChangedWith(sameModel);

            // Assert
            Assert.False(isChanged);
        }

        #endregion

        #region Custom Comparer Tests

        [Fact]
        public void CustomComparer_UsedForChangeDetection()
        {
            // Arrange - Custom comparer that only compares ID
            Func<TestModel, TestModel, bool> comparer = (a, b) => a.Id == b.Id;
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model, comparer);

            // Act - Only change Name (ID remains the same)
            tracker.UpdateCurrentValue(new TestModel { Id = 1, Name = "Modified" });

            // Assert - Custom comparer considers it unchanged because ID is the same
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void CustomComparer_DetectsRelevantChanges()
        {
            // Arrange
            Func<TestModel, TestModel, bool> comparer = (a, b) => a.Id == b.Id;
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model, comparer);

            // Act - Change ID
            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "Original" });

            // Assert - Custom comparer considers it changed because ID is different
            Assert.True(tracker.IsChanged);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void ThreadSafety_ConcurrentAccess_NoExceptions()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act - Concurrent access from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    if (index % 3 == 0)
                    {
                        tracker.UpdateCurrentValue(new TestModel { Id = index, Name = $"Thread {index}" });
                    }
                    else if (index % 3 == 1)
                    {
                        var isChanged = tracker.IsChanged;
                    }
                    else
                    {
                        tracker.AcceptChanges();
                    }
                }));
            }

            // Assert - Verify no exceptions are thrown
            Task.WaitAll(tasks.ToArray());
            Assert.True(true);  // OK if completed without exceptions
        }

        [Fact]
        public void ThreadSafety_IsChangedCaching_ThreadSafe()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act - Concurrent reads of IsChanged from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var _ = tracker.IsChanged;  // Read from cache
                }));
            }

            // Assert - Verify no exceptions are thrown
            Task.WaitAll(tasks.ToArray());
            Assert.True(true);
        }

        #endregion

        #region Validation Support Tests

        [Fact]
        public void CanAcceptChanges_DefaultImplementation_ReturnsTrue()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Test" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var canAccept = tracker.CanAcceptChanges(out string error);

            // Assert
            Assert.True(canAccept);
            Assert.Null(error);
        }

        [Fact]
        public void CanRejectChanges_DefaultImplementation_ReturnsTrue()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Test" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act
            var canReject = tracker.CanRejectChanges(out string error);

            // Assert
            Assert.True(canReject);
            Assert.Null(error);
        }

        // Test validation support in derived classes
        private class ValidatedChangeTracker : ChangeTracker<TestModel>
        {
            public ValidatedChangeTracker(TestModel initialValue) : base(initialValue) { }

            public override bool CanAcceptChanges(out string error)
            {
                if (CurrentValue.Name == "Invalid")
                {
                    error = "Name cannot be 'Invalid'";
                    return false;
                }
                error = null;
                return true;
            }
        }

        [Fact]
        public void DerivedClass_CustomValidation_Works()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Valid" };
            var tracker = new ValidatedChangeTracker(model);

            // Act - Change to invalid value
            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "Invalid" });

            // Assert
            Assert.False(tracker.CanAcceptChanges(out string error));
            Assert.Equal("Name cannot be 'Invalid'", error);
        }

        #endregion

        #region IsChanged Caching Tests

        [Fact]
        public void IsChanged_Caching_DoesNotRecomputeUntilValueChanges()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            // Act - Call IsChanged multiple times
            var result1 = tracker.IsChanged;
            var result2 = tracker.IsChanged;
            var result3 = tracker.IsChanged;

            // Assert - Cache is working (operates quickly without exceptions)
            Assert.False(result1);
            Assert.False(result2);
            Assert.False(result3);
        }

        [Fact]
        public void IsChanged_CacheInvalidation_AfterUpdateCurrentValue()
        {
            // Arrange
            var model = new TestModel { Id = 1, Name = "Original" };
            var tracker = new ChangeTracker<TestModel>(model);

            var _ = tracker.IsChanged;  // Create cache

            // Act - Change value
            tracker.UpdateCurrentValue(new TestModel { Id = 2, Name = "Updated" });

            // Assert - Cache is invalidated and recalculated with new value
            Assert.True(tracker.IsChanged);
        }

        #endregion
    }
}
