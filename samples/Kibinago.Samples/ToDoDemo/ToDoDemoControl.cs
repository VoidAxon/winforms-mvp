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
    public partial class ToDoDemoControl : UserControl, IToDoView
    {
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
