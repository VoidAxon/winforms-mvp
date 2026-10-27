using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common;
using Xunit;
using Row = Kibinago.Samples.Tests.Common.SortableBindingListTests.Row;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>The list as WinForms data binding uses it: through a BindingSource and a DataGridView.</summary>
    public class SortableBindingListWinFormsTests
    {
        private static SortableBindingList<Row> Wards() => new SortableBindingList<Row>(new[]
        {
            new Row(1, "B", 2), new Row(2, "A", 9), new Row(3, "B", 1), new Row(4, "A", 3), new Row(5, "B", 2),
        });

        [Fact]
        public void BindingSourceSort_WithSeveralColumns_SortsTheList()
        {
            var list = Wards();
            using (var source = new BindingSource { DataSource = list })
            {
                source.Sort = "Ward, No DESC";

                Assert.True(source.SupportsAdvancedSorting);
                Assert.True(source.IsSorted);
                Assert.Equal(new[] { 2, 4, 1, 5, 3 }, list.Select(r => r.Id).ToArray());
            }
        }

        [Fact]
        public void BindingSourceRemoveSort_KeepsTheOrder()
        {
            var list = Wards();
            using (var source = new BindingSource { DataSource = list })
            {
                source.Sort = "No";
                source.RemoveSort();

                Assert.False(source.IsSorted);
                Assert.Equal(new[] { 3, 1, 5, 4, 2 }, list.Select(r => r.Id).ToArray());
            }
        }

        [Fact]
        public void DataGridViewSort_OnABoundColumn_SortsTheList()
        {
            var list = Wards();
            using (var grid = new DataGridView { BindingContext = new BindingContext() })
            {
                grid.DataSource = list;

                grid.Sort(grid.Columns[nameof(Row.No)], ListSortDirection.Descending);

                Assert.Equal(new[] { 2, 4, 1, 5, 3 }, list.Select(r => r.Id).ToArray());
                Assert.Equal(SortOrder.Descending, grid.SortOrder);
                Assert.Equal(2, ((Row)grid.Rows[0].DataBoundItem).Id);
            }
        }
    }
}
