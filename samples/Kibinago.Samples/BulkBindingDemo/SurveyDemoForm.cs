using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Events;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.BulkBindingDemo
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
        private ViewActionBinder _viewActionBinder;

        public SurveyDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
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

    }
}
