using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows.Forms;
using WinformsMVP.Common.Validation;
using WinformsMVP.Common.Validation.Core;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Validation errors: ValidationState (Core), its connection to the presenter / view, and the
    /// WinForms ErrorProviderDisplay.
    /// </summary>
    public class ValidationDisplayTests
    {
        private sealed class Customer : IValidatableObject
        {
            [Required(ErrorMessage = "Name is required.")]
            public string Name { get; set; }

            [Range(0, 100, ErrorMessage = "Discount must be 0-100.")]
            public int Discount { get; set; }

            public bool Vip { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext context)
            {
                if (Vip && Discount < 10) yield return new ValidationResult("VIP customers get at least 10%.");
            }
        }

        #region ValidationState

        [Fact]
        public void Validate_GroupsModelErrorsByField_AndFormLevel()
        {
            var state = new ValidationState();

            state.Validate(new Customer { Name = null, Discount = 150 });

            Assert.True(state.HasErrors);
            Assert.Equal(new[] { "Name is required." }, state.GetErrors(nameof(Customer.Name)));
            Assert.Equal(new[] { "Discount must be 0-100." }, state.GetErrors(nameof(Customer.Discount)));
            Assert.Empty(state.GetErrors(ValidationState.FormLevel));
        }

        [Fact]
        public void Validate_ClassLevelError_GoesToFormLevel()
        {
            var state = new ValidationState();

            state.Validate(new Customer { Name = "a", Vip = true, Discount = 5 });

            Assert.Equal(new[] { "VIP customers get at least 10%." }, state.GetErrors(ValidationState.FormLevel));
        }

        [Fact]
        public void Validate_ReplacesModelErrors_ButKeepsPresenterErrors()
        {
            var state = new ValidationState();
            state.SetError(nameof(Customer.Name), "Name already taken.");
            state.Validate(new Customer { Name = null });

            state.Validate(new Customer { Name = "fixed" });

            Assert.Equal(new[] { "Name already taken." }, state.GetErrors(nameof(Customer.Name)));
            state.ClearError(nameof(Customer.Name));
            Assert.False(state.HasErrors);
        }

        [Fact]
        public void Check_RevealsAll_AndReturnsValidity()
        {
            var state = new ValidationState();

            Assert.False(state.Check(new Customer { Name = null }));
            Assert.True(state.RevealAll);
            Assert.True(state.Check(new Customer { Name = "ok" }));
        }

        [Fact]
        public void Reset_ClearsErrorsAndRevealAll()
        {
            var state = new ValidationState();
            state.Check(new Customer { Name = null });

            state.Reset();

            Assert.False(state.HasErrors);
            Assert.False(state.RevealAll);
        }

        [Fact]
        public void ErrorsChanged_NotRaised_WhenNothingChanged()
        {
            var state = new ValidationState();
            state.Validate(new Customer { Name = null });
            int raised = 0;
            state.ErrorsChanged += (s, e) => raised++;

            state.Validate(new Customer { Name = null });
            state.SetError("x", "same");
            state.SetError("x", "same");

            Assert.Equal(1, raised);
        }

        #endregion

        #region Presenter connection

        private sealed class RecordingDisplay : IValidationErrorDisplay
        {
            public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors;
            public bool RevealAll;
            public int Calls;

            public void ShowErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, bool revealAll)
            {
                Errors = errors;
                RevealAll = revealAll;
                Calls++;
            }
        }

        public interface ICustomerView : IWindowView, IValidatableView
        {
            string Name { get; set; }
        }

        private sealed class CustomerView : ICustomerView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public RecordingDisplay Display { get; } = new RecordingDisplay();
            public IValidationErrorDisplay ValidationErrors => Display;
            public string Name { get; set; }
        }

        private static readonly ViewAction Save = ViewAction.Create("ValidationDisplay.Save");
        private static readonly ViewAction Edit = ViewAction.Create("ValidationDisplay.Edit");

        private sealed class CustomerPresenter : WindowPresenterBase<ICustomerView>
        {
            public bool Saved;

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Edit, () => Validation.Validate(Read()));
                Dispatcher.Register(Save, () =>
                {
                    if (!Validation.Check(Read())) return;
                    Saved = true;
                });
            }

            private Customer Read() => new Customer { Name = View.Name };
        }

        [Fact]
        public void Presenter_ValidationChanges_AreShownOnTheView()
        {
            var view = new CustomerView();
            PresenterTestHost.Start(new CustomerPresenter(), view);

            view.Binder.Dispatch(Edit);

            Assert.False(view.Display.RevealAll);
            Assert.Contains(nameof(Customer.Name), view.Display.Errors.Keys);
        }

        [Fact]
        public void Presenter_SubmitWithErrors_RevealsAllAndDoesNotSave()
        {
            var view = new CustomerView();
            var presenter = new CustomerPresenter();
            PresenterTestHost.Start(presenter, view);

            view.Binder.Dispatch(Save);

            Assert.False(presenter.Saved);
            Assert.True(view.Display.RevealAll);

            view.Name = "ACME";
            view.Binder.Dispatch(Save);
            Assert.True(presenter.Saved);
            Assert.Empty(view.Display.Errors);
        }

        [Fact]
        public void Presenter_ValidationChange_RefreshesCanExecute()
        {
            var view = new CustomerView();
            PresenterTestHost.Start(new CustomerPresenter(), view);
            int refreshes = 0;
            view.Binder.Dispatcher.CanExecuteChanged += (s, e) => refreshes++;

            view.Binder.Dispatch(Edit);

            Assert.True(refreshes >= 1);
        }

        #endregion

        #region ErrorProviderDisplay

        private static void Leave(Control control)
            => typeof(Control).GetMethod("OnLeave", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(control, new object[] { EventArgs.Empty });

        private static Dictionary<string, IReadOnlyList<string>> Errors(params (string Field, string Message)[] errors)
        {
            var all = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (field, message) in errors) all[field] = new[] { message };
            return all;
        }

        [Fact]
        public void ErrorProvider_ShowsAFieldOnlyAfterItWasVisited()
        {
            using (var provider = new ErrorProvider())
            using (var txtName = new TextBox())
            using (var numDiscount = new NumericUpDown())
            {
                var display = new ErrorProviderDisplay(provider)
                    .Map(nameof(Customer.Name), txtName)
                    .Map(nameof(Customer.Discount), numDiscount);
                var errors = Errors((nameof(Customer.Name), "Name is required."), (nameof(Customer.Discount), "Too high."));

                display.ShowErrors(errors, revealAll: false);
                Assert.Equal("", provider.GetError(txtName));

                Leave(txtName);   // the user tabbed out of Name

                Assert.Equal("Name is required.", provider.GetError(txtName));
                Assert.Equal("", provider.GetError(numDiscount));
            }
        }

        [Fact]
        public void ErrorProvider_RevealAll_ShowsEveryField_AndFormLevelOnItsControl()
        {
            using (var provider = new ErrorProvider())
            using (var txtName = new TextBox())
            using (var btnSave = new Button())
            {
                var display = new ErrorProviderDisplay(provider)
                    .Map(nameof(Customer.Name), txtName)
                    .Map(ValidationState.FormLevel, btnSave);

                display.ShowErrors(Errors(
                    (nameof(Customer.Name), "Name is required."),
                    (ValidationState.FormLevel, "VIP customers get at least 10%."),
                    ("Unmapped", "Shown with the form-level errors.")), revealAll: true);

                Assert.Equal("Name is required.", provider.GetError(txtName));
                Assert.Contains("VIP customers", provider.GetError(btnSave));
                Assert.Contains("Shown with the form-level errors.", provider.GetError(btnSave));
            }
        }

        [Fact]
        public void ErrorProvider_Reset_ForgetsVisitedFields()
        {
            using (var provider = new ErrorProvider())
            using (var txtName = new TextBox())
            {
                var display = new ErrorProviderDisplay(provider).Map(nameof(Customer.Name), txtName);
                var errors = Errors((nameof(Customer.Name), "Name is required."));
                Leave(txtName);
                display.ShowErrors(errors, revealAll: true);

                display.ShowErrors(errors, revealAll: false);   // ValidationState.Reset, then a new error

                Assert.Equal("", provider.GetError(txtName));
            }
        }

        [Fact]
        public void ErrorProvider_MappingAFieldTwice_Throws()
        {
            using (var provider = new ErrorProvider())
            using (var a = new TextBox())
            using (var b = new TextBox())
            {
                var display = new ErrorProviderDisplay(provider).Map("Name", a);
                Assert.Throws<InvalidOperationException>(() => display.Map("Name", b));
            }
        }

        #endregion
    }
}
