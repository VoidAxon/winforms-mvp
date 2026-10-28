using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MVPComparisonDemo.PassiveView
{
    /// <summary>
    /// Passive View implementation.
    ///
    /// In Passive View:
    /// - Form is COMPLETELY passive - just a dumb container
    /// - NO data binding
    /// - NO business logic in the form
    /// - Properties simply get/set TextBox.Text, CheckBox.Checked, etc.
    /// - Presenter controls everything explicitly
    /// </summary>
    public partial class UserEditorForm : Form, IUserEditorView
    {
        private ViewActionBinder _viewActionBinder;

        public UserEditorForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        #region IUserEditorView Implementation - Passive View Style

        // In Passive View, these properties just expose the control values
        // Presenter reads/writes them explicitly - NO data binding

        public string UserName
        {
            get => _nameTextBox.Text;
            set => _nameTextBox.Text = value;
        }

        public string Email
        {
            get => _emailTextBox.Text;
            set => _emailTextBox.Text = value;
        }

        public int Age
        {
            get => (int)_ageNumericUpDown.Value;
            set => _ageNumericUpDown.Value = value;
        }

        public bool IsActive
        {
            get => _isActiveCheckBox.Checked;
            set => _isActiveCheckBox.Checked = value;
        }

        public void ShowValidationErrors(string errors)
        {
            _errorLabel.Text = errors;
            _errorLabel.Visible = true;
        }

        public void ClearValidationErrors()
        {
            _errorLabel.Text = "";
            _errorLabel.Visible = false;
        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(UserEditorActions.Save, _saveButton);
            _viewActionBinder.Add(UserEditorActions.Reset, _resetButton);
            _viewActionBinder.Add(UserEditorActions.Close, _closeButton);
        }

        #endregion

    }
}
