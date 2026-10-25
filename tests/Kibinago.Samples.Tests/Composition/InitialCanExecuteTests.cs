using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>
    /// The binder is bound before OnInitialize, so control states computed then predate the data the
    /// presenter loads. The framework refreshes them after OnInitialize and after OnInitializeAsync.
    /// </summary>
    public class InitialCanExecuteTests
    {
        private static readonly ViewAction Delete = ViewAction.Create("InitialCanExecute.Delete");

        /// <summary>Records the Enabled state a real binder would give the bound controls.</summary>
        private sealed class StateRecordingBinder : IViewActionBinder
        {
            private ViewActionDispatcher _dispatcher;
            public readonly List<bool> States = new List<bool>();
            public void Bind(ViewActionDispatcher dispatcher)
            {
                _dispatcher = dispatcher;
                _dispatcher.CanExecuteChanged += (s, e) => Record();
                Record();
            }
            public void Unbind() => _dispatcher = null;
            private void Record() { if (_dispatcher != null) States.Add(_dispatcher.CanDispatch(Delete)); }
            public bool Current => States[States.Count - 1];
        }

        public interface IListView : IWindowView { }

        private sealed class ListView : IListView
        {
            public StateRecordingBinder Binder { get; } = new StateRecordingBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        private sealed class ListPresenter : WindowPresenterBase<IListView>
        {
            private bool _hasSelection;
            public TaskCompletionSource<bool> Load;
            protected override void OnViewAttached() { }
            protected override void RegisterViewActions() => Dispatcher.Register(Delete, () => { }, canExecute: () => _hasSelection);
            protected override void OnInitialize() { if (Load == null) _hasSelection = true; }   // data shown, first row selected
            protected override async Task OnInitializeAsync(System.Threading.CancellationToken ct)
            {
                if (Load == null) return;
                await Load.Task;
                _hasSelection = true;
            }
        }

        [Fact]
        public void AfterOnInitialize_ControlStatesReflectTheLoadedData()
        {
            var view = new ListView();
            using (PresenterTestHost.Start(TestServices.Create(), new ListPresenter(), view))
            {
                Assert.False(view.Binder.States[0]);   // at Bind, before OnInitialize
                Assert.True(view.Binder.Current);      // refreshed after OnInitialize
            }
        }

        [Fact]
        public async Task AfterOnInitializeAsync_ControlStatesAreRefreshedAgain()
        {
            var view = new ListView();
            var presenter = new ListPresenter { Load = new TaskCompletionSource<bool>() };
            using (var host = PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                Assert.False(view.Binder.Current);
                presenter.Load.SetResult(true);
                await host.Initialized;
                Assert.True(view.Binder.Current);
            }
        }
    }
}
