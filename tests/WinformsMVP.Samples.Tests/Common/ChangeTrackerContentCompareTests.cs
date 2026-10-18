using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// ChangeTracker compares by content by default: an entity whose Equals means "same Id" must
    /// not hide an edit (which would skip the dirty-check save prompt).
    /// </summary>
    public class ChangeTrackerContentCompareTests
    {
        private abstract class Entity : IEquatable<Entity>
        {
            public int Id { get; set; }
            public bool Equals(Entity other) => other != null && other.GetType() == GetType() && other.Id == Id;
            public override bool Equals(object obj) => Equals(obj as Entity);
            public override int GetHashCode() => Id;
        }

        private sealed class Customer : Entity
        {
            public string Name { get; set; }
        }

        private sealed class Order : Entity
        {
            public string Note { get; set; }
            public Customer Customer { get; set; }
            public HashSet<Customer> Watchers { get; set; } = new HashSet<Customer>();
        }

        private static Order NewOrder() => new Order
        {
            Id = 1,
            Note = "n",
            Customer = new Customer { Id = 7, Name = "Alice" },
        };

        [Fact]
        public void EditingAnEntityWithIdEquality_IsDetected()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());

            tracker.CurrentValue.Note = "edited";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void EditingANestedEntityWithIdEquality_IsDetected()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());

            tracker.CurrentValue.Customer.Name = "Bob";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void EditingAnEntityInsideASet_IsDetected()
        {
            var order = NewOrder();
            order.Watchers.Add(new Customer { Id = 9, Name = "Carol" });
            var tracker = new ChangeTracker<Order>(order);
            Assert.False(tracker.IsChanged);

            foreach (var watcher in tracker.CurrentValue.Watchers) watcher.Name = "Dave";

            Assert.True(tracker.IsChanged);
        }

        private sealed class IdComparer : IEqualityComparer<Customer>
        {
            public bool Equals(Customer x, Customer y) => x?.Id == y?.Id;
            public int GetHashCode(Customer c) => c.Id;
        }

        [Fact]
        public void EditingAnEntityInsideASetWithAnIdComparer_IsDetected()
        {
            var order = NewOrder();
            order.Watchers = new HashSet<Customer>(new IdComparer()) { new Customer { Id = 9, Name = "Carol" } };
            var tracker = new ChangeTracker<Order>(order);
            Assert.False(tracker.IsChanged);

            foreach (var watcher in tracker.CurrentValue.Watchers) watcher.Name = "Dave";

            Assert.True(tracker.IsChanged);
            Assert.True(ObjectComparer.DeepEquals(order.Watchers, tracker.CurrentValue.Watchers)); // membership only
        }

        [Fact]
        public void UntouchedEntity_IsNotChanged()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void ExplicitComparer_OptsIntoIdentityEquality()
        {
            var tracker = new ChangeTracker<Order>(NewOrder(), (a, b) => a.Equals(b));

            tracker.CurrentValue.Note = "edited";

            Assert.False(tracker.IsChanged);
        }

        private struct Money : IEquatable<Money>
        {
            public decimal Amount;
            public string Currency;
            public bool Equals(Money other) => Amount == other.Amount
                && string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);
            public override bool Equals(object obj) => obj is Money m && Equals(m);
            public override int GetHashCode() => Amount.GetHashCode();
        }

        private sealed class Invoice
        {
            public Money Total;
            public Uri Link;
        }

        [Fact]
        public void ValueTypeAndFrameworkEquality_AreStillTrusted()
        {
            var a = new Invoice { Total = new Money { Amount = 1, Currency = "usd" }, Link = new Uri("http://x/") };
            var b = new Invoice { Total = new Money { Amount = 1, Currency = "USD" }, Link = new Uri("http://x/") };

            Assert.True(ObjectComparer.ContentEquals(a, b));
        }

        [Fact]
        public void DeepEquals_StillRespectsCustomEquals()
        {
            var a = NewOrder();
            var b = NewOrder();
            b.Note = "different";

            Assert.True(ObjectComparer.DeepEquals(a, b));      // Id equality
            Assert.False(ObjectComparer.ContentEquals(a, b));  // content differs
        }
    }
}
