using System;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    // Tests in this collection both depend on the default hooks AND replace them,
    // so they must run serially (not in parallel with each other).
    [Collection("ChangeTrackerDefaults")]
    public class ChangeTrackerHookTests
    {
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void Defaults_AreNotNull_AndDelegateToEngine()
        {
            Assert.NotNull(ChangeTrackerDefaults.Cloner);
            Assert.NotNull(ChangeTrackerDefaults.Comparer);

            var src = new PlainPoco { Id = 1, Name = "x" };
            var copy = (PlainPoco)ChangeTrackerDefaults.Cloner(src);
            Assert.NotSame(src, copy);
            Assert.Equal("x", copy.Name);

            Assert.True(ChangeTrackerDefaults.Comparer(
                new PlainPoco { Id = 1, Name = "x" },
                new PlainPoco { Id = 1, Name = "x" }));

            Assert.False(ChangeTrackerDefaults.Comparer(
                new PlainPoco { Id = 1, Name = "x" },
                new PlainPoco { Id = 1, Name = "y" }));
        }

        [Fact]
        public void Cloner_CanBeReplaced()
        {
            var original = ChangeTrackerDefaults.Cloner;
            try
            {
                var called = false;
                ChangeTrackerDefaults.Cloner = o => { called = true; return original(o); };

                var copy = (PlainPoco)ChangeTrackerDefaults.Cloner(new PlainPoco { Name = "y" });

                Assert.True(called);
                Assert.Equal("y", copy.Name);
            }
            finally
            {
                ChangeTrackerDefaults.Cloner = original;   // restore global state
            }
        }

        [Fact]
        public void Comparer_CanBeReplaced()
        {
            var original = ChangeTrackerDefaults.Comparer;
            try
            {
                var called = false;
                ChangeTrackerDefaults.Comparer = (a, b) => { called = true; return original(a, b); };

                var equal = ChangeTrackerDefaults.Comparer(
                    new PlainPoco { Id = 1, Name = "x" },
                    new PlainPoco { Id = 1, Name = "x" });
                var notEqual = ChangeTrackerDefaults.Comparer(
                    new PlainPoco { Id = 1, Name = "x" },
                    new PlainPoco { Id = 1, Name = "y" });

                Assert.True(called);
                Assert.True(equal);
                Assert.False(notEqual);
            }
            finally
            {
                ChangeTrackerDefaults.Comparer = original;
            }
        }

        [Fact]
        public void Setters_RejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => ChangeTrackerDefaults.Cloner = null);
            Assert.Throws<ArgumentNullException>(() => ChangeTrackerDefaults.Comparer = null);
        }

        // No ICloneable, no Equals override: old implementation would always report IsChanged=true,
        // new implementation should correctly report false after construction.
        private class NoEqualsModel
        {
            public string Name { get; set; }
            private int _secret;
            public int Secret { get => _secret; set => _secret = value; }
        }

        // Implements ICloneable (counter verifies Clone() is used, not the hook).
        private class CloneableModel : ICloneable
        {
            public static int CloneCount;
            public string Name { get; set; }
            public object Clone()
            {
                CloneCount++;
                return new CloneableModel { Name = this.Name };
            }
        }

        [Fact]
        public void Tracker_NonCloneableModel_NotChangedAfterConstruction()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 7 };

            var tracker = new ChangeTracker<NoEqualsModel>(model);

            Assert.False(tracker.IsChanged);   // Bug fix: old implementation returned true here
        }

        [Fact]
        public void Tracker_NonCloneableModel_DetectsChange_AndRejectRestores()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 7 };
            var tracker = new ChangeTracker<NoEqualsModel>(model);

            tracker.UpdateCurrentValue(new NoEqualsModel { Name = "b", Secret = 7 });
            Assert.True(tracker.IsChanged);

            tracker.RejectChanges();
            Assert.False(tracker.IsChanged);
            Assert.Equal("a", tracker.CurrentValue.Name);
            Assert.Equal(7, tracker.CurrentValue.Secret);   // private backing field is also fully restored
        }

        [Fact]
        public void Tracker_CloneableModel_UsesCloneMethod_NotHook()
        {
            CloneableModel.CloneCount = 0;
            var originalCloner = ChangeTrackerDefaults.Cloner;
            try
            {
                ChangeTrackerDefaults.Cloner = o => throw new InvalidOperationException("hook should not be called");

                var tracker = new ChangeTracker<CloneableModel>(new CloneableModel { Name = "a" });

                Assert.True(CloneableModel.CloneCount >= 2);   // construction clones twice, via ICloneable
                Assert.False(tracker.IsChanged);
            }
            finally
            {
                ChangeTrackerDefaults.Cloner = originalCloner;
            }
        }

        [Fact]
        public void Tracker_ExplicitComparer_OverridesEverything()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 1 };
            // Only compares Name, ignores Secret
            var tracker = new ChangeTracker<NoEqualsModel>(model, (x, y) => x.Name == y.Name);

            tracker.UpdateCurrentValue(new NoEqualsModel { Name = "a", Secret = 999 });

            Assert.False(tracker.IsChanged);   // Secret changed but ignored by custom comparer
        }
    }

    // Collection definition (empty class carrying CollectionDefinition attribute only).
    [CollectionDefinition("ChangeTrackerDefaults", DisableParallelization = true)]
    public class ChangeTrackerDefaultsCollection { }
}
