using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Samples.FirstErrorDemo;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.Samples
{
    public class IntakePresenterTests
    {
        private sealed class MockIntakeView : IIntakeView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public IntakeModel Model = new IntakeModel();
            public string Focused;
            public IReadOnlyList<(string Field, string Message)> Errors;

            public IntakeModel ReadModel() => Model;
            public void FocusError(string field) => Focused = field;
            public void ShowErrors(IReadOnlyList<(string Field, string Message)> errors) => Errors = errors;
        }

        [Fact]
        public void AnEmptySubmit_ShowsTheFirstErrorInScreenOrder_ThenFocusesIt()
        {
            var fakes = TestServices.CreateFakes();
            var view = new MockIntakeView();
            using (PresenterTestHost.Start(fakes.Provider, new IntakePresenter(), view))
            {
                view.Binder.Dispatch(IntakeActions.Submit);

                Assert.Equal("氏名を入力してください。", fakes.Messages.Calls.Single().Text);
                Assert.Equal(nameof(IntakeModel.Name), view.Focused);
                // The model's declaration order; the form-level rule last.
                Assert.Equal(new[] { "Name", "Kana", "BirthDate", "" }, view.Errors.Select(e => e.Field));
            }
        }

        [Fact]
        public void APresenterRule_TakesItsPropertysPlace()
        {
            var fakes = TestServices.CreateFakes();
            var view = new MockIntakeView
            {
                Model = new IntakeModel { Name = "高橋 健", Kana = "タカハシ ケン", BirthDate = DateTime.Today.AddDays(1), Phone = "03-1234-5678" },
            };
            using (PresenterTestHost.Start(fakes.Provider, new IntakePresenter(), view))
            {
                view.Binder.Dispatch(IntakeActions.Submit);

                Assert.Equal("生年月日が未来の日付です。", fakes.Messages.Calls.Single().Text);
                Assert.Equal(nameof(IntakeModel.BirthDate), view.Focused);
            }
        }

        [Fact]
        public void AValidSubmit_Registers()
        {
            var fakes = TestServices.CreateFakes();
            var view = new MockIntakeView
            {
                Model = new IntakeModel { Name = "高橋 健", Kana = "タカハシ ケン", BirthDate = new DateTime(1985, 4, 12), Email = "ken@example.com" },
            };
            using (PresenterTestHost.Start(fakes.Provider, new IntakePresenter(), view))
            {
                view.Binder.Dispatch(IntakeActions.Submit);

                Assert.Equal(MessageKind.Info, fakes.Messages.Calls.Single().Kind);
                Assert.Null(view.Focused);
                Assert.Empty(view.Errors);
            }
        }
    }
}
