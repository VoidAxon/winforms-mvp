using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
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
            var parent = new SelectionStore<Node>();
            var child = new SelectionStore<Node>();
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
            var parent = new SelectionStore<Node>();
            var p0 = new Node(5);
            parent.Select(p0);
            var child = new SelectionStore<Node>();

            Node reloadedWith = null;
            int reloadCount = 0;
            Cascade.Bind(parent, child, p => { reloadCount++; reloadedWith = p; });   // initialSync default true

            Assert.Equal(1, reloadCount);
            Assert.Same(p0, reloadedWith);
        }

        [Fact]
        public void Bind_InitialSyncFalse_DoesNotReloadAtBind()
        {
            var parent = new SelectionStore<Node>();
            parent.Select(new Node(5));
            var child = new SelectionStore<Node>();
            int reloadCount = 0;

            Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_Dispose_StopsReacting()
        {
            var parent = new SelectionStore<Node>();
            var child = new SelectionStore<Node>();
            int reloadCount = 0;
            var binding = Cascade.Bind(parent, child, p => reloadCount++, initialSync: false);

            binding.Dispose();
            parent.Select(new Node(1));

            Assert.Equal(0, reloadCount);
        }

        [Fact]
        public void Bind_ThreeLevels_TopChange_ClearsAndReloadsEachLevelOnce()
        {
            var a = new SelectionStore<Node>();   // top
            var b = new SelectionStore<Node>();   // middle
            var c = new SelectionStore<Node>();   // leaf
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
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var c = new SelectionStore<Node>();
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
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var target = new SelectionStore<Node>();
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
            var a = new SelectionStore<Node>();
            var b = new SelectionStore<Node>();
            var target = new SelectionStore<Node>();
            int reloadCount = 0;
            var binding = Cascade.Combine(a, b, target, (av, bv) => reloadCount++, initialSync: false);

            binding.Dispose();
            a.Select(new Node(1));
            b.Select(new Node(2));

            Assert.Equal(0, reloadCount);
        }
    }
}
