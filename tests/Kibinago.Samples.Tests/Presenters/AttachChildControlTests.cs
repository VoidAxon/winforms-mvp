using System;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Samples.Tests.Composition;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>A child attached to a real control is released when that control is disposed.</summary>
    public class AttachChildControlTests
    {
        private sealed class NamePanelControl : UserControl, INamePanelView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public string Shown { set { } }
        }

        private sealed class ControlEditView : IEditView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public NamePanelControl Panel { get; } = new NamePanelControl();
            public INamePanelView NamePanel => Panel;
            public INamePanelView OtherPanel => null;
        }

        [Fact]
        public void ControlDisposedWhileAttached_ReleasesTheChild()
        {
            var view = new ControlEditView();
            var child = new NamePanelPresenter(new NameSource());
            using (PresenterTestHost.Start(TestServices.Create(),
                       new EditPresenter((p, input) => p.Attach(child, view.NamePanel, input)), view, new EditInput()))
            {
                view.Panel.Dispose();

                Assert.True(child.Disposed);
                Assert.False(view.Panel.Binder.IsBound);
            }
        }

        [Fact]
        public void AlreadyDisposedControl_IsRejected()
        {
            var view = new ControlEditView();
            view.Panel.Dispose();
            var child = new NamePanelPresenter(new NameSource());

            Assert.Throws<ObjectDisposedException>(() => PresenterTestHost.Start(TestServices.Create(),
                new EditPresenter((p, input) => p.Attach(child, view.NamePanel, input)), view, new EditInput()));
            Assert.False(child.Disposed);   // rejected before ownership: the caller still owns it
        }

        [Fact]
        public void AControlHostedWithConnect_CannotAlsoBeAChildView()
        {
            var view = new ControlEditView();
            var connected = new NamePanelPresenter(new NameSource());
            connected.Connect(view.Panel, new EditInput());

            Assert.Throws<InvalidOperationException>(() => PresenterTestHost.Start(TestServices.Create(),
                new EditPresenter((p, input) => p.Attach(new NamePanelPresenter(new NameSource()), view.NamePanel, input)),
                view, new EditInput()));
            Assert.False(connected.Disposed);
        }

        [Fact]
        public void ConnectingAnAttachedChild_IsRejected_AndTheChildStaysAttached()
        {
            var view = new ControlEditView();
            var child = new NamePanelPresenter(new NameSource());
            using (PresenterTestHost.Start(TestServices.Create(),
                       new EditPresenter((p, input) => p.Attach(child, view.NamePanel, input)), view, new EditInput()))
            using (var otherControl = new NamePanelControl())
            {
                Assert.Throws<InvalidOperationException>(() => child.Connect(otherControl, new EditInput()));

                Assert.False(child.Disposed);
                Assert.True(view.Panel.Binder.IsBound);
            }
        }
    }
}
