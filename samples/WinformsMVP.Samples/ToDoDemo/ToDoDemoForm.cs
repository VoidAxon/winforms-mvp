using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ToDoDemo
{
    /// <summary>
    /// Demo form implementing IToDoView.
    ///
    /// IMPORTANT - Proper MVP Design:
    /// - This Form knows about UI elements (Button, TextBox, etc.)
    /// - The IToDoView interface does NOT expose these UI elements
    /// - Presenter interacts only through the interface (no UI knowledge)
    /// - ViewActionBinder is used internally by the Form
    /// - Form is responsible for mapping UI controls to actions
    ///
    /// Shows ViewAction system with CanExecute in action.
    /// </summary>
    public partial class ToDoDemoForm : Form, IToDoView
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

        // ViewActionBinder is internal to the Form
        // Presenter doesn't know about it
        private ViewActionBinder _viewActionBinder;

        // State
        private bool _hasUnsavedChanges;
        private bool _hasSelection;

        public ToDoDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "WinForms MVP - ViewAction Demo";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title Label
            var titleLabel = new Label
            {
                Text = "ToDo List Manager - ViewAction System Demo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(640, 30),
                ForeColor = Color.DarkBlue
            };

            // Info Label
            var infoLabel = new Label
            {
                Text = "Watch buttons enable/disable automatically based on application state!\n" +
                       "• Add Task: Always enabled\n" +
                       "• Delete/Complete: Enabled only when task is selected\n" +
                       "• Save: Enabled only when there are unsaved changes",
                Location = new Point(20, 55),
                Size = new Size(640, 60),
                ForeColor = Color.DarkGray
            };

            // Task Input
            var inputLabel = new Label
            {
                Text = "New Task:",
                Location = new Point(20, 125),
                Size = new Size(80, 20)
            };

            _taskTextBox = new TextBox
            {
                Location = new Point(105, 123),
                Size = new Size(300, 23)
            };

            _addButton = new Button
            {
                Text = "Add Task",
                Location = new Point(415, 120),
                Size = new Size(100, 28),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _addButton.FlatAppearance.BorderSize = 0;

            // Task List
            var listLabel = new Label
            {
                Text = "Task List:",
                Location = new Point(20, 160),
                Size = new Size(80, 20)
            };

            _taskListBox = new ListBox
            {
                Location = new Point(20, 185),
                Size = new Size(495, 180),
                Font = new Font("Segoe UI", 10f)
            };
            _taskListBox.SelectedIndexChanged += (s, e) =>
            {
                OnSelectionChanged();
            };

            // Action Buttons Panel
            var buttonPanel = new Panel
            {
                Location = new Point(525, 185),
                Size = new Size(150, 180)
            };

            _deleteButton = new Button
            {
                Text = "Delete Task",
                Location = new Point(0, 0),
                Size = new Size(140, 35),
                Enabled = false // Will be controlled by CanExecute
            };

            _completeButton = new Button
            {
                Text = "Complete Task",
                Location = new Point(0, 45),
                Size = new Size(140, 35),
                Enabled = false // Will be controlled by CanExecute
            };

            _saveButton = new Button
            {
                Text = "Save All",
                Location = new Point(0, 90),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false // Will be controlled by CanExecute
            };
            _saveButton.FlatAppearance.BorderSize = 0;

            buttonPanel.Controls.AddRange(new Control[] {
                _deleteButton, _completeButton, _saveButton
            });

            // Status Bar
            var statusPanel = new Panel
            {
                Location = new Point(0, 380),
                Size = new Size(700, 90),
                BackColor = Color.FromArgb(240, 240, 240),
                Dock = DockStyle.Bottom
            };

            _statusLabel = new Label
            {
                Text = "Ready. Add some tasks to see CanExecute in action!",
                Location = new Point(20, 15),
                Size = new Size(640, 20),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            _hasChangesCheckBox = new CheckBox
            {
                Text = "Simulate Unsaved Changes (for testing Save button)",
                Location = new Point(20, 45),
                Size = new Size(400, 20),
                Checked = false
            };
            _hasChangesCheckBox.CheckedChanged += (s, e) =>
            {
                _hasUnsavedChanges = _hasChangesCheckBox.Checked;
                OnDataChanged();
            };

            statusPanel.Controls.AddRange(new Control[] {
                _statusLabel, _hasChangesCheckBox
            });

            // Add all controls to form
            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel,
                inputLabel, _taskTextBox, _addButton,
                listLabel, _taskListBox,
                buttonPanel,
                statusPanel
            });
        }

        #region IToDoView Implementation

        // ========================================
        // Data Properties
        // ========================================

        public bool HasSelectedTask => _hasSelection;
        public bool HasPendingChanges => _hasUnsavedChanges;

        public string TaskText
        {
            get => _taskTextBox.Text;
            set => _taskTextBox.Text = value;
        }

        // ========================================
        // ViewAction Integration
        // ========================================

        /// <summary>
        /// Bind UI controls to the ViewActionDispatcher.
        /// This is where the Form maps its buttons to the actions registered by the Presenter.
        /// The Presenter has no knowledge of these buttons - only the Form does.
        /// </summary>
        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            // ========================================
            // CRITICAL: This method should contain ONLY UI binding code!
            //
            // ✅ DO:
            //    - Map controls to actions (_binder.Add(...))
            //    - Bind to dispatcher (_binder.Bind(...))
            //
            // ❌ DO NOT:
            //    - Call database operations
            //    - Execute business logic
            //    - Perform expensive computations
            //    - Make network calls
            //
            // Think of this like WPF's InitializeComponent() - pure UI infrastructure only.
            // ========================================

            // Create the binder
            _viewActionBinder = new ViewActionBinder();

            // Map UI controls to actions
            // The Presenter registered these actions but doesn't know about the buttons
            _viewActionBinder.Add(ToDoDemoActions.AddTask, _addButton);
            _viewActionBinder.Add(ToDoDemoActions.RemoveTask, _deleteButton);
            _viewActionBinder.Add(ToDoDemoActions.CompleteTask, _completeButton);
            _viewActionBinder.Add(ToDoDemoActions.SaveAll, _saveButton);

            // Bind to the dispatcher
            // This enables automatic CanExecute support
        }

        // ========================================
        // View Behaviors
        // ========================================

        public void AddTaskToList(string task)
        {
            _taskListBox.Items.Add(task);
            _hasUnsavedChanges = true;
            _hasChangesCheckBox.Checked = true;
            UpdateStatus($"Task added: {task}");
        }

        public void RemoveSelectedTask()
        {
            if (_taskListBox.SelectedIndex >= 0)
            {
                var task = _taskListBox.SelectedItem.ToString();
                _taskListBox.Items.RemoveAt(_taskListBox.SelectedIndex);
                _hasUnsavedChanges = true;
                _hasChangesCheckBox.Checked = true;
                UpdateStatus($"Task deleted: {task}");
            }
        }

        public void CompleteSelectedTask()
        {
            if (_taskListBox.SelectedIndex >= 0)
            {
                var index = _taskListBox.SelectedIndex;
                var task = _taskListBox.Items[index].ToString();

                // Mark as completed with strikethrough (using unicode)
                if (!task.StartsWith("✓ "))
                {
                    _taskListBox.Items[index] = "✓ " + task;
                    _hasUnsavedChanges = true;
                    _hasChangesCheckBox.Checked = true;
                    UpdateStatus($"Task completed: {task}");
                }
            }
        }

        public void ClearPendingChanges()
        {
            _hasUnsavedChanges = false;
            _hasChangesCheckBox.Checked = false;
            UpdateStatus("All changes saved!");
        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
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
