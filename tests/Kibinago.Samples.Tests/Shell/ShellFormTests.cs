using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.Shell;
using Kibinago.Samples.ToDoDemo;
using Xunit;

namespace Kibinago.Samples.Tests.Shell
{
    /// <summary>
    /// The real main window, shown off screen. In the "OpenForms" collection: a shown form is in the
    /// process-wide Application.OpenForms, which other tests count.
    /// </summary>
    [Collection("OpenForms")]
    public class ShellFormTests
    {
        [Fact]
        public void TheRealShell_ShowsEveryCategory_AndRunsAnEmbeddedDemoInItsTab()
        {
            using (var shell = new ShellForm())
            {
                // Off screen, not maximized: a test must not cover the desktop.
                shell.WindowState = FormWindowState.Normal;
                shell.StartPosition = FormStartPosition.Manual;
                shell.Location = new Point(-3000, -3000);
                shell.ShowInTaskbar = false;

                var presenter = new ShellPresenter(SampleCatalog.Create());
                presenter.Connect(shell);
                shell.Show();

                var tabs = shell.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<TabControl>()).Single();
                var sidebar = shell.Controls.OfType<ListBox>().Single();
                foreach (var category in SampleCatalog.Create())
                {
                    shell.SelectCategory(category.Id);
                    Assert.Equal(category.Demos.Count + 2, tabs.TabPages.Count);   // 説明, the demos, コード
                    Assert.Equal(0, tabs.SelectedIndex);
                }

                shell.SelectCategory("viewaction");
                var todoTab = tabs.TabPages.Cast<TabPage>().Single(p => (string)p.Tag == DemoIds.ToDo);
                Assert.Empty(todoTab.Controls);   // not created until opened
                tabs.SelectedTab = todoTab;
                var todo = Assert.IsType<ToDoDemoControl>(Assert.Single(todoTab.Controls));

                shell.SelectCategory("intro");
                Assert.True(todo.IsDisposed);   // leaving the category released the demo and its control
                Assert.Equal(0, sidebar.SelectedIndex);

                shell.Close();
            }
        }
    }
}
