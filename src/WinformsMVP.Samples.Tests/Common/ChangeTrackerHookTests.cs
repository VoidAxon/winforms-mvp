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
    }

    // Collection definition (empty class carrying CollectionDefinition attribute only).
    [CollectionDefinition("ChangeTrackerDefaults", DisableParallelization = true)]
    public class ChangeTrackerDefaultsCollection { }
}
