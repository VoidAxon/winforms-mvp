using System;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Services;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    [Collection("ServiceLocator")]
    public class TrackChangesTests : IDisposable
    {
        public TrackChangesTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        public class Order
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public interface IOrderView : IWindowView { }

        private sealed class OrderView : IOrderView
        {
            public IViewActionBinder ActionBinder => null;
        }

        // Creates its tracker in OnInitialize, the way a real edit presenter would. TrackChanges is
        // protected, so the test reaches it through a nested helper rather than widening visibility.
        private sealed class OrderPresenter : WindowPresenterBase<IOrderView>
        {
            private readonly Func<Tracking, ChangeTracker<Order>> _create;
            public ChangeTracker<Order> Tracker;

            public OrderPresenter(Func<Tracking, ChangeTracker<Order>> create) { _create = create; }

            protected override void OnViewAttached() { }
            protected override void OnInitialize() => Tracker = _create(new Tracking(this));

            public sealed class Tracking
            {
                private readonly OrderPresenter _p;
                internal Tracking(OrderPresenter p) { _p = p; }
                public ChangeTracker<Order> Track(Order o) => _p.TrackChanges(o);
                public ChangeTracker<Order> Track(Order o, ChangeTrackingPolicy<Order> policy) => _p.TrackChanges(o, policy);
                public ChangeTracker<Order> Track(Order o, Func<Order, Order, bool> comparer) => _p.TrackChanges(o, comparer);
            }
        }

        private static ChangeTracker<Order> Start(Func<OrderPresenter.Tracking, ChangeTracker<Order>> create)
        {
            var presenter = new OrderPresenter(create);
            using (PresenterTestHost.Start(presenter, new OrderView()))
            {
                return presenter.Tracker;
            }
        }

        private sealed class AlwaysEqual : IObjectContentComparer
        {
            public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores) => true;
        }

        private sealed class CountingCloner : IObjectCloner
        {
            public int Count;
            public object DeepClone(object value) { Count++; return BuiltInObjectCloner.Instance.DeepClone(value); }
        }

        [Fact]
        public void Default_UsesBuiltInEngines()
        {
            var order = new Order { Name = "a" };
            var tracker = Start(p => p.Track(order));

            order.Name = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void RegisteredComparerEngine_IsUsed()
        {
            ServiceLocator.Configure(r => r.RegisterInstance<IObjectContentComparer>(new AlwaysEqual()));
            var order = new Order { Name = "a" };
            var tracker = Start(p => p.Track(order));

            order.Name = "b";

            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void RegisteredClonerEngine_IsUsed()
        {
            var cloner = new CountingCloner();
            ServiceLocator.Configure(r => r.RegisterInstance<IObjectCloner>(cloner));

            Start(p => p.Track(new Order { Name = "a" }));

            Assert.Equal(1, cloner.Count);   // the snapshot only
        }

        [Fact]
        public void RegisteredTypePolicy_WinsOverEngines()
        {
            var cloner = new CountingCloner();
            ServiceLocator.Configure(r =>
            {
                r.RegisterInstance<IObjectCloner>(cloner);
                r.RegisterInstance(new ChangeTrackingPolicy<Order>(o => new Order { Id = o.Id, Name = o.Name }, (a, b) => true));
            });

            var order = new Order { Name = "a" };
            var tracker = Start(p => p.Track(order));
            order.Name = "b";

            Assert.False(tracker.IsChanged);
            Assert.Equal(0, cloner.Count);
        }

        [Fact]
        public void ExplicitPolicy_WinsOverRegistered()
        {
            ServiceLocator.Configure(r => r.RegisterInstance(
                new ChangeTrackingPolicy<Order>(o => new Order { Name = o.Name }, (a, b) => true)));
            var explicitPolicy = new ChangeTrackingPolicy<Order>(o => new Order { Name = o.Name }, (a, b) => a.Name == b.Name);

            var order = new Order { Name = "a" };
            var tracker = Start(p => p.Track(order, explicitPolicy));
            order.Name = "b";

            Assert.True(tracker.IsChanged);
        }

        [Fact]
        public void ComparerOverload_UsesRegisteredCloner()
        {
            var cloner = new CountingCloner();
            ServiceLocator.Configure(r => r.RegisterInstance<IObjectCloner>(cloner));

            var order = new Order { Id = 1, Name = "a" };
            var tracker = Start(p => p.Track(order, (a, b) => a.Id == b.Id));
            order.Name = "b";

            Assert.Equal(1, cloner.Count);   // the snapshot only
            Assert.False(tracker.IsChanged);
        }

        [Fact]
        public void ComparerOverload_UsesRegisteredTypePolicyClone()
        {
            int policyClones = 0;
            ServiceLocator.Configure(r => r.RegisterInstance(new ChangeTrackingPolicy<Order>(
                o => { policyClones++; return new Order { Id = o.Id, Name = o.Name }; }, (a, b) => true)));

            Start(p => p.Track(new Order { Id = 1 }, (a, b) => a.Id == b.Id));

            Assert.Equal(1, policyClones);   // the snapshot only
        }

        [Fact]
        public void PolicyFixedAtCreation()
        {
            var order = new Order { Name = "a" };
            var tracker = Start(p => p.Track(order));

            ServiceLocator.Configure(r => r.RegisterInstance<IObjectContentComparer>(new AlwaysEqual()));
            order.Name = "b";

            Assert.True(tracker.IsChanged);   // the later configuration does not reach an existing tracker
        }
    }
}
