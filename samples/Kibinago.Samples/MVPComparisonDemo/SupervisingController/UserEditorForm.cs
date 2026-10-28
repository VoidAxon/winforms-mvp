using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.Events;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MVPComparisonDemo.SupervisingController
{
    /// <summary>
    /// Supervising Controller implementation.
    ///
    /// In Supervising Controller:
    /// - Form uses DATA BINDING to connect controls to Model
    /// - When Model properties change, UI updates automatically
    /// - When user edits UI, Model is updated automatically
    /// - Form contains simple binding logic
    /// - Presenter focuses on coordination and complex logic
    /// - LESS CODE than Passive View for simple scenarios
    /// </summary>
    public partial class UserEditorForm : Form, IUserEditorView
    {
        private ViewActionBinder _viewActionBinder;
        private UserModel _model;

        public UserEditorForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        #region IUserEditorView Implementation - Supervising Controller Style

        /// <summary>
        /// The Model property.
        /// In Supervising Controller, when the model is set,
        /// we set up DATA BINDINGS to automatically sync UI with Model.
        /// </summary>
        public UserModel Model
        {
            get => _model;
            set
            {
                _model = value;
                SetupDataBindings();
            }
        }

        /// <summary>
        /// Set up data bindings between UI controls and Model.
        /// This is the KEY difference from Passive View!
        ///
        /// When Model properties change, UI updates automatically.
        /// When user edits UI, Model is updated automatically.
        /// </summary>
        private void SetupDataBindings()
        {
            if (_model == null) return;

            // Clear any existing bindings
            _nameTextBox.DataBindings.Clear();
            _emailTextBox.DataBindings.Clear();
            _ageNumericUpDown.DataBindings.Clear();
            _isActiveCheckBox.DataBindings.Clear();
            _errorLabel.DataBindings.Clear();

            // Bind controls to model properties using extension methods
            // These bindings are BIDIRECTIONAL and AUTOMATIC
            _nameTextBox.Bind(_model, m => m.Name);
            _emailTextBox.Bind(_model, m => m.Email);
            _ageNumericUpDown.Bind(_model, m => (decimal)m.Age);
            _isActiveCheckBox.Bind(_model, m => m.IsActive);

            // Bind error label to model's ValidationErrors property
            // When validation changes, error label updates automatically!
            _errorLabel.BindProperty(_model, m => m.ValidationErrors, nameof(_errorLabel.Text));

            // Show/hide error title based on whether there are errors
            // This demonstrates automatic UI updates based on model state
            _model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_model.ValidationErrors))
                {
                    var hasErrors = !string.IsNullOrEmpty(_model.ValidationErrors);
                    _errorTitleLabel.Visible = hasErrors;
                }
            };

            // Set initial visibility
            _errorTitleLabel.Visible = !string.IsNullOrEmpty(_model.ValidationErrors);
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
