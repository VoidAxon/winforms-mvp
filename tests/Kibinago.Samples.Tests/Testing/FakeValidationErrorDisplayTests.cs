using System.ComponentModel.DataAnnotations;
using Kibinago.Validation;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakeValidationErrorDisplayTests
    {
        private static readonly ViewAction Submit = ViewAction.Create("FakeValidationDisplayTests.Submit");
        private static readonly ViewAction Clear = ViewAction.Create("FakeValidationDisplayTests.Clear");

        public sealed class Input
        {
            [Required(ErrorMessage = "Name is required.")]
            public string Name { get; set; }

            [Range(0, 120, ErrorMessage = "Age is out of range.")]
            public int Age { get; set; }
        }

        public interface IInputView : IWindowView, IValidatableView
        {
            string InputName { get; set; }
            int Age { get; set; }
        }

        private sealed class FakeInputView : IInputView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public FakeValidationErrorDisplay Errors { get; } = new FakeValidationErrorDisplay();
            public IValidationErrorDisplay ValidationErrors => Errors;
            public string InputName { get; set; }
            public int Age { get; set; }
        }

        private sealed class InputPresenter : WindowPresenterBase<IInputView>
        {
            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Submit, () => Validation.Check(ReadModel()));
                Dispatcher.Register(Clear, () => Validation.Reset());
            }

            private Input ReadModel() => new Input { Name = View.InputName, Age = View.Age };
        }

        [Fact]
        public void BeforeAnything_HasNoErrors()
        {
            var display = new FakeValidationErrorDisplay();

            Assert.Empty(display.Errors);
            Assert.False(display.RevealAll);
            Assert.Equal(0, display.ShowCount);
            Assert.Empty(display.ErrorsFor("Name"));
            Assert.False(display.HasError("Name"));
        }

        [Fact]
        public void FailedSubmit_RecordsEveryErrorRevealed()
        {
            var view = new FakeInputView { InputName = "", Age = 200 };
            using (PresenterTestHost.Start(new InputPresenter(), view))
            {
                view.Binder.Dispatch(Submit);

                Assert.True(view.Errors.RevealAll);
                Assert.True(view.Errors.HasError(nameof(Input.Name)));
                Assert.Equal(new[] { "Age is out of range." }, view.Errors.ErrorsFor(nameof(Input.Age)));
                Assert.True(view.Errors.ShowCount > 0);
            }
        }

        [Fact]
        public void Fixed_ClearsTheField()
        {
            var view = new FakeInputView { InputName = "", Age = 30 };
            using (PresenterTestHost.Start(new InputPresenter(), view))
            {
                view.Binder.Dispatch(Submit);
                view.InputName = "Ann";
                view.Binder.Dispatch(Submit);

                Assert.False(view.Errors.HasError(nameof(Input.Name)));
                Assert.Empty(view.Errors.Errors);
            }
        }

        [Fact]
        public void Reset_IsCounted()
        {
            var view = new FakeInputView { InputName = "", Age = 30 };
            using (PresenterTestHost.Start(new InputPresenter(), view))
            {
                view.Binder.Dispatch(Submit);
                view.Binder.Dispatch(Clear);

                Assert.Equal(1, view.Errors.ResetCount);
                Assert.False(view.Errors.RevealAll);
            }
        }
    }
}
