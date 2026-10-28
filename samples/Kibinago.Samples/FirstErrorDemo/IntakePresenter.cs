using System;
using Kibinago.Presenters;
using Kibinago.Validation;
using Kibinago.ViewActions;

namespace Kibinago.Samples.FirstErrorDemo
{
    public static class IntakeActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Intake");

        public static readonly ViewAction Submit = Factory.Create(nameof(Submit));
    }

    /// <summary>
    /// A failed submit: the first error in the screen's order as a message, then the focus on its field.
    /// The order comes from the model (declaration order); presenter rules (SetError) take their
    /// property's place; form-level errors come last.
    /// </summary>
    public class IntakePresenter : ControlPresenterBase<IIntakeView>
    {
        protected override void RegisterViewActions() =>
            Dispatcher.Register(IntakeActions.Submit, OnSubmit);   // never disabled by errors: it reveals them

        private void OnSubmit()
        {
            var model = View.ReadModel();
            ApplyRules(model);

            var valid = Validation.Check(model);
            View.ShowErrors(Validation.ErrorsInOrder());
            if (!valid)
            {
                var first = Validation.FirstError;
                Messages.ShowWarning(first.Message, "入力内容を確認してください");
                View.FocusError(first.Field);   // after the message, never from ShowErrors
                return;
            }

            Messages.ShowInfo($"{model.Name} さんの受付を登録しました。", "登録");
        }

        // Rules DataAnnotations cannot express: a presenter rule on a property, and a form-level one.
        private void ApplyRules(IntakeModel model)
        {
            if (model.BirthDate > DateTime.Today)
                Validation.SetError(nameof(IntakeModel.BirthDate), "生年月日が未来の日付です。");
            else
                Validation.ClearError(nameof(IntakeModel.BirthDate));

            if (string.IsNullOrWhiteSpace(model.Phone) && string.IsNullOrWhiteSpace(model.Email))
                Validation.SetError(ValidationState.FormLevel, "電話番号かメールアドレスのどちらかを入力してください。");
            else
                Validation.ClearError(ValidationState.FormLevel);
        }
    }
}
