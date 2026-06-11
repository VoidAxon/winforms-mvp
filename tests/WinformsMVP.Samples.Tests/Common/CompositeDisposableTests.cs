using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class CompositeDisposableTests
    {
        private sealed class Tracked : IDisposable
        {
            private readonly List<string> _log;
            private readonly string _name;
            public int DisposeCount;
            public Tracked(List<string> log, string name) { _log = log; _name = name; }
            public void Dispose() { DisposeCount++; _log.Add(_name); }
        }

        [Fact]
        public void Dispose_ReleasesMembersInReverseOrder()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            bag.Add(new Tracked(log, "a"));
            bag.Add(new Tracked(log, "b"));
            bag.Add(new Tracked(log, "c"));

            bag.Dispose();

            Assert.Equal(new[] { "c", "b", "a" }, log);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var log = new List<string>();
            var item = new Tracked(log, "x");
            var bag = new CompositeDisposable();
            bag.Add(item);

            bag.Dispose();
            bag.Dispose();

            Assert.Equal(1, item.DisposeCount);
        }

        [Fact]
        public void Add_AfterDispose_DisposesImmediately()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            bag.Dispose();

            var late = new Tracked(log, "late");
            bag.Add(late);

            Assert.Equal(1, late.DisposeCount);
        }

        [Fact]
        public void Add_Null_IsIgnored()
        {
            var bag = new CompositeDisposable();
            bag.Add(null);
            bag.Dispose();   // must not throw
        }

        [Fact]
        public void DisposeWith_RegistersAndReturnsSameInstance()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            var item = new Tracked(log, "x");

            var returned = item.DisposeWith(bag);

            Assert.Same(item, returned);
            bag.Dispose();
            Assert.Equal(1, item.DisposeCount);
        }

        [Fact]
        public void DisposableCreate_RunsActionOnceOnly()
        {
            int calls = 0;
            var d = Disposable.Create(() => calls++);
            d.Dispose();
            d.Dispose();
            Assert.Equal(1, calls);
        }

        [Fact]
        public void DisposableCreate_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Disposable.Create(null));
        }
    }
}
