using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Composition
{
    public class AttachChildDynamicTests
    {
        // ---- a list of line panels added and removed at runtime ----

        public sealed class Line
        {
            public int Quantity;
        }

        public static class LineActions
        {
            public static readonly ViewAction<int> SetQuantity = ViewAction.Create<int>("Line.SetQuantity");
            public static readonly ViewAction Slow = ViewAction.Create("Line.Slow");
        }

        public interface ILineView : IActionableView { }

        public sealed class FakeLineView : ILineView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        public interface IOrderView : IWindowView
        {
            ILineView AddLine();
            void RemoveLine(ILineView line);
            event EventHandler AddLineRequested;
            event EventHandler<ILineView> RemoveLineRequested;
        }

        public sealed class FakeOrderView : IOrderView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public List<FakeLineView> Lines { get; } = new List<FakeLineView>();
            public event EventHandler AddLineRequested;
            public event EventHandler<ILineView> RemoveLineRequested;

            public ILineView AddLine()
            {
                var line = new FakeLineView();
                Lines.Add(line);
                return line;
            }

            public void RemoveLine(ILineView line) => Lines.Remove((FakeLineView)line);

            public void RaiseAddLine() => AddLineRequested?.Invoke(this, EventArgs.Empty);
            public void RaiseRemoveLine(FakeLineView line) => RemoveLineRequested?.Invoke(this, line);
        }

        public sealed class LinePresenter : ControlPresenterBase<ILineView, Line>
        {
            private Line _line;
            public bool Disposed;
            public bool LifetimeCancelled;
            public TaskCompletionSource<bool> SlowGate = new TaskCompletionSource<bool>();
            public int SlowFinished;

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                Dispatcher.Register(LineActions.SetQuantity, (int q) => _line.Quantity = q);
                Dispatcher.Register(LineActions.Slow, async ct => { await SlowGate.Task; SlowFinished++; });
            }

            protected override void OnInitialize(Line line)
            {
                _line = line;
                Lifetime.Register(() => LifetimeCancelled = true);
            }

            protected override void Cleanup() => Disposed = true;
        }

        public sealed class OrderPresenter : WindowPresenterBase<IOrderView, List<Line>>
        {
            private List<Line> _lines;
            private readonly Dictionary<ILineView, (Line Line, IDisposable Handle)> _panels
                = new Dictionary<ILineView, (Line, IDisposable)>();
            public readonly List<LinePresenter> Created = new List<LinePresenter>();
            public Action<IDisposable> OnCleanupWith;
            public IDisposable LastHandle;

            protected override void OnViewAttached()
            {
                SubscribeViewEvent(h => View.AddLineRequested += h, h => View.AddLineRequested -= h, OnAdd);
                SubscribeViewEvent<ILineView>(h => View.RemoveLineRequested += h, h => View.RemoveLineRequested -= h, OnRemove);
            }

            protected override void OnInitialize(List<Line> lines) => _lines = lines;

            private void OnAdd()
            {
                var line = new Line();
                _lines.Add(line);
                var view = View.AddLine();
                var presenter = new LinePresenter();
                Created.Add(presenter);
                LastHandle = AttachChild(presenter, view, line);
                _panels[view] = (line, LastHandle);
            }

            private void OnRemove(ILineView view)
            {
                var (line, handle) = _panels[view];
                _lines.Remove(line);
                _panels.Remove(view);
                try { handle.Dispose(); }
                finally { View.RemoveLine(view); }
            }

            protected override void Cleanup() => OnCleanupWith?.Invoke(LastHandle);

            internal int LiveChildCount => _panels.Count;
        }

        private readonly FakeOrderView _view = new FakeOrderView();
        private readonly List<Line> _lines = new List<Line>();
        private readonly OrderPresenter _presenter = new OrderPresenter();

        private PresenterTestHost Start() => PresenterTestHost.Start(TestServices.Create(), _presenter, _view, _lines);

        [Fact]
        public void AddDispatchRemove_EndToEnd()
        {
            using (Start())
            {
                _view.RaiseAddLine();
                _view.RaiseAddLine();
                var second = _view.Lines[1];

                second.Binder.Dispatch(LineActions.SetQuantity, 3);
                Assert.Equal(3, _lines[1].Quantity);

                _view.RaiseRemoveLine(second);

                Assert.Single(_view.Lines);
                Assert.Single(_lines);
                Assert.True(_presenter.Created[1].Disposed);
                Assert.True(_presenter.Created[1].LifetimeCancelled);
                Assert.False(_presenter.Created[0].Disposed);
            }
        }

        [Fact]
        public async Task ARemovedPanel_IsInert_SyncAndAsync()
        {
            using (Start())
            {
                _view.RaiseAddLine();
                var view = _view.Lines[0];
                var line = _lines[0];
                var child = _presenter.Created[0];
                var slow = view.Binder.DispatchAsync(LineActions.Slow);   // still running when removed

                _view.RaiseRemoveLine(view);
                view.Binder.Dispatch(LineActions.SetQuantity, 9);        // a late click on the retained control
                child.SlowGate.SetResult(true);
                await slow;

                Assert.Equal(0, line.Quantity);
                Assert.False(view.Binder.IsBound);
            }
        }

        [Fact]
        public void Handle_DisposeTwice_DoesNothing()
        {
            using (Start())
            {
                _view.RaiseAddLine();
                var handle = _presenter.LastHandle;
                handle.Dispose();
                handle.Dispose();
                Assert.True(_presenter.Created[0].Disposed);
            }
        }

        [Fact]
        public void Handle_DisposedInTheParentsCleanup_ChildDisposedOnce()
        {
            var host = Start();
            _view.RaiseAddLine();
            _presenter.OnCleanupWith = handle => handle.Dispose();

            host.Dispose();

            Assert.True(_presenter.Created[0].Disposed);
        }

        [Fact]
        public void ChildDisposedDirectly_IsNoLongerOwned()
        {
            var host = Start();
            _view.RaiseAddLine();
            var child = _presenter.Created[0];

            child.Dispose();
            child.Disposed = false;
            host.Dispose();

            Assert.False(child.Disposed);   // the parent did not dispose it again
        }

        [Fact]
        public void ManyAddsAndRemoves_LeaveOnlyLiveChildren()
        {
            using (Start())
            {
                for (int i = 0; i < 50; i++)
                {
                    _view.RaiseAddLine();
                    _view.RaiseRemoveLine(_view.Lines[0]);
                }
                Assert.Empty(_view.Lines);
                Assert.Equal(0, _presenter.LiveChildCount);
                Assert.Equal(0, ChildRegistryCount(_presenter));
            }
        }

        [Fact]
        public void AReleasedChild_CannotBeAttachedAgain()
        {
            using (Start())
            {
                _view.RaiseAddLine();
                var released = _presenter.Created[0];
                _presenter.LastHandle.Dispose();

                Assert.Throws<ObjectDisposedException>(() => ReattachForTest(_presenter, released));
            }
        }

        // The registry is private; reflection keeps the presenter's surface unchanged.
        private static int ChildRegistryCount(object presenter)
        {
            var field = typeof(PresenterBase<IOrderView>).GetField("_children",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var list = (System.Collections.ICollection)field.GetValue(presenter);
            return list?.Count ?? 0;
        }

        private static void ReattachForTest(OrderPresenter parent, LinePresenter child)
        {
            var method = typeof(PresenterBase<IOrderView>).GetMethod("AttachChildCore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(ILineView));
            try
            {
                method.Invoke(parent, new object[] { child, new FakeLineView(), (Action)(() => { }) });
            }
            catch (System.Reflection.TargetInvocationException ex) { throw ex.InnerException; }
        }
    }
}
