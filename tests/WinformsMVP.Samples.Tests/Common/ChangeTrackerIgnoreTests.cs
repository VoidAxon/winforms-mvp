using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ChangeTrackerIgnoreTests
    {
        public class Audit
        {
            public string By { get; set; }
        }

        public class OrderLine
        {
            public string Product { get; set; }
            public decimal CachedTotal { get; set; }
        }

        public class Customer
        {
            public string Name { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        public class Order
        {
            public string Number { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string LastViewedBy { get; set; }
            public Customer Customer { get; set; } = new Customer();
            public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
            public Audit Audit { get; set; } = new Audit();
            public int Total => Lines.Count;   // computed: no field
        }

        // Hand-written backing fields, as with BindableBase.SetProperty(ref _name, value).
        public class Bindable
        {
            private string _name;
            private int m_count;
            public string Name { get => _name; set => _name = value; }
            public int Count { get => m_count; set => m_count = value; }
        }

        private static Order NewOrder() => new Order
        {
            Number = "A-1",
            UpdatedAt = new DateTime(2026, 1, 1),
            LastViewedBy = "ann",
            Customer = new Customer { Name = "Contoso", UpdatedAt = new DateTime(2026, 1, 1) },
            Lines = new List<OrderLine> { new OrderLine { Product = "Pen", CachedTotal = 1 } },
            Audit = new Audit { By = "ann" },
        };

        [Fact]
        public void Ignore_RootProperty_EditIsNotAChange_OtherEditsStillAre()
        {
            var tracker = new ChangeTracker<Order>(NewOrder()).Ignore(x => x.UpdatedAt);

            var edited = NewOrder();
            edited.UpdatedAt = new DateTime(2026, 2, 2);
            Assert.False(tracker.IsChangedWith(edited));

            edited.Number = "A-2";
            Assert.True(tracker.IsChangedWith(edited));
        }

        [Fact]
        public void Ignore_RootProperty_DoesNotIgnoreSameNameOnOtherTypes()
        {
            var tracker = new ChangeTracker<Order>(NewOrder()).Ignore(x => x.UpdatedAt);

            var edited = NewOrder();
            edited.Customer.UpdatedAt = new DateTime(2026, 2, 2);

            Assert.True(tracker.IsChangedWith(edited));   // Order.UpdatedAt only, not Customer.UpdatedAt
        }

        [Fact]
        public void IgnoreOwner_AppliesWhereverTheTypeAppears_IncludingCollectionElements()
        {
            var tracker = new ChangeTracker<Order>(NewOrder()).Ignore<OrderLine>(l => l.CachedTotal);

            var edited = NewOrder();
            edited.Lines[0].CachedTotal = 99;
            Assert.False(tracker.IsChangedWith(edited));

            edited.Lines[0].Product = "Ink";
            Assert.True(tracker.IsChangedWith(edited));
        }

        [Fact]
        public void IgnoreMember_ByName_AppliesToEveryType()
        {
            var tracker = new ChangeTracker<Order>(NewOrder()).IgnoreMember("UpdatedAt");

            var edited = NewOrder();
            edited.UpdatedAt = new DateTime(2026, 2, 2);
            edited.Customer.UpdatedAt = new DateTime(2026, 2, 2);

            Assert.False(tracker.IsChangedWith(edited));
        }

        [Fact]
        public void IgnoreType_SkipsMembersOfThatType()
        {
            var tracker = new ChangeTracker<Order>(NewOrder()).IgnoreType<Audit>();

            var edited = NewOrder();
            edited.Audit.By = "bob";
            Assert.False(tracker.IsChangedWith(edited));

            edited.Audit = null;
            Assert.False(tracker.IsChangedWith(edited));
        }

        [Fact]
        public void Ignore_HandWrittenBackingField_IsFoundByConvention()
        {
            var tracker = new ChangeTracker<Bindable>(new Bindable { Name = "a", Count = 1 })
                .Ignore(x => x.Name, x => x.Count);

            Assert.False(tracker.IsChangedWith(new Bindable { Name = "b", Count = 2 }));
        }

        [Fact]
        public void IgnoreMember_MatchesHandWrittenBackingFields()
        {
            var tracker = new ChangeTracker<Bindable>(new Bindable { Name = "a" }).IgnoreMember("Name");

            Assert.False(tracker.IsChangedWith(new Bindable { Name = "b" }));
        }

        [Fact]
        public void Ignore_NestedPath_IsRejected()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());

            var ex = Assert.Throws<ArgumentException>(() => tracker.Ignore(x => x.Customer.Name));
            Assert.Contains("Ignore<Customer>", ex.Message);   // tells the caller what to write instead
        }

        [Fact]
        public void Ignore_MethodCallOrIndexer_IsRejected()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());

            Assert.Throws<ArgumentException>(() => tracker.Ignore(x => x.Number.ToUpper()));
            Assert.Throws<ArgumentException>(() => tracker.Ignore(x => x.Lines[0]));
        }

        [Fact]
        public void Ignore_ComputedProperty_IsRejected()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());

            Assert.Throws<ArgumentException>(() => tracker.Ignore(x => x.Total));
        }

        [Fact]
        public void Ignore_WithCustomComparerFunction_Throws()
        {
            var tracker = new ChangeTracker<Order>(NewOrder(), (a, b) => a.Number == b.Number);

            var ex = Assert.Throws<InvalidOperationException>(() => tracker.Ignore(x => x.UpdatedAt));
            Assert.Contains("comparer", ex.Message);
        }

        [Fact]
        public void Ignore_RaisesIsChangedChanged_WhenTheDirtyStateFlips()
        {
            var tracker = new ChangeTracker<Order>(NewOrder());
            var edited = NewOrder();
            edited.UpdatedAt = new DateTime(2026, 2, 2);
            tracker.UpdateCurrentValue(edited);
            Assert.True(tracker.IsChanged);
            int raised = 0;
            tracker.IsChangedChanged += (s, e) => raised++;

            tracker.Ignore(x => x.UpdatedAt);

            Assert.False(tracker.IsChanged);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void Ignores_AreExposedForExternalComparers()
        {
            var tracker = new ChangeTracker<Order>(NewOrder())
                .Ignore(x => x.UpdatedAt)
                .Ignore<OrderLine>(l => l.CachedTotal)
                .IgnoreMember("LastViewedBy")
                .IgnoreType<Audit>();

            var ignores = tracker.Ignores;
            Assert.Equal(new[] { "Order.UpdatedAt", "OrderLine.CachedTotal" },
                ignores.Members.Select(m => m.Owner.Name + "." + m.Name));
            Assert.Equal(new[] { "LastViewedBy" }, ignores.MemberNames);
            Assert.Equal(new[] { typeof(Audit) }, ignores.Types);
        }

        [Fact]
        public void PolicyFromEngines_PassesIgnoresToTheComparer()
        {
            var comparer = new RecordingComparer();
            var tracker = new ChangeTracker<Order>(NewOrder(),
                    ChangeTrackingPolicy<Order>.From(BuiltInObjectCloner.Instance, comparer))
                .IgnoreMember("UpdatedAt");

            var unused = tracker.IsChanged;

            Assert.Equal(new[] { "UpdatedAt" }, comparer.LastIgnores.MemberNames);
        }

        [Fact]
        public void NoIgnores_ComparerReceivesNone()
        {
            var comparer = new RecordingComparer();
            var tracker = new ChangeTracker<Order>(NewOrder(),
                ChangeTrackingPolicy<Order>.From(BuiltInObjectCloner.Instance, comparer));

            var unused = tracker.IsChanged;

            Assert.Same(ChangeTrackingIgnores.None, comparer.LastIgnores);
        }

        private sealed class RecordingComparer : IObjectContentComparer
        {
            public ChangeTrackingIgnores LastIgnores;
            public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores)
            {
                LastIgnores = ignores;
                return true;
            }
        }
    }
}
