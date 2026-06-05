using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.BulkBindingDemo
{
    /// <summary>
    /// Demo form showing bulk binding techniques for many RadioButtons.
    /// Demonstrates three approaches:
    /// 1. AddRange with tuples (recommended for moderate number of controls)
    /// 2. AddRange with dictionary (alternative syntax)
    /// 3. AddByTagFromContainer (recommended for many controls in same container)
    /// </summary>
    public partial class SurveyDemoForm : Form, ISurveyView
    {
        // Question 1: 5 RadioButtons (use AddRange with tuples)
        private Panel _q1Panel;
        private RadioButton _q1StronglyDisagree;
        private RadioButton _q1Disagree;
        private RadioButton _q1Neutral;
        private RadioButton _q1Agree;
        private RadioButton _q1StronglyAgree;

        // Question 2: 5 RadioButtons (use AddByTagFromContainer)
        private Panel _q2Panel;
        private RadioButton _q2StronglyDisagree;
        private RadioButton _q2Disagree;
        private RadioButton _q2Neutral;
        private RadioButton _q2Agree;
        private RadioButton _q2StronglyAgree;

        private Button _submitButton;
        private Label _statusLabel;
        private TextBox _resultsTextBox;

        private ViewActionBinder _viewActionBinder;

        public SurveyDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "WinForms MVP - Bulk Binding Demo";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "Survey - RadioButton Bulk Binding Demo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(640, 30),
                ForeColor = Color.DarkBlue
            };

            // Info
            var infoLabel = new Label
            {
                Text = "This demo shows efficient ways to bind many RadioButtons.\n" +
                       "Question 1 uses AddRange(tuples), Question 2 uses AddRange(dictionary).",
                Location = new Point(20, 55),
                Size = new Size(640, 40),
                ForeColor = Color.DarkGray
            };

            // Question 1
            var q1Label = new Label
            {
                Text = "Q1: I find this framework easy to use (AddRange demo)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 110),
                Size = new Size(640, 20)
            };

            _q1Panel = CreateLikertScalePanel(120, out _q1StronglyDisagree, out _q1Disagree,
                out _q1Neutral, out _q1Agree, out _q1StronglyAgree);

            // Question 2
            var q2Label = new Label
            {
                Text = "Q2: Bulk binding saves me time (AddRange with dictionary demo)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 270),
                Size = new Size(640, 20)
            };

            _q2Panel = CreateLikertScalePanel(280, out _q2StronglyDisagree, out _q2Disagree,
                out _q2Neutral, out _q2Agree, out _q2StronglyAgree);

            // Submit Button
            _submitButton = new Button
            {
                Text = "Submit Survey",
                Location = new Point(20, 440),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _submitButton.FlatAppearance.BorderSize = 0;

            // Results
            var resultsLabel = new Label
            {
                Text = "Results:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(200, 445),
                Size = new Size(80, 20)
            };

            _resultsTextBox = new TextBox
            {
                Location = new Point(280, 440),
                Size = new Size(380, 60),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // Status Bar
            var statusPanel = new Panel
            {
                Location = new Point(0, 520),
                Size = new Size(700, 40),
                BackColor = Color.FromArgb(240, 240, 240),
                Dock = DockStyle.Bottom
            };

            _statusLabel = new Label
            {
                Text = "Select answers and see how bulk binding handles many RadioButtons efficiently.",
                Location = new Point(20, 10),
                Size = new Size(640, 20),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            statusPanel.Controls.Add(_statusLabel);

            // Add all controls
            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel,
                q1Label, _q1Panel,
                q2Label, _q2Panel,
                _submitButton, resultsLabel, _resultsTextBox,
                statusPanel
            });
        }

        private Panel CreateLikertScalePanel(int yOffset,
            out RadioButton stronglyDisagree, out RadioButton disagree,
            out RadioButton neutral, out RadioButton agree, out RadioButton stronglyAgree)
        {
            var panel = new Panel
            {
                Location = new Point(40, yOffset + 10),
                Size = new Size(620, 120),
                BorderStyle = BorderStyle.FixedSingle
            };

            stronglyDisagree = new RadioButton
            {
                Text = "Strongly Disagree",
                Location = new Point(10, 10),
                Size = new Size(150, 24)
            };

            disagree = new RadioButton
            {
                Text = "Disagree",
                Location = new Point(10, 35),
                Size = new Size(150, 24)
            };

            neutral = new RadioButton
            {
                Text = "Neutral",
                Location = new Point(10, 60),
                Size = new Size(150, 24)
            };

            agree = new RadioButton
            {
                Text = "Agree",
                Location = new Point(10, 85),
                Size = new Size(150, 24)
            };

            stronglyAgree = new RadioButton
            {
                Text = "Strongly Agree",
                Location = new Point(200, 10),
                Size = new Size(150, 24)
            };

            panel.Controls.AddRange(new Control[] {
                stronglyDisagree, disagree, neutral, agree, stronglyAgree
            });

            return panel;
        }

        #region ISurveyView Implementation

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            // ========================================
            // CRITICAL: This method should contain ONLY UI binding code!
            //
            // ✅ DO:
            //    - Map controls to actions (_binder.Add(...), _binder.AddRange(...))
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

            _viewActionBinder = new ViewActionBinder();

            // ============================================
            // Approach 1: AddRange with tuples
            // Best for clear, explicit binding
            // ============================================
            _viewActionBinder.AddRange(
                (SurveyActions.Q1_StronglyDisagree, _q1StronglyDisagree),
                (SurveyActions.Q1_Disagree, _q1Disagree),
                (SurveyActions.Q1_Neutral, _q1Neutral),
                (SurveyActions.Q1_Agree, _q1Agree),
                (SurveyActions.Q1_StronglyAgree, _q1StronglyAgree)
            );

            // ============================================
            // Approach 2: AddRange with dictionary
            // Alternative syntax, good for complex mappings
            // ============================================
            var q2Mapping = new Dictionary<ViewAction, Component>
            {
                [SurveyActions.Q2_StronglyDisagree] = _q2StronglyDisagree,
                [SurveyActions.Q2_Disagree] = _q2Disagree,
                [SurveyActions.Q2_Neutral] = _q2Neutral,
                [SurveyActions.Q2_Agree] = _q2Agree,
                [SurveyActions.Q2_StronglyAgree] = _q2StronglyAgree
            };
            _viewActionBinder.AddRange(q2Mapping);

            // Submit button
            _viewActionBinder.Add(SurveyActions.Submit, _submitButton);

        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        public void ShowResults(string results)
        {
            _resultsTextBox.Text = results;
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        #endregion
    }
}
