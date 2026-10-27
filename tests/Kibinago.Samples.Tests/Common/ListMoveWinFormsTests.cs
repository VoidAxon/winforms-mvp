using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common;
using Xunit;
using Row = Kibinago.Samples.Tests.Common.SortableBindingListTests.Row;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>Moves as WinForms data binding sees them: the current row follows an ItemMoved.</summary>
    public class ListMoveWinFormsTests
    {
        private static Row[] Data() => new[] { new Row(1, "A", 1), new Row(2, "A", 2), new Row(3, "B", 3), new Row(4, "B", 4) };

        [Fact]
        public void SortableBindingList_TheCurrentRowFollowsTheMovedRow()
        {
            var rows = new SortableBindingList<Row>(Data());
            using (var source = new BindingSource { DataSource = rows })
            {
                source.Position = 3;
                var moved = rows[3];

                rows.MoveToTop(moved);

                Assert.Equal(0, source.Position);
                Assert.Same(moved, source.Current);
            }
        }

        [Fact]
        public void BindingList_TheCurrentRowLandsOnAnotherRow_TheViewSelectsItAgain()
        {
            var rows = new BindingList<Row>(Data().ToList());
            using (var source = new BindingSource { DataSource = rows })
            {
                source.Position = 3;
                var moved = rows[3];

                rows.MoveToTop(moved);

                Assert.NotSame(moved, source.Current);
            }
        }

        [Fact]
        public void DataGridView_CurrentCellFollowsTheMovedRow()
        {
            var rows = new SortableBindingList<Row>(Data());
            using (var grid = new DataGridView { BindingContext = new BindingContext() })
            {
                grid.DataSource = rows;
                grid.CurrentCell = grid.Rows[2].Cells[0];

                rows.MoveUp(rows[2]);

                Assert.Equal(1, grid.CurrentCellAddress.Y);
                Assert.Equal(3, ((Row)grid.CurrentRow.DataBoundItem).Id);
            }
        }
    }
}
