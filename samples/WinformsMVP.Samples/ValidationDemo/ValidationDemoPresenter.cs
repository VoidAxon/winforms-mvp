using System;
using System.Linq;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ValidationDemo
{
    /// <summary>
    /// Reference example of presenter-side validation:
    /// - The rules live on <see cref="RegistrationModel"/> as DataAnnotations.
    /// - On every edit the presenter re-runs <c>Validation.Validate(model)</c>; the view shows a
    ///   field's errors once the user has left that field.
    /// - Submit calls <c>Validation.Check(model)</c>, which reveals every error (including fields
    ///   the user never visited). Submit is therefore never disabled by validation errors.
    /// - Business rules the model cannot express (here: reserved user names) are added with
    ///   <c>Validation.SetError</c> / <c>ClearError</c> and are shown the same way.
    /// The presenter never touches controls or error widgets.
    /// </summary>
    public class ValidationDemoPresenter : WindowPresenterBase<IValidationDemoView>
    {
        private static readonly string[] ReservedUserNames = { "admin", "administrator", "root", "system" };

        protected override void OnViewAttached()
        {
            EventHandler onDataChanged = (s, e) => Revalidate();
            View.DataChanged += onDataChanged;
            Disposable.Create(() => View.DataChanged -= onDataChanged).DisposeWith(Disposables);
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ValidationActions.Submit, OnSubmit);
            Dispatcher.Register(ValidationActions.Reset, OnReset);
            Dispatcher.Register(ValidationActions.ValidateAll, OnValidateAll);
        }

        protected override void OnInitialize()
        {
            // Errors exist from the start, so tabbing through an empty required field flags it.
            Revalidate();
        }

        private void OnSubmit()
        {
            var model = ReadModel();
            CheckReservedUserName(model);
            if (!Validation.Check(model))
            {
                Messages.ShowWarning("Please correct the validation errors before submitting.", "Validation Failed");
                return;
            }

            Messages.ShowInfo(
                $"Form submitted successfully!\n\n" +
                $"User Name: {model.UserName}\n" +
                $"Email: {model.Email}\n" +
                $"Age: {model.Age}\n" +
                $"Phone: {model.PhoneNumber}",
                "Success");

            OnReset();
        }

        private void OnReset()
        {
            View.UserName = string.Empty;
            View.Email = string.Empty;
            View.Password = string.Empty;
            View.ConfirmPassword = string.Empty;
            View.Age = 0;
            View.PhoneNumber = string.Empty;

            Validation.Reset();   // forget errors and go back to "show visited fields only"
            Revalidate();
        }

        private void OnValidateAll()
        {
            var model = ReadModel();
            CheckReservedUserName(model);
            if (Validation.Check(model))
            {
                Messages.ShowInfo("All fields are valid!", "Validation Results");
                return;
            }

            var count = Validation.Errors.Values.Sum(errors => errors.Count);
            Messages.ShowWarning($"Found {count} validation error(s).", "Validation Results");
        }

        private void Revalidate()
        {
            var model = ReadModel();
            CheckReservedUserName(model);
            Validation.Validate(model);
        }

        // A presenter rule (it could equally be a repository lookup): combined with the model's
        // annotations under the same field name.
        private void CheckReservedUserName(RegistrationModel model)
        {
            var name = model.UserName?.Trim() ?? string.Empty;
            if (ReservedUserNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                Validation.SetError(nameof(RegistrationModel.UserName), $"The user name '{name}' is reserved.");
            else
                Validation.ClearError(nameof(RegistrationModel.UserName));
        }

        private RegistrationModel ReadModel() => new RegistrationModel
        {
            UserName = View.UserName,
            Email = View.Email,
            Password = View.Password,
            ConfirmPassword = View.ConfirmPassword,
            Age = View.Age > 0 ? View.Age : (int?)null,
            PhoneNumber = View.PhoneNumber
        };
    }

    public static class ValidationActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Validation");

        public static readonly ViewAction Submit = Factory.Create("Submit");
        public static readonly ViewAction Reset = Factory.Create("Reset");
        public static readonly ViewAction ValidateAll = Factory.Create("ValidateAll");
    }
}
