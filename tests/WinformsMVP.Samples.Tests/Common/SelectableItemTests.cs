using System;
using System.Linq;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class SelectableItemTests
    {
        private enum Color { Red, Green, Blue }

        [Fact]
        public void Of_InfersType_AndDefaultsTextToValueToString()
        {
            var item = SelectableItem.Of(2025);

            Assert.Equal(2025, item.Value);
            Assert.Equal("2025", item.Text);
            Assert.Equal("2025", item.ToString());
            Assert.Equal((object)2025, item.Key);
        }

        [Fact]
        public void Of_UsesExplicitText()
        {
            var item = SelectableItem.Of(2025, "2025 fiscal year");
            Assert.Equal("2025 fiscal year", item.Text);
        }

        [Fact]
        public void Equality_IsByValue_IgnoringText()
        {
            var a = SelectableItem.Of(2025, "label A");
            var b = SelectableItem.Of(2025, "label B");

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_DiffersByValue()
        {
            Assert.NotEqual(SelectableItem.Of(1), SelectableItem.Of(2));
        }

        [Fact]
        public void From_WrapsSequence_WithTextSelector()
        {
            var items = SelectableItem.From(new[] { 1, 2, 3 }, n => "#" + n);

            Assert.Equal(3, items.Count);
            Assert.Equal("#2", items[1].Text);
            Assert.Equal(2, items[1].Value);
        }

        [Fact]
        public void From_NullSequence_ReturnsEmpty()
        {
            Assert.Empty(SelectableItem.From<int>(null));
        }

        [Fact]
        public void FromEnum_WrapsEveryEnumValue()
        {
            var items = SelectableItem.FromEnum<Color>();

            Assert.Equal(new[] { Color.Red, Color.Green, Color.Blue },
                         items.Select(i => i.Value).ToArray());
        }

        [Fact]
        public void FromEnum_NonEnum_Throws()
        {
            Assert.Throws<ArgumentException>(() => SelectableItem.FromEnum<int>());
        }
    }
}
