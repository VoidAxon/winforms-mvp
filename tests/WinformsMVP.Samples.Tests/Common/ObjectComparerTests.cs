using System;
using System.Collections.Generic;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ObjectComparerTests
    {
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class Parent
        {
            public string Title { get; set; }
            public PlainPoco Child { get; set; }
            public List<PlainPoco> Items { get; set; }
        }

        private class Node
        {
            public string Name { get; set; }
            public Node Next { get; set; }
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
        public void DeepEquals_NullCases()
        {
            Assert.True(ObjectComparer.DeepEquals<PlainPoco>(null, null));
            Assert.False(ObjectComparer.DeepEquals(new PlainPoco(), null));
            Assert.False(ObjectComparer.DeepEquals(null, new PlainPoco()));
        }

        [Fact]
        public void DeepEquals_SameReference_IsTrue()
        {
            var p = new PlainPoco { Id = 1 };
            Assert.True(ObjectComparer.DeepEquals(p, p));
        }

        [Fact]
        public void DeepEquals_FlatPoco_StructuralEquality()
        {
            var a = new PlainPoco { Id = 1, Name = "x" };
            var b = new PlainPoco { Id = 1, Name = "x" };
            var c = new PlainPoco { Id = 1, Name = "y" };

            Assert.True(ObjectComparer.DeepEquals(a, b));
            Assert.False(ObjectComparer.DeepEquals(a, c));
        }

        [Fact]
        public void DeepEquals_Nested_And_Collections()
        {
            var a = new Parent
            {
                Title = "p",
                Child = new PlainPoco { Id = 1, Name = "c" },
                Items = new List<PlainPoco> { new PlainPoco { Id = 9, Name = "i" } }
            };
            var b = new Parent
            {
                Title = "p",
                Child = new PlainPoco { Id = 1, Name = "c" },
                Items = new List<PlainPoco> { new PlainPoco { Id = 9, Name = "i" } }
            };
            Assert.True(ObjectComparer.DeepEquals(a, b));

            b.Items[0].Name = "different";
            Assert.False(ObjectComparer.DeepEquals(a, b));   // difference nested inside a collection
        }

        [Fact]
        public void DeepEquals_CyclicGraph_Terminates()
        {
            var a1 = new Node { Name = "a" }; var a2 = new Node { Name = "b" };
            a1.Next = a2; a2.Next = a1;
            var b1 = new Node { Name = "a" }; var b2 = new Node { Name = "b" };
            b1.Next = b2; b2.Next = b1;

            Assert.True(ObjectComparer.DeepEquals(a1, b1));
        }

        [Fact]
        public void DeepEquals_DelegateFields_AreIgnored()
        {
            var a = new HasDelegate { Name = "n", Handler = () => { } };
            var b = new HasDelegate { Name = "n", Handler = () => { } };

            Assert.True(ObjectComparer.DeepEquals(a, b));   // delegates are ignored; only Name is compared
        }

        [Fact]
        public void DeepEquals_UnsupportedType_ThrowsNotSupported()
        {
            var a = new HasStream { Stream = new System.IO.MemoryStream() };
            var b = new HasStream { Stream = new System.IO.MemoryStream() };

            Assert.Throws<NotSupportedException>(() => ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_Dictionary_DifferentValues_IsFalse()
        {
            var a = new Dictionary<string, PlainPoco> { ["k"] = new PlainPoco { Id = 1, Name = "x" } };
            var b = new Dictionary<string, PlainPoco> { ["k"] = new PlainPoco { Id = 1, Name = "y" } };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_Dictionary_DifferentCounts_IsFalse()
        {
            var a = new Dictionary<string, int> { ["a"] = 1 };
            var b = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_Dictionary_DifferentKeys_SameCount_IsFalse()
        {
            var a = new Dictionary<string, int> { ["a"] = 1 };
            var b = new Dictionary<string, int> { ["b"] = 1 };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_Array_DifferentLength_IsFalse()
        {
            var a = new[] { 1, 2, 3 };
            var b = new[] { 1, 2 };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_Array_DifferentElements_IsFalse()
        {
            var a = new[] { 1, 2, 3 };
            var b = new[] { 1, 9, 3 };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_List_DifferentCount_IsFalse()
        {
            var a = new List<int> { 1, 2 };
            var b = new List<int> { 1, 2, 3 };
            Assert.False(ObjectComparer.DeepEquals(a, b));
        }

        [Fact]
        public void DeepEquals_DifferentRuntimeTypes_IsFalse()
        {
            // Two unrelated POCOs with structurally similar data must still compare unequal.
            var a = new PlainPoco { Id = 1, Name = "x" };
            var b = new Parent { Title = "x" };
            Assert.False(ObjectComparer.DeepEquals<object>(a, b));
        }

        [Fact]
        public void DeepEquals_MultiDimensionalArray_ThrowsNotSupported()
        {
            var a = new int[2, 2];
            var b = new int[2, 2];
            Assert.Throws<NotSupportedException>(() => ObjectComparer.DeepEquals(a, b));
        }
    }
}
