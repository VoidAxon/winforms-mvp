using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.BulkBindingDemo
{
    /// <summary>
    /// Action keys for Survey demo.
    /// Demonstrates bulk binding for many similar actions.
    /// </summary>
    public static class SurveyActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Survey");
        private static readonly ViewActionFactory Q1Factory = Factory.WithQualifier("Q1");
        private static readonly ViewActionFactory Q2Factory = Factory.WithQualifier("Q2");

        // Question 1 actions
        public static readonly ViewAction Q1_StronglyDisagree = Q1Factory.Create("StronglyDisagree");
        public static readonly ViewAction Q1_Disagree = Q1Factory.Create("Disagree");
        public static readonly ViewAction Q1_Neutral = Q1Factory.Create("Neutral");
        public static readonly ViewAction Q1_Agree = Q1Factory.Create("Agree");
        public static readonly ViewAction Q1_StronglyAgree = Q1Factory.Create("StronglyAgree");

        // Question 2 actions
        public static readonly ViewAction Q2_StronglyDisagree = Q2Factory.Create("StronglyDisagree");
        public static readonly ViewAction Q2_Disagree = Q2Factory.Create("Disagree");
        public static readonly ViewAction Q2_Neutral = Q2Factory.Create("Neutral");
        public static readonly ViewAction Q2_Agree = Q2Factory.Create("Agree");
        public static readonly ViewAction Q2_StronglyAgree = Q2Factory.Create("StronglyAgree");

        public static readonly ViewAction Submit = Factory.Create("Submit");
    }

    /// <summary>
    /// Presenter for Survey demo.
    /// Shows how bulk binding simplifies handling many RadioButtons.
    /// </summary>
    public class SurveyDemoPresenter : WindowPresenterBase<ISurveyView>
    {
        private int _q1Answer = -1;  // -1 = not answered, 0-4 = answer index
        private int _q2Answer = -1;

        protected override void OnViewAttached()
        {
            // Nothing to do
        }

        protected override void RegisterViewActions()
        {
            // Question 1 actions
            _dispatcher.Register(SurveyActions.Q1_StronglyDisagree, () => OnQ1Answer(0, "Strongly Disagree"));
            _dispatcher.Register(SurveyActions.Q1_Disagree, () => OnQ1Answer(1, "Disagree"));
            _dispatcher.Register(SurveyActions.Q1_Neutral, () => OnQ1Answer(2, "Neutral"));
            _dispatcher.Register(SurveyActions.Q1_Agree, () => OnQ1Answer(3, "Agree"));
            _dispatcher.Register(SurveyActions.Q1_StronglyAgree, () => OnQ1Answer(4, "Strongly Agree"));

            // Question 2 actions
            _dispatcher.Register(SurveyActions.Q2_StronglyDisagree, () => OnQ2Answer(0, "Strongly Disagree"));
            _dispatcher.Register(SurveyActions.Q2_Disagree, () => OnQ2Answer(1, "Disagree"));
            _dispatcher.Register(SurveyActions.Q2_Neutral, () => OnQ2Answer(2, "Neutral"));
            _dispatcher.Register(SurveyActions.Q2_Agree, () => OnQ2Answer(3, "Agree"));
            _dispatcher.Register(SurveyActions.Q2_StronglyAgree, () => OnQ2Answer(4, "Strongly Agree"));

            // Submit action
            _dispatcher.Register(SurveyActions.Submit, OnSubmit);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.UpdateStatus("Please answer the survey questions. Notice how bulk binding makes the code cleaner!");
        }

        private void OnQ1Answer(int answerIndex, string answerText)
        {
            _q1Answer = answerIndex;
            View.UpdateStatus($"Q1 answered: {answerText} (bound via AddRange)");
            UpdateResults();
        }

        private void OnQ2Answer(int answerIndex, string answerText)
        {
            _q2Answer = answerIndex;
            View.UpdateStatus($"Q2 answered: {answerText} (bound via AddByTagFromContainer)");
            UpdateResults();
        }

        private void OnSubmit()
        {
            if (_q1Answer < 0 || _q2Answer < 0)
            {
                Messages.ShowWarning("Please answer all questions before submitting.", "Incomplete Survey");
                return;
            }

            var message = $"Survey submitted!\n\n" +
                         $"Q1: {GetAnswerText(_q1Answer)}\n" +
                         $"Q2: {GetAnswerText(_q2Answer)}\n\n" +
                         $"Thank you for your feedback!";

            Messages.ShowInfo(message, "Survey Submitted");

            View.UpdateStatus("Survey submitted successfully!");
        }

        private void UpdateResults()
        {
            var results = $"Q1: {(_q1Answer >= 0 ? GetAnswerText(_q1Answer) : "Not answered")}\n" +
                         $"Q2: {(_q2Answer >= 0 ? GetAnswerText(_q2Answer) : "Not answered")}";
            View.ShowResults(results);
        }

        private string GetAnswerText(int index)
        {
            switch (index)
            {
                case 0: return "Strongly Disagree";
                case 1: return "Disagree";
                case 2: return "Neutral";
                case 3: return "Agree";
                case 4: return "Strongly Agree";
                default: return "Unknown";
            }
        }
    }
}
