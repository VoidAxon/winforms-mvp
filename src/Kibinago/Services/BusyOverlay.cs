using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Logging;

namespace Kibinago.Services
{
    /// <summary>
    /// The overlay of one busy view: after the delay, a backdrop popup over the view's visible area and an
    /// indicator popup, both owned by the view's top-level form. It follows the view and its containers
    /// (move, resize, show/hide, reparent, the owner's handle recreated), and hides while the owner is
    /// minimized, hidden or disabled by a modal dialog (so the dialog is never under it), and while an MDI
    /// child view is not the active child. A renderer that throws is logged and the overlay stops; the
    /// busy state itself (cursor, input block) is not affected.
    /// </summary>
    internal sealed class BusyOverlay : IDisposable
    {
        private readonly Control _view;
        private readonly BusyRenderer _renderer;
        private readonly ILogger _logger;
        private readonly Font _font;   // mvp.Busy.Font (caller-owned); null: Windows' UI font
        private Font _systemFont;      // Windows' UI font, created when first needed and disposed with the overlay
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly List<Control> _tracked = new List<Control>();
        private Timer _delay;
        private Timer _frames;
        private Control _owner;          // the top-level window owning the popups
        private Form _mdiParent;         // when the view is in an MDI child
        private BusyLayeredPopup _backdrop;
        private BusyLayeredPopup _indicator;
        private string _message;
        private Rectangle _indicatorBounds;   // in view coordinates
        private bool _layoutDirty = true;
        private bool _started;
        private bool _failed;
        private bool _disposed;

        public BusyOverlay(Control view, BusyRenderer renderer, int delay, Font font, string message, ILogger logger)
        {
            _view = view; _renderer = renderer; _font = font; _message = message; _logger = logger;
            Track();
            if (delay <= 0) Start();
            else
            {
                _delay = new Timer { Interval = delay };
                _delay.Tick += OnDelayElapsed;
                _delay.Start();
            }
        }

        public void SetMessage(string message)
        {
            if (_message == message) return;
            _message = message;
            _layoutDirty = true;
            Refresh();
        }

        private void OnDelayElapsed(object sender, EventArgs e)
        {
            StopTimer(ref _delay, OnDelayElapsed);
            Start();
        }

        private void Start()
        {
            if (_disposed || _failed) return;
            _started = true;
            _clock.Start();
            _frames = new Timer { Interval = 40 };
            _frames.Tick += OnFrame;
            _frames.Start();
            Refresh();
        }

        private void OnFrame(object sender, EventArgs e) => Refresh(indicatorOnly: true);

        // ---- tracking: the view, its containers up to the top-level window, the MDI parent ----

        private void Track()
        {
            Untrack();
            for (Control c = _view; c != null; c = c.Parent)
            {
                c.LocationChanged += OnLayoutChanged;
                c.SizeChanged += OnLayoutChanged;
                c.VisibleChanged += OnLayoutChanged;
                c.ParentChanged += OnParentChanged;
                _tracked.Add(c);
            }
            var top = _tracked[_tracked.Count - 1];
            if (_owner != top)
            {
                ClosePopups();   // a new owner: popups are recreated under it
                if (_owner != null)
                {
                    _owner.HandleCreated -= OnOwnerHandleCreated;
                    _owner.HandleDestroyed -= OnOwnerHandleDestroyed;
                }
                _owner = top;
                _owner.HandleCreated += OnOwnerHandleCreated;
                _owner.HandleDestroyed += OnOwnerHandleDestroyed;
            }
            var mdiParent = (_view.FindForm() is Form form && form.IsMdiChild) ? form.MdiParent : null;
            if (_mdiParent != mdiParent)
            {
                if (_mdiParent != null) _mdiParent.MdiChildActivate -= OnLayoutChanged;
                _mdiParent = mdiParent;
                if (_mdiParent != null) _mdiParent.MdiChildActivate += OnLayoutChanged;
            }
            _layoutDirty = true;
        }

        private void Untrack()
        {
            foreach (var c in _tracked)
            {
                c.LocationChanged -= OnLayoutChanged;
                c.SizeChanged -= OnLayoutChanged;
                c.VisibleChanged -= OnLayoutChanged;
                c.ParentChanged -= OnParentChanged;
            }
            _tracked.Clear();
        }

        private void OnParentChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            Track();
            Refresh();
        }

        private void OnLayoutChanged(object sender, EventArgs e)
        {
            _layoutDirty = true;
            Refresh();
        }

        private void OnOwnerHandleDestroyed(object sender, EventArgs e) => ClosePopups();   // Windows destroys owned popups with it

        private void OnOwnerHandleCreated(object sender, EventArgs e)
        {
            _layoutDirty = true;
            Refresh();
        }

        // ---- painting ----

        private void Refresh(bool indicatorOnly = false)
        {
            if (_disposed || _failed || !_started) return;
            try
            {
                var visible = VisibleScreenBounds();
                if (visible.IsEmpty)
                {
                    _backdrop?.Hide();
                    _indicator?.Hide();
                    _layoutDirty = true;   // repaint everything when it shows again
                    return;
                }
                if (!EnsurePopups()) return;

                var viewScreen = _view.RectangleToScreen(_view.ClientRectangle);
                var scale = _view.DeviceDpi / 96f;
                var elapsed = _clock.Elapsed;

                if (_layoutDirty)
                {
                    _indicatorBounds = _renderer.GetIndicatorBounds(new BusyLayoutContext(viewScreen.Size, scale, Font, _message));
                }

                // The backdrop is painted only when the layout changed (move, resize, message, shown again).
                if (_backdrop != null && (!indicatorOnly || _layoutDirty))
                {
                    var g = _backdrop.Begin(visible.Size, _view.DeviceDpi);
                    g.TranslateTransform(viewScreen.Left - visible.Left, viewScreen.Top - visible.Top);
                    var state = g.Save();
                    _renderer.PaintBackdrop(new BusyPaintContext(g, new Rectangle(Point.Empty, viewScreen.Size), elapsed, scale, Font, _message));
                    g.Restore(state);
                    _backdrop.Present(visible.Location);
                }
                _layoutDirty = false;

                var indicatorScreen = new Rectangle(viewScreen.Left + _indicatorBounds.Left, viewScreen.Top + _indicatorBounds.Top,
                    _indicatorBounds.Width, _indicatorBounds.Height);
                var shown = Rectangle.Intersect(indicatorScreen, visible);
                if (_indicatorBounds.Width <= 0 || _indicatorBounds.Height <= 0 || shown.Width <= 0 || shown.Height <= 0)
                {
                    _indicator.Hide();
                    return;
                }
                var ig = _indicator.Begin(shown.Size, _view.DeviceDpi);
                ig.TranslateTransform(indicatorScreen.Left - shown.Left, indicatorScreen.Top - shown.Top);
                var saved = ig.Save();
                _renderer.PaintIndicator(new BusyPaintContext(ig, new Rectangle(Point.Empty, indicatorScreen.Size), elapsed, scale, Font, _message));
                ig.Restore(saved);
                _indicator.Present(shown.Location);
                _indicator.PlaceAbove(_backdrop);
            }
            catch (Exception ex)
            {
                // A renderer (or the system) failed: stop the overlay, keep the busy state.
                _failed = true;
                _logger?.LogError(ex, "The busy overlay failed and was removed; the view stays busy (cursor and input block)");
                StopTimer(ref _frames, OnFrame);
                ClosePopups();
            }
        }

        private bool EnsurePopups()
        {
            var alive = _indicator != null && _indicator.IsAlive
                        && (!_renderer.HasBackdrop || (_backdrop != null && _backdrop.IsAlive));
            if (alive) return true;
            ClosePopups();
            if (!_owner.IsHandleCreated) return false;   // created when it is (OnOwnerHandleCreated)
            try
            {
                if (_renderer.HasBackdrop) _backdrop = new BusyLayeredPopup(_owner.Handle);
                _indicator = new BusyLayeredPopup(_owner.Handle);
                _layoutDirty = true;
                return true;
            }
            catch
            {
                ClosePopups();
                throw;
            }
        }

        // The view's client area on screen clipped by every container (scrolled-away or MDI-clipped parts are
        // not covered); empty while the overlay must not show.
        private Font Font => _font ?? (_systemFont ?? (_systemFont = DefaultFont())) ?? _view.Font;

        internal const float DefaultFontSize = 12f;

        // Windows' UI font (the one its dialogs use) at DefaultFontSize points.
        internal static Font DefaultFont()
        {
            using (var ui = SystemFonts.MessageBoxFont)
                return ui == null ? null : new Font(ui.FontFamily, DefaultFontSize, ui.Style);
        }

        private Rectangle VisibleScreenBounds()
        {
            if (!_owner.IsHandleCreated || !_view.IsHandleCreated || !_view.Visible) return Rectangle.Empty;
            var owner = _owner.Handle;
            if (!BusyLayeredPopup.Native.IsWindowVisible(owner) || BusyLayeredPopup.Native.IsIconic(owner)
                || !BusyLayeredPopup.Native.IsWindowEnabled(owner))   // a modal dialog is open over it
                return Rectangle.Empty;
            if (_mdiParent != null && _mdiParent.ActiveMdiChild != _view.FindForm()) return Rectangle.Empty;

            var r = _view.RectangleToScreen(_view.ClientRectangle);
            for (var c = _view.Parent; c != null; c = c.Parent)
            {
                if (!c.IsHandleCreated || !c.Visible) return Rectangle.Empty;
                r.Intersect(c.RectangleToScreen(c.ClientRectangle));
            }
            return r.Width > 0 && r.Height > 0 ? r : Rectangle.Empty;
        }

        private void ClosePopups()
        {
            var indicator = _indicator;
            var backdrop = _backdrop;
            _indicator = null;
            _backdrop = null;
            try { indicator?.Dispose(); }
            finally { backdrop?.Dispose(); }
        }

        private static void StopTimer(ref Timer timer, EventHandler handler)
        {
            if (timer == null) return;
            timer.Stop();
            timer.Tick -= handler;
            timer.Dispose();
            timer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopTimer(ref _delay, OnDelayElapsed);
            StopTimer(ref _frames, OnFrame);
            Untrack();
            if (_owner != null)
            {
                _owner.HandleCreated -= OnOwnerHandleCreated;
                _owner.HandleDestroyed -= OnOwnerHandleDestroyed;
            }
            if (_mdiParent != null) _mdiParent.MdiChildActivate -= OnLayoutChanged;
            ClosePopups();
            _systemFont?.Dispose();
            _systemFont = null;
        }
    }
}
