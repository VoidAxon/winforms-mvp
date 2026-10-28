using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.Shell;
using Kibinago.Samples.ToDoDemo;
using Kibinago.Views;
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

        [Fact]
        public void EveryEmbeddedDemo_StartsInItsTab_AndIsReleasedWithItsCategory()
        {
            using (var shell = new ShellForm())
            {
                shell.WindowState = FormWindowState.Normal;
                shell.StartPosition = FormStartPosition.Manual;
                shell.Location = new Point(-3000, -3000);
                shell.ShowInTaskbar = false;
                new ShellPresenter(SampleCatalog.Create()).Connect(shell);
                shell.Show();
                var tabs = shell.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<TabControl>()).Single();

                var opened = 0;
                foreach (var category in SampleCatalog.Create())
                {
                    shell.SelectCategory(category.Id);
                    var controls = new List<Control>();
                    foreach (var demo in category.Demos.Where(d => d.Kind == DemoKind.Embedded))
                    {
                        var tab = tabs.TabPages.Cast<TabPage>().Single(p => (string)p.Tag == demo.Id);
                        tabs.SelectedTab = tab;   // creates the control and attaches its presenter
                        Application.DoEvents();
                        var control = Assert.Single(tab.Controls.Cast<Control>());
                        Assert.IsAssignableFrom<IViewBase>(control);
                        controls.Add(control);
                        opened++;
                    }

                    shell.SelectCategory(category.Id == "intro" ? "viewaction" : "intro");   // leave the category
                    Assert.All(controls, c => Assert.True(c.IsDisposed, c.GetType().Name + " was not released"));
                }

                Assert.True(opened >= 20, $"only {opened} embedded demos");
                shell.Close();
            }
        }
    }
}
