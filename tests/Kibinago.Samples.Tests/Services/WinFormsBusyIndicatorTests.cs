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

        private sealed class FontRecordingRenderer : BusyRenderer
        {
            public Font LayoutFont { get; private set; }
            public Font PaintFont { get; private set; }

            public override Rectangle GetIndicatorBounds(BusyLayoutContext context)
            {
                LayoutFont = context.Font;
                return new Rectangle(0, 0, 20, 20);
            }

            public override void PaintIndicator(BusyPaintContext context) => PaintFont = context.Font;
        }

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
                Assert.False(panel.UseWaitCursor);
                Assert.False(second.UseWaitCursor);

                var own = new ViewPanel { UseWaitCursor = true };   // a control's own setting moves with it
                panel.Controls.Add(own);
                busy = new WinFormsBusyIndicator().Begin(panel, null);
                first.Controls.Add(panel);
                busy.Dispose();
                Assert.True(own.UseWaitCursor);
                Assert.False(panel.UseWaitCursor);
                AssertNothingBusy();
            }
        }

        [Fact]
        public void AnMdiChildsOwnWaitCursor_ReturnsAfterItsParent_EndsLast()
        {
            using (var parent = new ViewForm { IsMdiContainer = true })
            {
                var child = new ViewForm(createHandle: false) { MdiParent = parent, UseWaitCursor = true };   // the app's own setting
                child.CreateControl();
                var indicator = new WinFormsBusyIndicator();
                var inner = indicator.Begin(child, null);    // child first
                var outer = indicator.Begin(parent, null);

                inner.Dispose();                             // the child ends first …
                outer.Dispose();                             // … the parent's end sets the subtree, then restores the child's own

                Assert.True(child.UseWaitCursor);
                Assert.False(parent.UseWaitCursor);
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

                var box = new TextBox();
                child.Controls.Add(box);
                outer = indicator.Begin(parent, null);   // parent first: the child's controls inherit the cursor
                inner = indicator.Begin(child, null);
                outer.Dispose();
                inner.Dispose();
                Assert.False(box.UseWaitCursor);          // not taken for the control's own setting

                inner = indicator.Begin(child, null);     // child first
                outer = indicator.Begin(parent, null);
                inner.Dispose();
                Assert.True(child.UseWaitCursor);         // its parent is still busy
                outer.Dispose();
                Assert.False(child.UseWaitCursor);
                Assert.False(box.UseWaitCursor);

                box.UseWaitCursor = true;                 // the app's own setting survives both orders
                inner = indicator.Begin(child, null);
                outer = indicator.Begin(parent, null);
                inner.Dispose();
                outer.Dispose();
                Assert.True(box.UseWaitCursor);
                Assert.False(child.UseWaitCursor);
                outer = indicator.Begin(parent, null);
                inner = indicator.Begin(child, null);
                outer.Dispose();
                inner.Dispose();
                Assert.True(box.UseWaitCursor);
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

                Assert.True(Coordinator == null || Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, menu) == false);
                using (new WinFormsBusyIndicator().Begin(form, null))
                {
                    Assert.False(menu.Visible);   // closed when the block started (it would not take input)

                    menu.Show(button, new Point(0, 0));   // opened by code while busy: its input is the window's
                    item.ShowDropDown();
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, menu));
                    Assert.True(Coordinator.ShouldBlock(WM_LBUTTONDOWN, IntPtr.Zero, item.DropDown));
                    menu.Close();
                }
                AssertNothingBusy();
            }
        }

        [Fact]
        public void AControlsOwnWaitCursor_IsKeptAfterTheBusyEnds()
        {
            using (var form = new ViewForm())
            {
                var panel = new ViewPanel { UseWaitCursor = true };
                form.Controls.Add(panel);

                using (new WinFormsBusyIndicator().Begin(form, null)) { }

                Assert.False(form.UseWaitCursor);
                Assert.True(panel.UseWaitCursor);
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
        public void BeginAndEndOnAWorker_ArePostedToTheUiThread()
        {
            using (var form = new ViewForm())
            {
                var context = new WindowsFormsSynchronizationContext();
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    // Posted; it may already run while this STA thread waits for the worker (the wait pumps).
                    var busy = System.Threading.Tasks.Task.Run(() => new WinFormsBusyIndicator().Begin(form, null)).Result;

                    Application.DoEvents();             // the UI thread starts it
                    Assert.True(form.UseWaitCursor);
                    Assert.True(Coordinator.IsBlocked(form));

                    System.Threading.Tasks.Task.Run(busy.Dispose).Wait();
                    Application.DoEvents();             // … and ends it
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
        public void ARequestBegunAndEndedOnAWorkerBeforeTheUiThreadRuns_NeverStarts()
        {
            using (var form = new ViewForm())
            {
                System.Threading.Tasks.Task.Run(() => new WinFormsBusyIndicator().Begin(form, null).Dispose()).Wait();
                Application.DoEvents();   // the posted start finds the request ended

                Assert.False(form.UseWaitCursor);
                AssertNothingBusy();
            }
        }

        [Fact]
        public void BeginOnAWorker_BeforeAnyHandle_DoesNothing_AndRaisesNoError()
        {
            using (var form = new ViewForm(createHandle: false))
            {
                var panel = new ViewPanel();
                form.Controls.Add(panel);
                var busy = System.Threading.Tasks.Task.Run(() => new WinFormsBusyIndicator().Begin(panel, null)).Result;

                var handle = form.Handle;   // no way to the UI thread before: nothing was started

                Assert.False(form.UseWaitCursor);
                Assert.False(Coordinator?.IsBlocked(form) ?? false);
                busy.Dispose();
                AssertNothingBusy();
            }
        }

        [Fact]
        public void WithoutAContext_ARequestEndedOnAWorker_RaisesNoError_AndEndsWithItsWindow()
        {
            // A UI thread without a synchronization context (AutoInstall off, per thread): a worker's Dispose
            // raises no error (it may have nothing to post through); the window's disposal ends the request.
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    WindowsFormsSynchronizationContext.AutoInstall = false;
                    System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                    var form = new ViewForm();
                    var busy = new WinFormsBusyIndicator().Begin(form, null);
                    Assert.True(form.UseWaitCursor);

                    System.Threading.Tasks.Task.Run(busy.Dispose).Wait();

                    form.Dispose();
                    AssertNothingBusy();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(10000));
            if (failure != null) throw new Exception("On the UI thread: " + failure.Message, failure);
        }

        [Fact]
        public void TheBuiltInLook_WidensForAMessage_UpToItsMaxTextWidth()
        {
            var renderer = new HudBusyRenderer();
            var view = new Size(1200, 800);
            Rectangle Bounds(string message) => renderer.GetIndicatorBounds(new BusyLayoutContext(view, 1f, SystemFonts.DefaultFont, message));

            var plain = Bounds(null);
            Assert.Equal(plain, Bounds(""));
            Assert.Equal(plain.Width, plain.Height);   // the square

            var text = Bounds("集計中…");
            Assert.True(text.Height > plain.Height);   // the text under the ring

            var longText = Bounds(new string('長', 400));
            Assert.True(longText.Width <= renderer.MaxTextWidth + 2 * 14 + 2 * 8 + 1);   // capped: text + padding + shadow margin
            var lineHeight = SystemFonts.DefaultFont.GetHeight(96);
            Assert.True(longText.Height < text.Height + 2 * lineHeight);   // two lines at most

            Assert.Equal(Rectangle.Empty, new HudBusyRenderer().GetIndicatorBounds(new BusyLayoutContext(new Size(60, 400), 1f, SystemFonts.DefaultFont, "集計中…")));
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
        public void TheOverlay_WritesWithTheConfiguredFont_OrTheViewsFont()
        {
            using (var font = new Font("Arial", 12f))
            using (var form = new ViewForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), Font = new Font("Tahoma", 9f) })
            {
                form.Show();
                var configured = new FontRecordingRenderer();
                using (new WinFormsBusyIndicator(new BusyOptions { Delay = 0, Font = font }, configured).Begin(form, null))
                {
                    Application.DoEvents();
                    Assert.Same(font, configured.LayoutFont);
                    Assert.Same(font, configured.PaintFont);
                }

                var plain = new FontRecordingRenderer();
                using (new WinFormsBusyIndicator(new BusyOptions { Delay = 0 }, plain).Begin(form, null))
                {
                    Application.DoEvents();
                    Assert.Same(form.Font, plain.LayoutFont);
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
        public void TheBuiltInLook_HasNoBackdropWindow_WhenItsBackdropIsFullyTransparent()
        {
            Assert.True(new HudBusyRenderer().HasBackdrop);
            Assert.True(new HudBusyRenderer { Backdrop = Color.FromArgb(1, 0, 0, 0) }.HasBackdrop);
            Assert.False(new HudBusyRenderer { Backdrop = Color.Transparent }.HasBackdrop);
            Assert.False(new HudBusyRenderer { Backdrop = Color.FromArgb(0, 255, 0, 0) }.HasBackdrop);
        }

        [Fact]
        public void APopupSurface_PaintsAtTheDpiItIsGiven_AfterAChangeToo()
        {
            using (var form = new ViewForm())
            using (var popup = new BusyLayeredPopup(form.Handle))
            {
                Assert.Equal(144f, popup.Begin(new Size(40, 30), 144).DpiX, 1);
                Assert.Equal(96f, popup.Begin(new Size(40, 30), 96).DpiX, 1);    // same surface, another DPI
                Assert.Equal(120f, popup.Begin(new Size(50, 30), 120).DpiY, 1);  // a new surface
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
