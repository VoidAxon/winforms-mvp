using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Logging;

namespace Kibinago.Services
{
    /// <summary>
    /// The busy state of one UI thread, shared by every <see cref="WinFormsBusyIndicator"/> on it. Requests are
    /// counted per window (the view's form, an MDI child on its own: wait cursor and input block) and per view
    /// (the overlay). A request ends exactly once: by its token, or when its view or window is closed or
    /// disposed, or when the thread's message loop ends. All members run on that thread.
    /// </summary>
    internal sealed class BusyCoordinator : IMessageFilter
    {
        [ThreadStatic] private static BusyCoordinator _current;

        private readonly Dictionary<Control, WindowState> _windows = new Dictionary<Control, WindowState>();
        private readonly Dictionary<Control, ViewState> _views = new Dictionary<Control, ViewState>();
        private readonly HashSet<Control> _blocked = new HashSet<Control>();
        private readonly HashSet<Control> _releasing = new HashSet<Control>();
        private bool _filterInstalled;
        private bool _idleHooked;
        private bool _exitHooked;
        private readonly System.Threading.Thread _thread = System.Threading.Thread.CurrentThread;

        public static BusyCoordinator ForCurrentThread() => _current ?? (_current = new BusyCoordinator());

        /// <summary>For tests: the coordinator of the calling thread, if any.</summary>
        internal static BusyCoordinator CurrentOrNull => _current;

        internal int BusyWindowCount => _windows.Count;
        internal int BusyViewCount => _views.Count;
        internal bool IsBlocked(Control window) => _blocked.Contains(window);

        // ---- begin / end ----

        public Request Begin(Control view, string message, BusyRenderer overlay, int delay, ILogger logger)
        {
            var request = new Request(view, WindowOf(view), message);
            AcquireWindow(request);
            try
            {
                AcquireView(request, overlay, delay, logger);
            }
            catch
            {
                ReleaseWindow(request, immediate: false);
                throw;
            }
            if (!_exitHooked)
            {
                Application.ThreadExit += OnThreadExit;
                _exitHooked = true;
            }
            return request;
        }

        public void End(Request request)
        {
            if (request.Ended) return;
            request.Ended = true;
            ReleaseView(request);
            ReleaseWindow(request, immediate: false);
        }

        private void AcquireWindow(Request request)
        {
            var window = request.Window;
            if (!_windows.TryGetValue(window, out var state))
            {
                // Under a busy window (an MDI child of a busy parent) UseWaitCursor is inherited, not the app's.
                var inherited = _windows.Keys.Any(w => w != window && w.Contains(window));
                state = new WindowState(window, this, inherited ? false : window.UseWaitCursor, inherited);
                _windows.Add(window, state);
            }
            state.Requests.Add(request);
            if (state.Requests.Count > 1) return;

            state.RememberOwnWaitCursors(c => c != window && _windows.ContainsKey(c));
            window.UseWaitCursor = true;           // stays while the message loop runs (awaits)
            Cursor.Current = Cursors.WaitCursor;   // shows at once, also while synchronous work holds the thread
            Block(window);
        }

        private void ReleaseWindow(Request request, bool immediate)
        {
            if (!_windows.TryGetValue(request.Window, out var state) || !state.Requests.Remove(request)) return;
            if (state.Requests.Count > 0) return;

            _windows.Remove(state.Window);
            state.Detach();
            // UseWaitCursor applies to children: inside a still-busy window (an MDI parent) it stays on, and
            // windows inside this one that are still busy keep theirs.
            // The innermost still-busy window around this one (an MDI parent busy longer than its child).
            var enclosing = _windows.Values.Where(w => !w.Window.IsDisposed && w.Window.Contains(state.Window))
                .OrderByDescending(w => Depth(w.Window)).FirstOrDefault();
            // Busy windows inside this one take over the own values recorded inside them (they restore them).
            foreach (var inner in _windows.Values)
                if (!inner.Window.IsDisposed && state.Window.Contains(inner.Window)) inner.TakeInside(state);
            if (!state.Window.IsDisposed)
            {
                state.Window.UseWaitCursor = state.HadWaitCursor || enclosing != null;   // sets the whole subtree …
                if (enclosing != null) enclosing.Adopt(state);                          // … its own values return with the enclosing window
                else state.RestoreOwnWaitCursors();                                     // … so put back the controls' own
            }
            foreach (var other in _windows.Keys)
                if (!other.IsDisposed && state.Window.Contains(other)) other.UseWaitCursor = true;
            if (_windows.Count == 0) Cursor.Current = Cursors.Default;
            Unblock(state.Window, immediate);
        }

        private void AcquireView(Request request, BusyRenderer overlay, int delay, ILogger logger)
        {
            var view = request.View;
            if (!_views.TryGetValue(view, out var state))
            {
                state = new ViewState(view, this);
                _views.Add(view, state);
            }
            state.Requests.Add(request);
            if (state.Requests.Count == 1)
            {
                if (overlay != null)
                {
                    try { state.Overlay = new BusyOverlay(view, overlay, delay, request.Message, logger); }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "The busy overlay could not be created; the view is busy without it");
                    }
                }
            }
            else state.Overlay?.SetMessage(state.Message);
        }

        private void ReleaseView(Request request)
        {
            if (!_views.TryGetValue(request.View, out var state) || !state.Requests.Remove(request)) return;
            if (state.Requests.Count > 0)
            {
                state.Overlay?.SetMessage(state.Message);
                return;
            }
            _views.Remove(state.View);
            state.Detach();
            state.Overlay?.Dispose();
            state.Overlay = null;
        }

        // A view or window that is closed or disposed ends every request on it at once.
        private void EndAll(Func<Request, bool> match, bool immediate)
        {
            var requests = _windows.Values.SelectMany(w => w.Requests).Where(match).Distinct().ToList();
            foreach (var request in requests)
            {
                if (request.Ended) continue;
                request.Ended = true;
                ReleaseView(request);
                ReleaseWindow(request, immediate);
            }
        }

        private void OnViewGone(Control view) => EndAll(r => r.View == view, immediate: false);

        // The view (or a container) got another parent: its requests move to the window it is in now.
        private void OnViewMoved(Control view)
        {
            if (!_views.TryGetValue(view, out var state)) return;
            state.Track();
            var window = WindowOf(view);
            var moving = state.Requests.Where(r => r.Window != window).ToList();
            if (moving.Count == 0) return;

            // The view leaves with its own wait-cursor values, not the old window's busy cursor.
            var own = new List<Control>();
            foreach (var old in moving.Select(r => r.Window).Distinct())
                if (_windows.TryGetValue(old, out var oldState)) own.AddRange(oldState.TakeOwnInside(view));
            if (!view.IsDisposed)
            {
                view.UseWaitCursor = false;
                foreach (var c in own) if (!c.IsDisposed) c.UseWaitCursor = true;
            }

            foreach (var request in moving)
            {
                ReleaseWindow(request, immediate: false);
                request.Window = window;
                AcquireWindow(request);
            }
            if (_windows.TryGetValue(window, out var newState)) newState.AddOwn(own);
        }

        private void OnWindowGone(Control window) => EndAll(r => r.Window == window, immediate: true);

        private void OnThreadExit(object sender, EventArgs e)
        {
            if (System.Threading.Thread.CurrentThread != _thread) return;   // ThreadExit is raised for every UI thread
            EndAll(r => true, immediate: true);
            Application.ThreadExit -= OnThreadExit;
            _exitHooked = false;
            if (_idleHooked) { Application.Idle -= OnIdle; _idleHooked = false; }
            if (_filterInstalled) { Application.RemoveMessageFilter(this); _filterInstalled = false; }
            _blocked.Clear();
            _releasing.Clear();
            if (_current == this) _current = null;
        }

        // ---- input block ----

        private void Block(Control window)
        {
            _releasing.Remove(window);
            _blocked.Add(window);
            CloseDropDowns(window);
            CancelImeComposition(window);
            if (_filterInstalled) return;
            Application.AddMessageFilter(this);
            _filterInstalled = true;
        }

        // Normal end: wait for Application.Idle, so clicks and keys queued while synchronous work held the
        // thread are dropped instead of running after it. A closed window is released at once.
        private void Unblock(Control window, bool immediate)
        {
            if (!_blocked.Contains(window)) return;
            if (immediate)
            {
                _blocked.Remove(window);
                _releasing.Remove(window);
                RemoveFilterIfIdle();
                return;
            }
            _releasing.Add(window);
            if (_idleHooked) return;
            Application.Idle += OnIdle;
            _idleHooked = true;
        }

        private void OnIdle(object sender, EventArgs e) => ReleaseAfterIdle();

        /// <summary>What Application.Idle does: lets go of the windows whose busy state ended (internal for tests).</summary>
        internal void ReleaseAfterIdle()
        {
            if (_idleHooked) Application.Idle -= OnIdle;
            _idleHooked = false;
            foreach (var window in _releasing) _blocked.Remove(window);
            _releasing.Clear();
            RemoveFilterIfIdle();
        }

        private void RemoveFilterIfIdle()
        {
            if (_blocked.Count > 0 || !_filterInstalled) return;
            Application.RemoveMessageFilter(this);
            _filterInstalled = false;
        }

        public bool PreFilterMessage(ref Message m) => ShouldBlock(m.Msg, m.WParam, Control.FromChildHandle(m.HWnd));

        /// <summary>Whether an input message for <paramref name="target"/> is dropped (internal for tests).</summary>
        internal bool ShouldBlock(int msg, IntPtr wParam, Control target)
        {
            if (target == null || _blocked.Count == 0) return false;   // a MessageBox, a busy overlay, another app
            const int WM_NCLBUTTONDOWN = 0x00A1, WM_NCLAST = 0x00AD;
            const int WM_KEYFIRST = 0x0100, WM_SYSKEYDOWN = 0x0104, WM_KEYLAST = 0x0109;
            const int WM_LBUTTONDOWN = 0x0201, WM_MOUSELAST = 0x020E;
            const int VK_F4 = 0x73;

            var source = SourceOf(target);
            if (source == null) return false;
            Control blockedWindow = null;   // the busy window the input is inside (an MDI child of a busy parent too)
            for (var c = source; c != null && blockedWindow == null; c = c.Parent)
                if (_blocked.Contains(c)) blockedWindow = c;
            if (blockedWindow == null) return false;
            if (msg >= WM_NCLBUTTONDOWN && msg <= WM_NCLAST)
            {
                // A control's non-client area (its scroll bars) is blocked; the busy form's own frame (title
                // bar, its buttons, borders) is not — except the form's own scroll bars (AutoScroll).
                const int HTHSCROLL = 6, HTVSCROLL = 7;
                var hit = (int)wParam;
                return target != blockedWindow || hit == HTHSCROLL || hit == HTVSCROLL;
            }
            if (msg == WM_SYSKEYDOWN && (int)wParam == VK_F4) return false;              // Alt+F4 still closes (through CanClose)
            return (msg >= WM_KEYFIRST && msg <= WM_KEYLAST) || (msg >= WM_LBUTTONDOWN && msg <= WM_MOUSELAST);
        }

        private static int Depth(Control c)
        {
            var depth = 0;
            for (; c != null; c = c.Parent) depth++;
            return depth;
        }

        // The window a view is in: its form (an MDI child on its own), else its top-level control.
        internal static Control WindowOf(Control control) => (Control)control.FindForm() ?? control.TopLevelControl ?? control;

        // A menu that stayed open (a submenu of a context menu too) acts for the control it was opened from.
        private static Control SourceOf(Control target)
        {
            var c = target;
            for (var depth = 0; c is ToolStripDropDown dropDown && depth < 32; depth++)
                c = (Control)dropDown.OwnerItem?.Owner ?? (dropDown as ContextMenuStrip)?.SourceControl;
            return c is ToolStripDropDown ? null : c;
        }

        // A composition in progress (Japanese input not yet committed) could still be committed from the IME's
        // candidate window, which is not ours to filter: cancel it. New compositions cannot start (keys are dropped).
        private static void CancelImeComposition(Control window)
        {
            try
            {
                var focused = GetFocus();
                if (focused == IntPtr.Zero) return;
                var control = Control.FromChildHandle(focused);
                if (control == null || (control != window && !window.Contains(control))) return;
                var context = ImmGetContext(focused);
                if (context == IntPtr.Zero) return;
                try
                {
                    const int NI_COMPOSITIONSTR = 0x0015, CPS_CANCEL = 0x0004;
                    ImmNotifyIME(context, NI_COMPOSITIONSTR, CPS_CANCEL, 0);
                }
                finally
                {
                    ImmReleaseContext(focused, context);
                }
            }
            catch (DllNotFoundException) { }   // no IME support on this system
            catch (EntryPointNotFoundException) { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetFocus();
        [System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr hwnd);
        [System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr himc);
        [System.Runtime.InteropServices.DllImport("imm32.dll")] private static extern bool ImmNotifyIME(IntPtr himc, int action, int index, int value);

        private static void CloseDropDowns(Control root)
        {
            if (root.ContextMenuStrip != null && root.ContextMenuStrip.Visible) root.ContextMenuStrip.Close();   // not in Controls
            foreach (Control c in root.Controls)
            {
                if (c.ContextMenuStrip != null && c.ContextMenuStrip.Visible) c.ContextMenuStrip.Close();
                if (c is ComboBox combo && combo.DroppedDown) combo.DroppedDown = false;
                if (c is ToolStrip strip)
                    foreach (ToolStripItem item in strip.Items)
                        if (item is ToolStripDropDownItem dropDownItem && dropDownItem.DropDown.Visible) dropDownItem.HideDropDown();
                if (c.HasChildren) CloseDropDowns(c);
            }
        }

        // ---- state ----

        internal sealed class Request
        {
            public Request(Control view, Control window, string message)
            {
                View = view; Window = window; Message = message;
            }

            public Control View { get; }
            public Control Window { get; set; }
            public string Message { get; }
            public bool Ended { get; set; }
        }

        private sealed class WindowState
        {
            private readonly BusyCoordinator _owner;

            private readonly bool _inherited;

            public WindowState(Control window, BusyCoordinator owner, bool hadWaitCursor, bool inherited)
            {
                Window = window; _owner = owner;
                HadWaitCursor = hadWaitCursor;
                _inherited = inherited;
                window.Disposed += OnGone;
                if (window is Form form) form.FormClosed += OnGone;   // a closed modal form may never be disposed
            }

            public Control Window { get; }
            public bool HadWaitCursor { get; }
            public List<Request> Requests { get; } = new List<Request>();
            private List<Control> _ownWaitCursors;

            // Controls inside the window with UseWaitCursor set of their own (setting it on the window overwrites them).
            // Busy windows inside this one are skipped: their values are the framework's, theirs to restore.
            public void RememberOwnWaitCursors(Func<Control, bool> isBusyWindow)
            {
                _ownWaitCursors = null;
                if (HadWaitCursor || _inherited) return;   // inside a busy window the controls' values are not their own
                Collect(Window, isBusyWindow);
            }

            private void Collect(Control parent, Func<Control, bool> isBusyWindow)
            {
                foreach (Control c in parent.Controls)
                {
                    if (isBusyWindow(c)) continue;
                    if (c.UseWaitCursor) (_ownWaitCursors ?? (_ownWaitCursors = new List<Control>())).Add(c);
                    else if (c.HasChildren) Collect(c, isBusyWindow);
                }
            }

            /// <summary>Takes over the own values that <paramref name="outer"/> recorded inside this window (it ends first).</summary>
            public void TakeInside(WindowState outer) => AddOwn(outer.TakeOwnInside(Window));

            /// <summary>Removes and returns the own values recorded in <paramref name="root"/> or inside it.</summary>
            public List<Control> TakeOwnInside(Control root)
            {
                if (_ownWaitCursors == null) return new List<Control>();
                var inside = _ownWaitCursors.Where(c => c == root || root.Contains(c)).ToList();
                foreach (var c in inside) _ownWaitCursors.Remove(c);
                return inside;
            }

            public void AddOwn(IEnumerable<Control> controls)
            {
                foreach (var c in controls)
                {
                    if (_ownWaitCursors == null) _ownWaitCursors = new List<Control>();
                    if (!_ownWaitCursors.Contains(c)) _ownWaitCursors.Add(c);
                }
            }

            /// <summary>Takes over the own values of a window inside this one that ended first.</summary>
            public void Adopt(WindowState inner)
            {
                if (inner._ownWaitCursors == null) return;
                (_ownWaitCursors ?? (_ownWaitCursors = new List<Control>())).AddRange(inner._ownWaitCursors);
                inner._ownWaitCursors = null;
            }

            public void RestoreOwnWaitCursors()
            {
                if (_ownWaitCursors == null) return;
                foreach (var c in _ownWaitCursors)
                    if (!c.IsDisposed) c.UseWaitCursor = true;
                _ownWaitCursors = null;
            }

            private void OnGone(object sender, EventArgs e) => _owner.OnWindowGone(Window);

            public void Detach()
            {
                Window.Disposed -= OnGone;
                if (Window is Form form) form.FormClosed -= OnGone;
            }
        }

        private sealed class ViewState
        {
            private readonly BusyCoordinator _owner;

            private readonly List<Control> _tracked = new List<Control>();

            public ViewState(Control view, BusyCoordinator owner)
            {
                View = view; _owner = owner;
                view.Disposed += OnGone;
                Track();
            }

            public Control View { get; }
            public List<Request> Requests { get; } = new List<Request>();
            public BusyOverlay Overlay { get; set; }

            /// <summary>The latest non-empty message of the open requests.</summary>
            public string Message => Requests.Select(r => r.Message).LastOrDefault(m => !string.IsNullOrEmpty(m));

            private void OnGone(object sender, EventArgs e) => _owner.OnViewGone(View);

            private void OnParentChanged(object sender, EventArgs e) => _owner.OnViewMoved(View);

            // The view and its containers: a new parent anywhere may put it in another window.
            public void Track()
            {
                Untrack();
                for (var c = View; c != null; c = c.Parent)
                {
                    c.ParentChanged += OnParentChanged;
                    _tracked.Add(c);
                }
            }

            private void Untrack()
            {
                foreach (var c in _tracked) c.ParentChanged -= OnParentChanged;
                _tracked.Clear();
            }

            public void Detach()
            {
                View.Disposed -= OnGone;
                Untrack();
            }
        }
    }
}
