using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ListDemo
{
    public partial class SortDemoControl : UserControl, ISortDemoView
    {
        private const string AllWards = "すべて";
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private SortableBindingList<PatientRow> _rows;

        public SortDemoControl()
        {
            InitializeComponent();

            _binder.Add(SortDemoActions.SortByWard, _sortByWardButton);
            _binder.Add(SortDemoActions.Resort, _resortButton);
            _binder.Add(SortDemoActions.RemoveSort, _removeSortButton);
            _binder.Add(SortDemoActions.Reload, _reloadButton);

            _wardFilter.SelectedIndexChanged += (s, e) => WardChanged?.Invoke(this, EventArgs.Empty);
            _keepSorted.CheckedChanged += (s, e) => KeepSortedChanged?.Invoke(this, EventArgs.Empty);
            _grid.ColumnHeaderMouseClick += OnHeaderClick;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedWard => _wardFilter.SelectedItem as string == AllWards ? null : _wardFilter.SelectedItem as string;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool KeepSorted => _keepSorted.Checked;

        public event EventHandler WardChanged;
        public event EventHandler KeepSortedChanged;

        public void SetRows(SortableBindingList<PatientRow> rows)
        {
            _rows = rows;
            _grid.DataSource = rows;
            foreach (DataGridViewColumn column in _grid.Columns)
                column.SortMode = DataGridViewColumnSortMode.Programmatic;   // the header click below decides
            _grid.Columns[nameof(PatientRow.Ward)].HeaderText = "病棟";
            _grid.Columns[nameof(PatientRow.No)].HeaderText = "番号";
            _grid.Columns[nameof(PatientRow.Name)].HeaderText = "氏名";
            _grid.Columns[nameof(PatientRow.AdmittedOn)].HeaderText = "入院日";
            _grid.Columns[nameof(PatientRow.Status)].HeaderText = "状態";
            // A Reset makes the grid redraw its ▲▼ from the last column only: put them back afterwards.
            rows.ListChanged += (s, e) => { if (IsHandleCreated) BeginInvoke((MethodInvoker)ShowGlyphs); };
        }

        public void SetWards(IReadOnlyList<string> wards)
        {
            _wardFilter.Items.Clear();
            _wardFilter.Items.Add(AllWards);
            foreach (var ward in wards)
                _wardFilter.Items.Add(ward);
            _wardFilter.SelectedIndex = 0;
        }

        public void ShowStatus(string text) => _statusLabel.Text = text;

        // Click = that column only, Shift+click = add a column, Ctrl+click = remove it (as DevExpress,
        // Telerik and ag-Grid do). The list takes several columns as they are; nothing else is needed.
        private void OnHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.ColumnIndex < 0 || _rows == null) return;   // -1: the top-left corner
            var column = TypeDescriptor.GetProperties(typeof(PatientRow))[_grid.Columns[e.ColumnIndex].DataPropertyName];
            var keys = _rows.SortDescriptions.Cast<ListSortDescription>().ToList();
            var index = keys.FindIndex(k => k.PropertyDescriptor.Name == column.Name);

            if ((ModifierKeys & Keys.Control) != 0)
            {
                if (index >= 0) keys.RemoveAt(index);
            }
            else
            {
                var direction = index >= 0 && keys[index].SortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
                var key = new ListSortDescription(column, direction);
                if ((ModifierKeys & Keys.Shift) == 0) keys = new List<ListSortDescription> { key };
                else if (index >= 0) keys[index] = key;
                else keys.Add(key);
            }
            _rows.ApplySort(new ListSortDescriptionCollection(keys.ToArray()));   // empty: no sort
        }

        private void ShowGlyphs()
        {
            if (_rows == null) return;
            var keys = _rows.SortDescriptions?.Cast<ListSortDescription>().ToList() ?? new List<ListSortDescription>();
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                var key = keys.FirstOrDefault(k => k.PropertyDescriptor.Name == column.DataPropertyName);
                column.HeaderCell.SortGlyphDirection = key == null ? SortOrder.None
                    : key.SortDirection == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            }
        }
    }
}
