using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demo form for cursor-anchored feedback (toast + message box).
    ///
    /// Demonstrates:
    /// - View.ShowToast() called from the presenter via IViewBase extensions
    /// - View.ConfirmYesNo() producing a message box anchored at the cursor
    /// - DataGridView Click bound to a ViewAction (GridTouch) via the binder's generic Control.Click fallback
    /// - No WinForms types or coordinates in the presenter
    /// </summary>
    public class AnchoredMessageDemoForm : Form, IAnchoredMessageDemoView
    {
        private Button _saveButton;
        private Button _deleteButton;
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
            this.Size = new Size(520, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            var titleLabel = new Label
            {
                Text = "Anchored Message Demo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 16),
                Size = new Size(460, 28),
                ForeColor = Color.FromArgb(0, 100, 160)
            };

            var infoLabel = new Label
            {
                Text = "Click the buttons or a grid row. Toast and dialogs appear near the cursor.",
                Location = new Point(20, 48),
                Size = new Size(460, 20),
                ForeColor = Color.DarkGray
            };

            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(20, 80),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(0, 128, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;

            _deleteButton = new Button
            {
                Text = "Delete...",
                Location = new Point(152, 80),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(192, 64, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _deleteButton.FlatAppearance.BorderSize = 0;

            var gridLabel = new Label
            {
                Text = "Click any row to send a GridTouch action:",
                Location = new Point(20, 128),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            _grid = new DataGridView
            {
                Location = new Point(20, 152),
                Size = new Size(460, 120),
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
                Text = "Hint: click a button or grid row.",
                Location = new Point(20, 288),
                Size = new Size(460, 56),
                ForeColor = Color.FromArgb(0, 80, 140),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic)
            };

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                titleLabel, infoLabel,
                _saveButton, _deleteButton,
                gridLabel, _grid,
                _hintLabel
            });
        }

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(AnchoredMessageDemoActions.Save, _saveButton);
            _viewActionBinder.Add(AnchoredMessageDemoActions.Delete, _deleteButton);
            _viewActionBinder.Add(AnchoredMessageDemoActions.GridTouch, _grid);
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        public void ShowHint(string message)
        {
            _hintLabel.Text = message;
        }
    }
}
