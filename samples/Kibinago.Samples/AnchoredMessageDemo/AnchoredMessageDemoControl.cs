using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Services;

namespace Kibinago.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demo form for the feedback division of labor.
    ///
    /// Demonstrates:
    /// - Presenter feedback defaults to Toasts.Show (corner) — correct for every trigger
    ///   (mouse, Alt+S/Alt+D mnemonics, the menu's Ctrl+S shortcut, Tab + Enter/Space, code)
    /// - Position-meaningful feedback via semantic view methods: ConfirmDelete() anchors a
    ///   message box at the Delete button, ShowRowTouched() anchors a toast at the grid's
    ///   current row — one explicit line each, trivially correct for all input kinds
    /// - The AnchoredToast / AnchoredMessageBox view-layer utilities (Do NOT call them from a
    ///   presenter; the semantic method is the presenter's entry)
    /// - No WinForms types or coordinates in the presenter
    /// </summary>
    public partial class AnchoredMessageDemoControl : UserControl, IAnchoredMessageDemoView
    {
        private ViewActionBinder _viewActionBinder;

        public AnchoredMessageDemoControl()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(AnchoredMessageDemoActions.Save, _saveButton, _menuSave);
            _viewActionBinder.Add(AnchoredMessageDemoActions.Delete, _deleteButton, _menuDelete);
            _viewActionBinder.Add(AnchoredMessageDemoActions.MenuNotify, _menuNotify);
            // A grid has no built-in binding strategy (which of its events is "the" action?): say it explicitly.
            _viewActionBinder.RegisterStrategy<DataGridView>(
                (g, h) => g.Click += h,
                (g, h) => g.Click -= h);
            _viewActionBinder.Add(AnchoredMessageDemoActions.GridTouch, _grid);
            _viewActionBinder.Add(AnchoredMessageDemoActions.ToggleNotify, _notifyCheckBox);
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        public string ItemName => _nameTextBox.Text;

        public bool NotificationsEnabled => _notifyCheckBox.Checked;

        public void ShowHint(string message)
        {
            _hintLabel.Text = message;
        }

        // Semantic feedback methods: the placement decision lives here, in the view, where the
        // controls are. One explicit anchor line each — correct for mouse, mnemonic, keyboard,
        // and programmatic triggers alike.

        public bool ConfirmDelete()
        {
            var anchor = _deleteButton.PointToScreen(new Point(0, _deleteButton.Height));
            return AnchoredMessageBox.ConfirmYesNo("Delete this item?", anchor, "Confirm");
        }

        public void ShowRowTouched()
        {
            var address = _grid.CurrentCellAddress;
            Point anchor;
            if (address.Y >= 0)
            {
                var cell = _grid.GetCellDisplayRectangle(address.X, address.Y, false);
                anchor = _grid.PointToScreen(new Point(cell.Left, cell.Bottom));
            }
            else
            {
                anchor = _grid.PointToScreen(new Point(0, _grid.Height));
            }
            AnchoredToast.Show("Row touched", ToastType.Info, anchor);
        }
    }
}
