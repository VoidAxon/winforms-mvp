using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Services;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// The WinForms busy indicator: wait cursor, input block, and that every way a request can end leaves
    /// nothing behind (counts, blocked windows, overlay windows, Application.OpenForms).
    /// </summary>
    public class WinFormsBusyIndicatorTests
    {
        private sealed class ViewForm : Form, IViewBase
        {
            public ViewForm(bool createHandle = true)
            {
                if (createHandle) CreateHandle();   // tests run on the thread that owns it (the UI thread)
            }
        }

        private sealed class ViewPanel : UserControl, IViewBase { }

        private sealed class ThrowingRenderer : BusyRenderer
        {
            public override Rectangle GetIndicatorBounds(BusyLayoutContext context) => throw new InvalidOperationException("renderer bug");
            public override void PaintIndicator(BusyPaintContext context) { }
        }

        private static BusyCoordinator Coordinator => BusyCoordinator.CurrentOrNull;

        private static void AssertNothingBusy()
        {
            var coordinator = Coordinator;
            if (coordinator == null) return;
            coordinator.ReleaseAfterIdle();
            Assert.Equal(0, coordinator.BusyWindowCount);
            Assert.Equal(0, coordinator.BusyViewCount);
        }

        [Fact]
        public void Begin_ShowsTheWaitCursor_AndBlocksTheWindow_UntilTheRequestEndsAndTheQueueIsIdle()
        {
            using (var form = new ViewForm())
            {
                var busy = new WinFormsBusyIndicator().Begin(form, null);

                Assert.True(form.UseWaitCursor);
                Assert.True(Coordinator.IsBlocked(form));

                busy.Dispose();
                Assert.False(form.UseWaitCursor);
                Assert.True(Coordinator.IsBlocked(form));    // input queued meanwhile is still dropped …
                Coordinator.ReleaseAfterIdle();
                Assert.False(Coordinator.IsBlocked(form));   // … until the queue is idle
                AssertNothingBusy();
            }
        }

        [Fact]
        public void NestedRequests_EndInAnyOrder_AndADoubleDisposeDoesNothing()
        {
            using (var form = new ViewForm())
            {
                var indicator = new WinFormsBusyIndicator();
                var outer = indicator.Begin(form, "outer");
                var inner = indicator.Begin(form, null);

                outer.Dispose();
                Assert.True(form.UseWaitCursor);
                outer.Dispose();
                Assert.True(form.UseWaitCursor);
                inner.Dispose();
                Assert.False(form.UseWaitCursor);
                AssertNothingBusy();
            }
        }

        [Fact]
        public void TwoIndicators_OnOneWindow_DoNotUnblockEachOther()
        {
            using (var form = new ViewForm())
            {
                var first = new WinFormsBusyIndicator().Begin(form, null);
                var second = new WinFormsBusyIndicator().Begin(form, null);

                first.Dispose();
                Coordinator.ReleaseAfterIdle();
                Assert.True(Coordinator.IsBlocked(form));
                Assert.True(form.UseWaitCursor);

                second.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void AChildPanelBusy_BlocksItsWholeWindow()
        {
            using (var form = new ViewForm())
            {
                var panel = new ViewPanel();
                form.Controls.Add(panel);

                var busy = new WinFormsBusyIndicator().Begin(panel, null);

                Assert.True(Coordinator.IsBlocked(form));
                Assert.True(form.UseWaitCursor);
                busy.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void ABusyPanelMovedToAnotherWindow_MovesTheBlock()
        {
            using (var first = new ViewForm())
            using (var second = new ViewForm())
            {
                var panel = new ViewPanel();
                first.Controls.Add(panel);
                var busy = new WinFormsBusyIndicator().Begin(panel, null);

                second.Controls.Add(panel);   // reparented while busy

                Coordinator.ReleaseAfterIdle();
                Assert.False(Coordinator.IsBlocked(first));
                Assert.False(first.UseWaitCursor);
                Assert.True(Coordinator.IsBlocked(second));
                Assert.True(second.UseWaitCursor);
                busy.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void BusyParentAndMdiChild_EndingInEitherOrder_LeaveNoWaitCursor()
        {
            using (var parent = new ViewForm { IsMdiContainer = true })
            {
                var child = new ViewForm(createHandle: false) { MdiParent = parent };
                child.CreateControl();
                var indicator = new WinFormsBusyIndicator();
                var outer = indicator.Begin(parent, null);
                var inner = indicator.Begin(child, null);

                outer.Dispose();
                Assert.True(child.UseWaitCursor);    // still busy itself
                inner.Dispose();
                Assert.False(child.UseWaitCursor);
                Assert.False(parent.UseWaitCursor);

                outer = indicator.Begin(parent, null);
                inner = indicator.Begin(child, null);
                inner.Dispose();
                Assert.True(child.UseWaitCursor);    // its parent is still busy
                outer.Dispose();
                Assert.False(child.UseWaitCursor);
                AssertNothingBusy();
            }
        }

        [Fact]
        public void ABusyMdiParent_BlocksItsChildren_ABusyChildDoesNotBlockTheParent()
        {
            const int WM_LBUTTONDOWN = 0x0201;
            using (var parent = new ViewForm { IsMdiContainer = true })
            {
                var child = new ViewForm(createHandle: false) { MdiParent = parent };
                var box = new TextBox();
                child.Controls.Add(box);
                child.CreateControl();
                var indicator = new WinFormsBusyIndicator();

                using (indicator.Begin(parent, null))
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, box));
                Coordinator.ReleaseAfterIdle();

                using (indicator.Begin(child, null))
                {
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, box));
                    Assert.False(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, parent));
                }
                AssertNothingBusy();
            }
        }

        [Fact]
        public void AnOpenSubmenuOfAContextMenu_IsBlockedWithTheWindowItWasOpenedFrom()
        {
            const int WM_LBUTTONDOWN = 0x0201;
            using (var form = new ViewForm())
            using (var menu = new ContextMenuStrip())
            {
                var button = new Button();
                form.Controls.Add(button);
                var item = new ToolStripMenuItem("More");
                item.DropDownItems.Add("Delete");
                menu.Items.Add(item);
                button.ContextMenuStrip = menu;
                menu.Show(button, new Point(0, 0));   // SourceControl = button
                item.ShowDropDown();

                using (new WinFormsBusyIndicator().Begin(form, null))
                {
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, menu));
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, item.DropDown));
                }
                menu.Close();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void DisposingABusyChildPanel_ReleasesItsWindow()
        {
            using (var form = new ViewForm())
            {
                var panel = new ViewPanel();
                form.Controls.Add(panel);
                var busy = new WinFormsBusyIndicator().Begin(panel, null);

                panel.Dispose();

                Assert.False(form.UseWaitCursor);
                AssertNothingBusy();
                busy.Dispose();   // late: no effect, no error
            }
        }

        [Fact]
        public void DisposingABusyWindow_ReleasesItAtOnce()
        {
            var form = new ViewForm();
            var busy = new WinFormsBusyIndicator().Begin(form, null);

            form.Dispose();

            Assert.False(Coordinator.IsBlocked(form));   // no wait for an idle that may never come
            Assert.Equal(0, Coordinator.BusyWindowCount);
            busy.Dispose();
        }

        [Fact]
        public void ARequestEndedOnAnotherThread_IsReleasedOnTheUiThread()
        {
            using (var form = new ViewForm())
            {
                var handle = form.Handle;   // a handle, so the UI thread has a context to post to
                var context = new WindowsFormsSynchronizationContext();
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    var busy = new WinFormsBusyIndicator().Begin(form, null);

                    var worker = new System.Threading.Thread(busy.Dispose);
                    worker.Start();
                    worker.Join();
                    Application.DoEvents();   // runs the posted release

                    Assert.False(form.UseWaitCursor);
                    AssertNothingBusy();
                }
                finally
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                }
            }
        }

        [Fact]
        public void BeginOnAWorker_BeforeAnyHandle_StartsWhenTheWindowsHandleIsCreated()
        {
            using (var form = new ViewForm(createHandle: false))
            {
                var panel = new ViewPanel();
                form.Controls.Add(panel);
                var busy = System.Threading.Tasks.Task.Run(() => new WinFormsBusyIndicator().Begin(panel, null)).Result;
                Assert.False(form.UseWaitCursor);   // nothing done on the worker

                var handle = form.Handle;           // created here, on this (the UI) thread

                Assert.True(form.UseWaitCursor);
                Assert.True(Coordinator.IsBlocked(form));
                busy.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void TheInputFilter_DropsInputForTheBusyWindow_ButNotItsFrameAltF4OrOtherWindows()
        {
            const int WM_LBUTTONDOWN = 0x0201, WM_MOUSEMOVE = 0x0200, WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
            const int WM_NCLBUTTONDOWN = 0x00A1, VK_F4 = 0x73, VK_A = 0x41;
            const int HTCAPTION = 2, HTVSCROLL = 7, HTCLOSE = 20;
            using (var form = new ViewForm())
            using (var other = new ViewForm())
            {
                var button = new Button();
                form.Controls.Add(button);
                using (new WinFormsBusyIndicator().Begin(form, null))
                {
                    var filter = Coordinator;
                    Assert.True(filter.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, button));
                    Assert.True(filter.ShouldBlock(WM_KEYDOWN, (IntPtr)VK_A, button));
                    Assert.True(filter.ShouldBlock(WM_NCLBUTTONDOWN, IntPtr.Zero, button));   // a control's scroll bar
                    Assert.False(filter.ShouldBlock(WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, form));   // the title bar
                    Assert.False(filter.ShouldBlock(WM_NCLBUTTONDOWN, (IntPtr)HTCLOSE, form));     // its × button
                    Assert.True(filter.ShouldBlock(WM_NCLBUTTONDOWN, (IntPtr)HTVSCROLL, form));    // the form's own scroll bar (AutoScroll)
                    Assert.False(filter.ShouldBlock(WM_SYSKEYDOWN, (IntPtr)VK_F4, button));   // Alt+F4
                    Assert.False(filter.ShouldBlock(WM_MOUSEMOVE, IntPtr.Zero, button));
                    Assert.False(filter.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, other));
                    Assert.False(filter.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, null));      // a MessageBox, an overlay
                }
                AssertNothingBusy();
            }
        }

        [Fact]
        public void TheOverlay_NeverAppearsInOpenForms_AndLeavesNoWindowBehind()
        {
            using (var form = new ViewForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                form.Show();
                var before = Application.OpenForms.Count;

                var busy = new WinFormsBusyIndicator(new BusyOptions { Delay = 0 }, new HudBusyRenderer()).Begin(form, null);
                Application.DoEvents();
                Assert.Equal(before, Application.OpenForms.Count);

                busy.Dispose();
                Assert.Equal(before, Application.OpenForms.Count);
                AssertNothingBusy();
            }
        }

        [Fact]
        public void APopup_IsNotAForm_AndItsHandleIsGoneAfterDispose()
        {
            using (var form = new ViewForm())
            {
                var before = Application.OpenForms.Count;
                var popup = new BusyLayeredPopup(form.Handle);
                Assert.NotEqual(IntPtr.Zero, popup.Handle);
                var g = popup.Begin(new Size(40, 30), 96);
                g.FillRectangle(Brushes.Red, 0, 0, 10, 10);
                popup.Present(new Point(0, 0));
                Assert.Equal(before, Application.OpenForms.Count);

                popup.Dispose();
                popup.Dispose();
                Assert.Equal(IntPtr.Zero, popup.Handle);
            }
        }

        [Fact]
        public void AFailingRenderer_IsDropped_TheViewStaysBusy()
        {
            using (var form = new ViewForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                form.Show();
                var busy = new WinFormsBusyIndicator(new BusyOptions { Delay = 0 }, new ThrowingRenderer()).Begin(form, null);

                Assert.True(form.UseWaitCursor);
                Assert.True(Coordinator.IsBlocked(form));
                busy.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void ANegativeDelay_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WinFormsBusyIndicator(new BusyOptions { Delay = -1 }));
        }
    }
}
