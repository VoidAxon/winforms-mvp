using System.Linq;
using Kibinago.Common;
using Kibinago.ViewActions;
using Kibinago.Samples.LegacyPopupDemo;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// A legacy (non-MVP) popup opened from a semantic view method: the test answers the method with
    /// an InteractionResult, as the Form would after ShowDialog — no Form, no event args, no service.
    /// </summary>
    public class LegacyPopupDemoPresenterTests
    {
        private sealed class StubView : ILegacyPopupDemoView
        {
            public InteractionResult<CustomerData> EditAnswer = InteractionResult<CustomerData>.Cancel();
            public CustomerData EditedFrom;
            public CustomerData Shown;
            public string Status;

            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;

            public InteractionResult<CustomerData> EditCustomer(CustomerData current) { EditedFrom = current; return EditAnswer; }
            public void ShowCustomer(CustomerData customer) => Shown = customer;
            public void ShowStatus(string message) => Status = message;
        }

        private readonly FakeServices _services = TestServices.CreateFakes();
        private readonly StubView _view = new StubView();

        private PresenterTestHost Start() => PresenterTestHost.Start(_services.Provider, new LegacyPopupDemoPresenter(), _view);

        private static CustomerData Customer(string name) => new CustomerData { Name = name, Email = name + "@example.com", Age = 30 };

        [Fact]
        public void Edit_Ok_KeepsAndShowsTheEditedCustomer()
        {
            using (Start())
            {
                var edited = Customer("Hanako");
                _view.EditAnswer = InteractionResult<CustomerData>.Ok(edited);

                _view.Binder.Dispatch(LegacyPopupDemoActions.EditCustomer);

                Assert.Same(edited, _view.Shown);
                Assert.Equal("Customer updated: Hanako", _view.Status);
            }
        }

        [Fact]
        public void Edit_PassesTheCurrentCustomer_SoTheLegacyFormStartsFromIt()
        {
            using (Start())
            {
                _view.EditAnswer = InteractionResult<CustomerData>.Ok(Customer("Hanako"));
                _view.Binder.Dispatch(LegacyPopupDemoActions.EditCustomer);
                _view.EditAnswer = InteractionResult<CustomerData>.Cancel();

                _view.Binder.Dispatch(LegacyPopupDemoActions.EditCustomer);

                Assert.Equal("Hanako", _view.EditedFrom.Name);
            }
        }

        [Fact]
        public void Edit_Cancelled_KeepsTheCustomer()
        {
            using (Start())
            {
                _view.Binder.Dispatch(LegacyPopupDemoActions.EditCustomer);

                Assert.Null(_view.Shown);
                Assert.Equal("Edit cancelled", _view.Status);
            }
        }

        [Fact]
        public void Edit_Error_ShowsTheMessage()
        {
            using (Start())
            {
                _view.EditAnswer = InteractionResult<CustomerData>.Error("The legacy editor failed.");

                _view.Binder.Dispatch(LegacyPopupDemoActions.EditCustomer);

                Assert.Contains(_services.Messages.Calls, c => c.Kind == MessageKind.Error);
                Assert.Null(_view.Shown);
            }
        }
    }
}
