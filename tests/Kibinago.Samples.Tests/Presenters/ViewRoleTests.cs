using System;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.Tests.Composition;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// A view is a window or a control. The presenter bases' constraints check the view interface at
    /// compile time; these runtime checks catch what the compiler cannot — a type implementing both roles,
    /// a Form behind a control view — before anything is attached.
    /// </summary>
    public class ViewRoleTests
    {
#pragma warning disable MVP019 // deliberately both roles: the runtime check is under test
        public interface IBothView : IWindowView, IControlView { }
#pragma warning restore MVP019

        private sealed class BothView : IBothView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class BothPresenter : ControlPresenterBase<IBothView> { }

        public interface IPanelView : IControlView { }

        // A Form behind a control view: compiles (the interface is a control view), rejected at run time.
        private sealed class PanelForm : Form, IPanelView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class PanelUserControl : UserControl, IPanelView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class PanelPresenter : ControlPresenterBase<IPanelView> { }

        public sealed class FormBackedNamePanel : Form, INamePanelView
        {
            public IViewActionBinder ActionBinder => null;
            string INamePanelView.Shown { set { } }   // explicit: Form has a Shown event
        }

        [Fact]
        public void AViewOfBothRoles_IsRejected_BeforeItIsAttached()
        {
            var presenter = new BothPresenter();

            var ex = Assert.Throws<InvalidOperationException>(() => PresenterTestHost.Start(TestServices.Create(), presenter, new BothView()));

            Assert.Contains("both a window view", ex.Message);
        }

        [Fact]
        public void AFormBehindAControlView_IsRejectedByConnect_AndThePresenterStaysUnattached()
        {
            using (var form = new PanelForm())
            using (var control = new PanelUserControl())
            {
                var presenter = new PanelPresenter();

                var ex = Assert.Throws<ArgumentException>(() => presenter.Connect(form));
                Assert.Contains("is a Form", ex.Message);

                presenter.Connect(control);   // the same presenter still connects to a real control
            }
        }

        [Fact]
        public void AFormBehindAChildView_IsRejectedByAttachChild_AndTheChildIsNotOwned()
        {
            var view = new FakeEditView();
            var input = new EditInput();
            using (var form = new FormBackedNamePanel())
            using (PresenterTestHost.Start(TestServices.Create(), new EditPresenter((parent, i) =>
            {
                var child = new NamePanelPresenter(new NameSource());
                Assert.Throws<ArgumentException>(() => parent.Attach(child, form, i));
                Assert.Empty(parent.Handles);
                Assert.False(child.Disposed);           // not owned: the rejection happened before anything else
                parent.Attach(child, view.NamePanel, i);   // and it can still be attached to a real panel
            }), view, input))
            {
                Assert.Equal(input.Name, view.NamePanel.Displayed);
            }
        }
    }
}
