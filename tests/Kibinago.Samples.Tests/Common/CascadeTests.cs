using Kibinago.Common;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    public class CascadeTests
    {
        private sealed class Node
        {
            public int Id;
            public Node(int id) { Id = id; }
        }

        [Fact]
        public void Bind_ParentChange_ClearsChildAndReloadsWithParentValue()
        {
            var parent = new Selection<Node>();
            var child = new Selection<Node>();
            child.Select(new Node(99));                 // pre-existing child selection

            Node reloadedWith = null;
            int reloadCount = 0;
            // initialSync:false so we count only the change-driven reload
            Cascade.Bind(parent, child, p => { reloadCount++; reloadedWith = p; }, initialSync: false);

            var c = new Node(1);
            parent.Select(c);

            Assert.Null(child.Current);                 // child cleared
            Assert.Equal(1, reloadCount);
            Assert.Same(c, reloadedWith);               // reloaded with the new parent value
        }

        [Fact]
        public void Bind_InitialSync_ReloadsOnceAtBindWithCurrentParent()
        {
            var parent = new Selection<Node>();
            var p0 = new Node(5);
            parent.Select(p0);
            var child = new Selection<Node>();

            Node reloadedWith = null;
            int reloadCount = 0;
            Cascade.Bind(parent, child, p => { reloadCount++; reloadedWith = p; });   // initialSync default true

            Assert.Equal(1, reloadCount);
            Assert.Same(p0, reloadedWith);
        }

        [Fact]
        public void Bind_InitialSyncFalse_DoesNotReloadAtBind()
        {
            var parent = new Selection<Node>();
            parent.Select(new Node(5));
            var child = new Selection<Node>();
            int reloadCount = 0;

            Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_Dispose_StopsReacting()
        {
            var parent = new Selection<Node>();
            var child = new Selection<Node>();
            int reloadCount = 0;
            var binding = Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            binding.Dispose();
            parent.Select(new Node(1));

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_ThreeLevels_TopChange_ClearsAndReloadsEachLevelOnce()
        {
            var a = new Selection<Node>();   // top
            var b = new Selection<Node>();   // middle
            var c = new Selection<Node>();   // leaf
            b.Select(new Node(20));
            c.Select(new Node(30));

            int reloadB = 0, reloadC = 0;
            Node bReloadArg = new Node(-1), cReloadArg = new Node(-1);
            Cascade.Bind(a, b, p => { reloadB++; bReloadArg = p; }, initialSync: false);
            Cascade.Bind(b, c, p => { reloadC++; cReloadArg = p; }, initialSync: false);

            a.Select(new Node(1));

            Assert.Null(b.Current);
            Assert.Null(c.Current);
            Assert.Equal(1, reloadB);
            Assert.Equal(1, reloadC);
            Assert.Same(a.Current, bReloadArg);   // b reloaded with the new top value
            Assert.Null(cReloadArg);              // c reloaded with b's value, which is now null
        }

        [Fact]
        public void Bind_ClearingAlreadyEmptyChild_DoesNotReloadGrandchildExtraTimes()
        {
            var a = new Selection<Node>();
            var b = new Selection<Node>();
            var c = new Selection<Node>();
            // b and c start empty (null)

            int reloadC = 0;
            Cascade.Bind(a, b, p => { }, initialSync: false);
            Cascade.Bind(b, c, p => reloadC++, initialSync: false);

            a.Select(new Node(1));   // clears b (already null -> short-circuit, no fire) then reloads b

            Assert.Equal(0, reloadC);   // grandchild reload never triggered (b stayed null)
        }

        [Fact]
        public void Combine_EitherParentChange_ClearsTargetAndReloadsWithBoth()
        {
            var a = new Selection<Node>();
            var b = new Selection<Node>();
            var target = new Selection<Node>();
            target.Select(new Node(7));

            Node lastA = null, lastB = null;
            int reloadCount = 0;
            Cascade.Combine(a, b, target,
                (av, bv) => { reloadCount++; lastA = av; lastB = bv; }, initialSync: false);

            var a1 = new Node(1);
            a.Select(a1);
            Assert.Null(target.Current);          // cleared on a change
            Assert.Equal(1, reloadCount);
            Assert.Same(a1, lastA);
            Assert.Null(lastB);

            var b1 = new Node(2);
            b.Select(b1);
            Assert.Equal(2, reloadCount);
            Assert.Same(a1, lastA);               // a's current still present
            Assert.Same(b1, lastB);
        }

        [Fact]
        public void Combine_Dispose_UnsubscribesBothParents()
        {
            var a = new Selection<Node>();
            var b = new Selection<Node>();
            var target = new Selection<Node>();
            int reloadCount = 0;
            var binding = Cascade.Combine(a, b, target, (av, bv) => reloadCount++, initialSync: false);

            binding.Dispose();
            a.Select(new Node(1));
            b.Select(new Node(2));

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_ParentReloadedAsNewInstanceOfSameRecord_KeepsChild()
        {
            var parent = new Selection<Node>(new NodeById());
            var child = new Selection<Node>();
            parent.Select(new Node(1));
            int reloads = 0;
            using (Cascade.Bind(parent, child, p => reloads++, initialSync: false))
            {
                var kept = new Node(10);
                child.Select(kept);

                parent.Select(new Node(1));   // list reload: same record, new object

                Assert.Equal(0, reloads);
                Assert.Same(kept, child.Current);
            }
        }

        [Fact]
        public void Combine_ParentReloadedAsNewInstanceOfSameRecord_DoesNotReload()
        {
            var a = new Selection<Node>(new NodeById());
            var b = new Selection<Node>(new NodeById());
            var target = new Selection<Node>();
            a.Select(new Node(1));
            b.Select(new Node(2));
            int reloads = 0;
            using (Cascade.Combine(a, b, target, (x, y) => reloads++, initialSync: false))
            {
                a.Select(new Node(1));
                b.Select(new Node(2));

                Assert.Equal(0, reloads);
            }
        }

        private sealed class NodeById : System.Collections.Generic.IEqualityComparer<Node>
        {
            public bool Equals(Node x, Node y) => ReferenceEquals(x, y) || (x != null && y != null && x.Id == y.Id);
            public int GetHashCode(Node obj) => obj == null ? 0 : obj.Id;
        }
    }
}
