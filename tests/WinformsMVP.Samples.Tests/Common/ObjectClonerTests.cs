using System;
using System.Collections.Generic;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ObjectClonerTests
    {
        // Plain POCO: no ICloneable, no Equals override. Uses auto-properties to verify that backing fields are copied.
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void DeepCopy_Null_ReturnsNull()
        {
            Assert.Null(ObjectCloner.DeepCopy<PlainPoco>(null));
        }

        [Fact]
        public void DeepCopy_Immutables_ReturnedAsIs()
        {
            Assert.Equal(42, ObjectCloner.DeepCopy(42));
            Assert.Equal("hello", ObjectCloner.DeepCopy("hello"));
            var now = DateTime.Now;
            Assert.Equal(now, ObjectCloner.DeepCopy(now));
        }

        [Fact]
        public void DeepCopy_FlatPoco_CopiesAllFields_AndIsIndependent()
        {
            var src = new PlainPoco { Id = 1, Name = "orig" };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src, copy);
            Assert.Equal(1, copy.Id);
            Assert.Equal("orig", copy.Name);

            copy.Name = "changed";
            Assert.Equal("orig", src.Name);   // mutating the copy must not affect the original
        }

        private class Parent
        {
            public string Title { get; set; }
            public PlainPoco Child { get; set; }
            public List<PlainPoco> Items { get; set; }
            public Dictionary<string, PlainPoco> Map { get; set; }
            public int[] Numbers { get; set; }
        }

        [Fact]
        public void DeepCopy_NestedPoco_IsDeep()
        {
            var src = new Parent { Title = "p", Child = new PlainPoco { Id = 1, Name = "c" } };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src.Child, copy.Child);
            Assert.Equal("c", copy.Child.Name);
            copy.Child.Name = "x";
            Assert.Equal("c", src.Child.Name);
        }

        [Fact]
        public void DeepCopy_Collections_AreDeep()
        {
            var src = new Parent
            {
                Items = new List<PlainPoco> { new PlainPoco { Id = 1, Name = "a" } },
                Map = new Dictionary<string, PlainPoco> { ["k"] = new PlainPoco { Id = 2, Name = "b" } },
                Numbers = new[] { 1, 2, 3 }
            };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src.Items, copy.Items);
            Assert.NotSame(src.Items[0], copy.Items[0]);
            Assert.Equal("a", copy.Items[0].Name);

            Assert.NotSame(src.Map, copy.Map);
            Assert.NotSame(src.Map["k"], copy.Map["k"]);
            Assert.Equal("b", copy.Map["k"].Name);

            Assert.NotSame(src.Numbers, copy.Numbers);
            Assert.Equal(new[] { 1, 2, 3 }, copy.Numbers);

            copy.Items[0].Name = "z";
            Assert.Equal("a", src.Items[0].Name);
        }

        private class Node
        {
            public string Name { get; set; }
            public Node Next { get; set; }
        }

        private class CloneableLeaf : ICloneable
        {
            public string Tag { get; set; }
            public bool Cloned { get; private set; }
            public object Clone() => new CloneableLeaf { Tag = this.Tag, Cloned = true };
        }

        private class HasDelegate
        {
            public string Name { get; set; }
            public Action Handler { get; set; }
        }

        private class HasStream
        {
            public System.IO.MemoryStream Stream { get; set; }
        }

        [Fact]
        public void DeepCopy_CyclicGraph_DoesNotStackOverflow_AndPreservesTopology()
        {
            var a = new Node { Name = "a" };
            var b = new Node { Name = "b" };
            a.Next = b;
            b.Next = a;   // cycle

            var copyA = ObjectCloner.DeepCopy(a);

            Assert.NotSame(a, copyA);
            Assert.Equal("a", copyA.Name);
            Assert.Equal("b", copyA.Next.Name);
            Assert.Same(copyA, copyA.Next.Next);   // cycle is preserved within the copy
        }

        [Fact]
        public void DeepCopy_NodeImplementingICloneable_UsesCloneMethod()
        {
            var src = new CloneableLeaf { Tag = "t" };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.True(copy.Cloned);   // ICloneable.Clone() was used, not reflection
            Assert.Equal("t", copy.Tag);
        }

        [Fact]
        public void DeepCopy_DelegateField_IsSkipped()
        {
            var src = new HasDelegate { Name = "n", Handler = () => { } };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.Equal("n", copy.Name);
            Assert.Null(copy.Handler);   // delegate field is skipped
        }

        [Fact]
        public void DeepCopy_UnsupportedType_ThrowsNotSupported()
        {
            var src = new HasStream { Stream = new System.IO.MemoryStream() };

            Assert.Throws<NotSupportedException>(() => ObjectCloner.DeepCopy(src));
        }

        [Fact]
        public void DeepCopy_MultiDimensionalArray_ThrowsNotSupported()
        {
            var src = new int[2, 2];

            Assert.Throws<NotSupportedException>(() => ObjectCloner.DeepCopy(src));
        }

        [Fact]
        public void DeepCopy_DictionaryWithCustomComparer_PreservesComparer()
        {
            var src = new Dictionary<string, PlainPoco>(StringComparer.OrdinalIgnoreCase)
            {
                ["Key"] = new PlainPoco { Id = 1, Name = "a" }
            };

            var copy = ObjectCloner.DeepCopy(src);

            // The custom comparer must survive the copy: case-insensitive lookup still works.
            Assert.True(copy.ContainsKey("KEY"));
            Assert.Equal("a", copy["key"].Name);
        }
    }
}
