using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// The owner of a window opened by Navigator may be a window's view or the view of a panel in a window.
    /// WinForms makes the top-level form the owner (Form.Show / ShowDialog take a control's TopLevelControl):
    /// the panel's form, or the MDI parent for anything inside an MDI child.
    /// </summary>
    [Collection("OpenForms")]   // shows forms
    public class WindowNavigatorOwnerTests
    {
        public interface IOwnedView : IWindowView { }

        public interface IPanelView : IView { }

        private sealed class OwnedForm : Form, IOwnedView
        {
            public OwnedForm() => OffScreen(this);
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class OwnedPresenter : WindowPresenterBase<IOwnedView> { }

        private sealed class Panel : UserControl, IPanelView { }

        private sealed class ChildForm : Form, IWindowView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class NotAControl : IPanelView { }

        private static WindowNavigator Navigator()
        {
            var views = new ViewMappingRegister();
            views.Register<IOwnedView, OwnedForm>();
            return new WindowNavigator(views);
        }

        private static T OffScreen<T>(T form) where T : Form
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-3000, -3000);
            form.ShowInTaskbar = false;
            return form;
        }

        private static Form Open(IView owner)
        {
            var window = (Form)Navigator().ShowWindow(new OwnedPresenter(), owner: owner);
            Assert.NotNull(window);
            return window;
        }

        [Fact]
        public void APanelsView_MakesItsFormTheOwner()
        {
            using (var host = OffScreen(new Form()))
            {
                var panel = new Panel();
                host.Controls.Add(panel);
                host.Show();

                var window = Open(panel);

                Assert.Same(host, window.Owner);
                window.Close();
            }
        }

        [Fact]
        public void InsideAnMdiChild_TheMdiParentIsTheOwner()
        {
            using (var parent = OffScreen(new Form { IsMdiContainer = true }))
            {
                parent.Show();
                var child = new ChildForm { MdiParent = parent };
                var panel = new Panel();
                child.Controls.Add(panel);
                child.Show();

                var fromPanel = Open(panel);
                var fromChild = Open(child);   // the child window's own view: the same, already before this change

                Assert.Same(parent, fromPanel.Owner);
                Assert.Same(parent, fromChild.Owner);
                fromPanel.Close();
                fromChild.Close();
            }
        }

        [Fact]
        public void APanelInNoWindowYet_OpensWithoutAnOwner()
        {
            using (var panel = new Panel())
            {
                var window = Open(panel);

                Assert.Null(window.Owner);
                window.Close();
            }
        }

        [Fact]
        public void AViewThatIsNoWinFormsControl_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => Navigator().ShowWindow(new OwnedPresenter(), owner: new NotAControl()));
        }
    }
}
