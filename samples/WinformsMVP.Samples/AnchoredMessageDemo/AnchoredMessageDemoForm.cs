using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demo form for the feedback division of labor.
    ///
    /// Demonstrates:
    /// - Presenter feedback defaults to Messages.ShowToast (corner) — correct for every trigger
    ///   (mouse, Alt+S/Alt+D mnemonics, the menu's Ctrl+S shortcut, Tab + Enter/Space, code)
    /// - Position-meaningful feedback via semantic view methods: ConfirmDelete() anchors a
    ///   message box at the Delete button, ShowRowTouched() anchors a toast at the grid's
    ///   current row — one explicit line each, trivially correct for all input kinds
    /// - The AnchoredToast / AnchoredMessageBox view-layer utilities (Do NOT call them from a
    ///   presenter; the semantic method is the presenter's entry)
    /// - No WinForms types or coordinates in the presenter
    /// </summary>
    public class AnchoredMessageDemoForm : Form, IAnchoredMessageDemoView
    {
        private MenuStrip _menuStrip;
        private ToolStripMenuItem _menuSave;
        private ToolStripMenuItem _menuDelete;
        private ToolStripMenuItem _menuNotify;
        private Button _saveButton;
        private Button _deleteButton;
        private TextBox _nameTextBox;
        private CheckBox _notifyCheckBox;
        private DataGridView _grid;
        private Label _hintLabel;

        private ViewActionBinder _viewActionBinder;

        public AnchoredMessageDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "WinForms MVP - Anchored Message Demo";
            this.Size = new Size(540, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            _menuStrip = new MenuStrip();
            var actionsMenu = new ToolStripMenuItem("&Actions");
            _menuSave = new ToolStripMenuItem("&Save")
            {
                ShortcutKeys = Keys.Control | Keys.S,
                ShowShortcutKeys = true
            };
            _menuDelete = new ToolStripMenuItem("&Delete...");
            _menuNotify = new ToolStripMenuItem("&Notify");
            actionsMenu.DropDownItems.Add(_menuSave);
            actionsMenu.DropDownItems.Add(_menuDelete);
            actionsMenu.DropDownItems.Add(new ToolStripSeparator());
            actionsMenu.DropDownItems.Add(_menuNotify);
            _menuStrip.Items.Add(actionsMenu);

            var titleLabel = new Label
            {
                Text = "Anchored Message Demo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 36),
                Size = new Size(480, 28),
                ForeColor = Color.FromArgb(0, 100, 160)
            };

            var infoLabel = new Label
            {
                Text = "Save/Notify/Toggle: corner toast (Messages — right for any trigger).\n" +
                       "Delete and grid rows: anchored via semantic view methods.",
                Location = new Point(20, 66),
                Size = new Size(480, 30),
                ForeColor = Color.DarkGray
            };

            _saveButton = new Button
            {
                Text = "&Save",
                Location = new Point(20, 104),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(0, 128, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;

            _deleteButton = new Button
            {
                Text = "&Delete...",
                Location = new Point(152, 104),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(192, 64, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _deleteButton.FlatAppearance.BorderSize = 0;

            var nameLabel = new Label
            {
                Text = "Item:",
                Location = new Point(20, 150),
                Size = new Size(40, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _nameTextBox = new TextBox
            {
                Text = "Alpha",
                Location = new Point(64, 147),
                Size = new Size(180, 23)
            };

            _notifyCheckBox = new CheckBox
            {
                Text = "Enable &notifications",
                Location = new Point(290, 147),
                Size = new Size(200, 24)
            };

            var gridLabel = new Label
            {
                Text = "Click any row — the toast is anchored at the current row:",
                Location = new Point(20, 184),
                Size = new Size(480, 20),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            _grid = new DataGridView
            {
                Location = new Point(20, 206),
                Size = new Size(480, 120),
                ColumnCount = 2,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false
            };
            _grid.Columns[0].HeaderText = "Item";
            _grid.Columns[0].Width = 200;
            _grid.Columns[1].HeaderText = "Status";
            _grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Rows.Add("Alpha", "Active");
            _grid.Rows.Add("Beta", "Pending");
            _grid.Rows.Add("Gamma", "Active");
            _grid.Rows.Add("Delta", "Archived");

            _hintLabel = new Label
            {
                Text = "Hint: try Alt+D — the confirmation appears at the Delete button no matter\n" +
                       "how it was triggered, because the view anchors it explicitly.",
                Location = new Point(20, 338),
                Size = new Size(480, 56),
                ForeColor = Color.FromArgb(0, 80, 140),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic)
            };

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                titleLabel, infoLabel,
                _saveButton, _deleteButton,
                nameLabel, _nameTextBox, _notifyCheckBox,
                gridLabel, _grid,
                _hintLabel
            });

            this.Controls.Add(_menuStrip);
            this.MainMenuStrip = _menuStrip;
        }

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(AnchoredMessageDemoActions.Save, _saveButton, _menuSave);
            _viewActionBinder.Add(AnchoredMessageDemoActions.Delete, _deleteButton, _menuDelete);
            _viewActionBinder.Add(AnchoredMessageDemoActions.MenuNotify, _menuNotify);
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
