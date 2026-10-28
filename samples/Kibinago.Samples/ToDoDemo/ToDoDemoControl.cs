using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ToDoDemo
{
    /// <summary>
    /// The ToDo demo's view: a UserControl implementing IToDoView, shown in a tab of the sample's
    /// main window (whose presenter attaches ToDoDemoPresenter to it with AttachChild).
    ///
    /// IMPORTANT - Proper MVP Design:
    /// - This control knows about UI elements (Button, TextBox, etc.)
    /// - The IToDoView interface does NOT expose these UI elements
    /// - Presenter interacts only through the interface (no UI knowledge)
    /// - ViewActionBinder is used internally by the control, which maps its buttons to actions
    /// </summary>
    public class ToDoDemoControl : UserControl, IToDoView
    {
        // UI Controls (private - not exposed through interface)
        private ListBox _taskListBox;
        private TextBox _taskTextBox;
        private Button _addButton;
        private Button _deleteButton;
        private Button _completeButton;
        private Button _saveButton;
        private Label _statusLabel;
        private CheckBox _hasChangesCheckBox;

        // ViewActionBinder is internal to the control; the presenter doesn't know about it
        private readonly ViewActionBinder _viewActionBinder = new ViewActionBinder();

        // State
        private bool _hasUnsavedChanges;
        private bool _hasSelection;

        public ToDoDemoControl()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Font = new Font("Yu Gothic UI", 9.75f);
            Padding = new Padding(16);
            BackColor = Color.White;

            var infoLabel = new Label
            {
                Text = "ボタンの有効・無効は状態に合わせて自動で切り替わります (CanExecute)。\n" +
                       "・追加: いつでも使える\n" +
                       "・削除 / 完了: タスクを選択しているときだけ\n" +
                       "・保存: 未保存の変更があるときだけ",
                Dock = DockStyle.Top,
                Height = 84,
                ForeColor = Color.DimGray,
            };

            var inputRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
            var inputLabel = new Label { Text = "新しいタスク:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _taskTextBox = new TextBox { Width = 320, Margin = new Padding(0, 4, 8, 0) };
            _addButton = new Button
            {
                Text = "追加",
                Size = new Size(96, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            _addButton.FlatAppearance.BorderSize = 0;
            inputRow.Controls.AddRange(new Control[] { inputLabel, _taskTextBox, _addButton });

            var buttonColumn = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 160,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(12, 0, 0, 0),
            };
            _deleteButton = new Button { Text = "削除", Size = new Size(140, 34), Enabled = false };      // Enabled follows CanExecute
            _completeButton = new Button { Text = "完了にする", Size = new Size(140, 34), Enabled = false };
            _saveButton = new Button
            {
                Text = "すべて保存",
                Size = new Size(140, 34),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            buttonColumn.Controls.AddRange(new Control[] { _deleteButton, _completeButton, _saveButton });

            _taskListBox = new ListBox { Dock = DockStyle.Fill, Font = new Font("Yu Gothic UI", 10.5f), IntegralHeight = false };
            _taskListBox.SelectedIndexChanged += (s, e) => OnSelectionChanged();

            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.FromArgb(243, 243, 243) };
            _statusLabel = new Label { Location = new Point(12, 10), AutoSize = true, ForeColor = Color.FromArgb(64, 64, 64) };
            _hasChangesCheckBox = new CheckBox
            {
                Text = "未保存の変更があることにする (保存ボタンの確認用)",
                Location = new Point(12, 34),
                AutoSize = true,
            };
            _hasChangesCheckBox.CheckedChanged += (s, e) =>
            {
                _hasUnsavedChanges = _hasChangesCheckBox.Checked;
                OnDataChanged();
            };
            statusPanel.Controls.AddRange(new Control[] { _statusLabel, _hasChangesCheckBox });

            // Docking order: Fill first, then the edges from the inside out.
            Controls.Add(_taskListBox);
            Controls.Add(buttonColumn);
            Controls.Add(statusPanel);
            Controls.Add(inputRow);
            Controls.Add(infoLabel);
        }

        #region IToDoView Implementation

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasSelectedTask => _hasSelection;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasPendingChanges => _hasUnsavedChanges;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TaskText
        {
            get => _taskTextBox.Text;
            set => _taskTextBox.Text = value;
        }

        /// <summary>
        /// The framework binds this to the presenter's dispatcher after RegisterViewActions.
        /// The presenter has no knowledge of the buttons below - only the control does.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _viewActionBinder;

        // Only UI binding code here: map controls to actions. No business logic.
        private void InitializeActionBindings()
        {
            _viewActionBinder.Add(ToDoDemoActions.AddTask, _addButton);
            _viewActionBinder.Add(ToDoDemoActions.RemoveTask, _deleteButton);
            _viewActionBinder.Add(ToDoDemoActions.CompleteTask, _completeButton);
            _viewActionBinder.Add(ToDoDemoActions.SaveAll, _saveButton);
        }

        public void AddTaskToList(string task)
        {
            _taskListBox.Items.Add(task);
            _hasUnsavedChanges = true;
            _hasChangesCheckBox.Checked = true;
            UpdateStatus($"追加しました: {task}");
        }

        public void RemoveSelectedTask()
        {
            if (_taskListBox.SelectedIndex >= 0)
            {
                var task = _taskListBox.SelectedItem.ToString();
                _taskListBox.Items.RemoveAt(_taskListBox.SelectedIndex);
                _hasUnsavedChanges = true;
                _hasChangesCheckBox.Checked = true;
                UpdateStatus($"削除しました: {task}");
            }
        }

        public void CompleteSelectedTask()
        {
            if (_taskListBox.SelectedIndex >= 0)
            {
                var index = _taskListBox.SelectedIndex;
                var task = _taskListBox.Items[index].ToString();

                if (!task.StartsWith("✓ "))
                {
                    _taskListBox.Items[index] = "✓ " + task;
                    _hasUnsavedChanges = true;
                    _hasChangesCheckBox.Checked = true;
                    UpdateStatus($"完了にしました: {task}");
                }
            }
        }

        public void ClearPendingChanges()
        {
            _hasUnsavedChanges = false;
            _hasChangesCheckBox.Checked = false;
            UpdateStatus("すべて保存しました。");
        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        #endregion

        #region Events for State Changes

        public event EventHandler SelectionChanged;
        public event EventHandler DataChanged;

        private void OnSelectionChanged()
        {
            _hasSelection = _taskListBox.SelectedIndex >= 0;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
