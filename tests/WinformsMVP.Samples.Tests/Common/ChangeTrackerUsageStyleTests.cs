using System;
using System.Collections.Generic;
using System.ComponentModel;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// The two supported ways to use a ChangeTracker. Style 1: the caller keeps editing one object
    /// (IsChanged / AcceptChanges()). Style 2: the caller builds a new object each time
    /// (IsChangedWith(x) / AcceptChanges(x)). The tracker only detects changes; it never holds the
    /// caller's data for it.
    /// </summary>
    public class ChangeTrackerUsageStyleTests
    {
        public class Order
        {
            public string Name { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }

        // ---- style 1: one object, edited in place ------------------------------------------

        [Fact]
        public void TracksTheObjectItself_InPlaceEditIsChanged()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);

            order.Name = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void EditBackToOriginal_IsNotChanged()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);

            order.Name = "b";
            order.Name = "a";

            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void AcceptChanges_ThenIsNotChanged_AndLaterEditsAreSeen()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);

            order.Name = "b";
            tracker.AcceptChanges();
            Assert.False(tracker.IsChanged);

            order.Name = "c";
            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void AcceptChangesWithSaved_EditDuringSave_IsStillChanged()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);
            var saved = new Order { Name = "b" };   // what the async save sent

            order.Name = "c";                        // the user kept editing meanwhile
            tracker.AcceptChanges(saved);
            Assert.True(tracker.IsChanged);

            order.Name = "b";
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void AcceptChangesWithSaved_EditBackToOriginalDuringSave_IsStillChanged()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);
            var saved = new Order { Name = "b" };

            order.Name = "a";                        // back to the original while "b" was being saved
            tracker.AcceptChanges(saved);

            Assert.True(tracker.IsChanged);          // storage now holds "b"
        }

        [Fact]
        public void ListRoot_AddRemoveEdit_AreDetected()
        {
            List<Order> NewList() => new List<Order> { new Order { Name = "a" }, new Order { Name = "b" } };

            var added = NewList();
            var t1 = new ChangeTracker<List<Order>>(added);
            added.Add(new Order { Name = "c" });
            Assert.True(t1.IsChanged);

            var removed = NewList();
            var t2 = new ChangeTracker<List<Order>>(removed);
            removed.RemoveAt(0);
            Assert.True(t2.IsChanged);

            var edited = NewList();
            var t3 = new ChangeTracker<List<Order>>(edited);
            edited[0].Name = "x";
            Assert.True(t3.IsChanged);
        }

        // ---- style 2: a new object each time -------------------------------------------------

        [Fact]
        public void IsChangedWith_NewInstance_ComparesContent()
        {
            var tracker = new ChangeTracker<Order>(new Order { Name = "a" });

            Assert.False(tracker.IsChangedWith(new Order { Name = "a" }));
            Assert.True(tracker.IsChangedWith(new Order { Name = "b" }));
        }

        [Fact]
        public void AcceptChangesWithValue_ThenSameContent_IsNotChanged()
        {
            var tracker = new ChangeTracker<Order>(new Order { Name = "a" });

            tracker.AcceptChanges(new Order { Name = "b" });

            Assert.False(tracker.IsChangedWith(new Order { Name = "b" }));
            Assert.True(tracker.IsChangedWith(new Order { Name = "a" }));
        }

        [Fact]
        public void AcceptChangesWithValue_SnapshotIsIndependentOfValue()
        {
            var tracker = new ChangeTracker<Order>(new Order { Name = "a" });
            var value = new Order { Name = "b" };

            tracker.AcceptChanges(value);
            value.Name = "z";

            Assert.False(tracker.IsChangedWith(new Order { Name = "b" }));
        }

        [Fact]
        public void AcceptChangesWithValue_DoesNotChangeTheTrackedObject()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);

            tracker.AcceptChanges(new Order { Name = "b" });

            Assert.Equal("a", order.Name);
            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void IsChangedWith_HasNoSideEffects()
        {
            var tracker = new ChangeTracker<Order>(new Order { Name = "a" });

            tracker.IsChangedWith(new Order { Name = "z" });

            Assert.False(tracker.IsChanged);
            Assert.False(tracker.IsChangedWith(new Order { Name = "a" }));
        }

        // ---- common -------------------------------------------------------------------------

        [Fact]
        public void Snapshot_IsIndependentOfTheTrackedObject()
        {
            var order = new Order { Name = "a" };
            var tracker = new ChangeTracker<Order>(order);

            order.Tags.Add("t");

            Assert.True(tracker.IsChanged);   // the snapshot was a deep copy
        }

        [Fact]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ChangeTracker<Order>((Order)null));
            var tracker = new ChangeTracker<Order>(new Order());
            Assert.Throws<ArgumentNullException>(() => tracker.AcceptChanges(null));
            Assert.Throws<ArgumentNullException>(() => tracker.IsChangedWith(null));
        }

        [Fact]
        public void ImplementsIChangeTrackingOnly()
        {
            var tracker = new ChangeTracker<Order>(new Order());

            Assert.IsAssignableFrom<IChangeTracking>(tracker);
            Assert.False(typeof(IRevertibleChangeTracking).IsAssignableFrom(tracker.GetType()));
        }
    }
}
