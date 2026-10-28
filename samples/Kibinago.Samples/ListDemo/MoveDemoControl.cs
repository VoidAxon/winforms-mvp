using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ListDemo
{
    public partial class MoveDemoControl : UserControl, IMoveDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public MoveDemoControl()
        {
            InitializeComponent();

            _binder.Add(MoveDemoActions.MoveUp, _upButton);
            _binder.Add(MoveDemoActions.MoveDown, _downButton);
            _binder.Add(MoveDemoActions.MoveToTop, _topButton);
            _binder.Add(MoveDemoActions.MoveToBottom, _bottomButton);

            _grid.CurrentCellChanged += (s, e) => CurrentRowChanged?.Invoke(this, EventArgs.Empty);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int? CurrentId => (_grid.CurrentRow?.DataBoundItem as PhraseRow)?.Id;

        public event EventHandler CurrentRowChanged;

        public void SetRows(SortableBindingList<PhraseRow> rows)
        {
            _grid.DataSource = rows;
            foreach (DataGridViewColumn column in _grid.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;   // the order IS the display order
            _grid.Columns[nameof(PhraseRow.Id)].HeaderText = "ID";
            _grid.Columns[nameof(PhraseRow.DisplayOrder)].HeaderText = "表示順";
            _grid.Columns[nameof(PhraseRow.Text)].HeaderText = "定型文";
            _grid.Columns[nameof(PhraseRow.Text)].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        // With a SortableBindingList the current row already follows a moved row (one ItemMoved);
        // this matters for lists that move by RemoveAt + Insert.
        public void SelectRow(int id)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if ((row.DataBoundItem as PhraseRow)?.Id == id)
                {
                    _grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
            _grid.Refresh();   // the moved rows show their new display order
        }

        public void Log(string text) => _log.Items.Insert(0, text);
    }
}
