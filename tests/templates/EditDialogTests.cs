// Copied by verify-templates.sh into the generated test project: drives the presenter that
// `dotnet new mvp-window -n CustomerEdit --edit` generated, with no Form.
using System;
using Kibinago.Common;
using Kibinago.Validation;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace DemoApp.Tests
{
    public class EditDialogTests
    {
        private sealed class FakeCustomerEditView : ICustomerEditView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public FakeValidationErrorDisplay Errors { get; } = new FakeValidationErrorDisplay();
            public IValidationErrorDisplay ValidationErrors => Errors;
            public string Title { get; set; }
            public string FocusedField { get; private set; }
            public event EventHandler DataChanged;
            public void FocusError(string field) => FocusedField = field;
            public void Type(string title) { Title = title; DataChanged?.Invoke(this, EventArgs.Empty); }
        }

        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeCustomerEditView _view = new FakeCustomerEditView();

        private PresenterTestHost Open(string title)
            => PresenterTestHost.Start(_fakes.Provider, new CustomerEditPresenter(),
                _view, new CustomerEditModel { Title = title });

        [Fact]
        public void Opens_ShowingTheModel()
        {
            using (Open("Ada"))
                Assert.Equal("Ada", _view.Title);
        }

        [Fact]
        public void Ok_ClosesWithTheEditedModel()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("Grace");

                _view.Binder.Dispatch(CustomerEditActions.Ok);

                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
                Assert.Equal("Grace", Assert.IsType<CustomerEditModel>(host.CloseResult).Title);
                Assert.Empty(_fakes.Messages.Calls);   // saved: closing does not ask
            }
        }

        [Fact]
        public void Ok_Invalid_ShowsTheFirstErrorFocusesItAndStaysOpen()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("");

                _view.Binder.Dispatch(CustomerEditActions.Ok);

                Assert.Equal(MessageKind.Warning, Assert.Single(_fakes.Messages.Calls).Kind);
                Assert.Equal(nameof(CustomerEditModel.Title), _view.FocusedField);
                Assert.True(_view.Errors.HasError(nameof(CustomerEditModel.Title)));
                Assert.False(host.CloseRequested);
            }
        }

        [Fact]
        public void Cancel_Unchanged_ClosesWithoutAsking()
        {
            using (var host = Open("Ada"))
            {
                _view.Binder.Dispatch(CustomerEditActions.Cancel);

                Assert.Equal(InteractionStatus.Cancel, host.CloseStatus);
                Assert.Empty(_fakes.Messages.Calls);
            }
        }

        [Fact]
        public void Cancel_Changed_AnsweredCancel_StaysOpen()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("Grace");
                _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.Cancel);

                _view.Binder.Dispatch(CustomerEditActions.Cancel);

                Assert.False(host.CloseRequested);
            }
        }

        [Fact]
        public void Close_Changed_AnsweredNo_ClosesWithoutTheEdit()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("Grace");
                _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.No);

                Assert.True(host.CanClose());   // X / Esc: allowed
                Assert.False(host.CloseRequested);   // no result: the caller gets Cancel
            }
        }

        [Fact]
        public void Close_Changed_AnsweredYes_ClosesWithTheEditedModel()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("Grace");
                _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.Yes);

                Assert.True(host.CanClose());
                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
                Assert.Equal("Grace", Assert.IsType<CustomerEditModel>(host.CloseResult).Title);
            }
        }

        [Fact]
        public void Close_ChangedToInvalid_AnsweredYes_ShowsTheErrorAndStaysOpen()
        {
            using (var host = Open("Ada"))
            {
                _view.Type("");
                _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.Yes);

                Assert.False(host.CanClose());
                Assert.Equal(nameof(CustomerEditModel.Title), _view.FocusedField);
                Assert.False(host.CloseRequested);
            }
        }
    }
}
