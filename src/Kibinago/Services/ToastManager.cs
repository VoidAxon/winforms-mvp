using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// Collects the live toasts of one <see cref="WinFormsToastService"/>: it tracks the stack,
    /// enforces <see cref="ToastHostOptions.MaxVisibleToasts"/>, positions the stack, and holds the
    /// single anchored toast. It also carries the service's defaults that each toast reads.
    /// UI-thread only: the service creates, shows and closes toasts on its UI thread.
    /// </summary>
    internal sealed class ToastManager
    {
        // Live toasts in oldest-first order; this list also defines the stacking order.
        private readonly List<ToastNotification> _liveToasts = new List<ToastNotification>();

        // The single anchored (point-positioned) toast, if any. It does NOT participate in the
        // stack: it never appears in _liveToasts and is never reflowed.
        private ToastNotification _anchored;

        internal ToastManager(ToastHostOptions settings, Font defaultFont, ToastRenderer defaultRenderer)
        {
            Settings = settings;
            DefaultFont = defaultFont;
            DefaultRenderer = defaultRenderer;
        }

        /// <summary>Validated snapshot of the service's options.</summary>
        internal ToastHostOptions Settings { get; }

        /// <summary>Font used when a toast does not set its own.</summary>
        internal Font DefaultFont { get; }

        /// <summary>App-wide custom painter, or <c>null</c> to paint by <see cref="ToastHostOptions.Style"/>.</summary>
        internal ToastRenderer DefaultRenderer { get; }

        /// <summary>Stacked toasts, oldest first.</summary>
        internal IReadOnlyList<ToastNotification> LiveToasts => _liveToasts;

        /// <summary>The anchored toast, or <c>null</c>.</summary>
        internal ToastNotification Anchored => _anchored;

        public void Add(ToastNotification toast)
        {
            _liveToasts.Add(toast);

            // Cap the stack: evict the oldest toasts so it never overflows the screen. Iterate by
            // index 0 because CloseNow() calls back into Remove(), shrinking the list.
            while (_liveToasts.Count > Settings.MaxVisibleToasts)
            {
                _liveToasts[0].CloseNow();
            }

            Reflow();
        }

        public void RegisterAnchored(ToastNotification toast)
        {
            var previous = _anchored;
            _anchored = toast;

            // Only one anchored toast at a time. Close the old one *after* swapping the reference so
            // its CloseNow() -> Remove() does not clear the new one.
            if (previous != null)
            {
                previous.CloseNow();
            }
        }

        public void Remove(ToastNotification toast)
        {
            if (ReferenceEquals(_anchored, toast))
            {
                _anchored = null;
                return;
            }

            if (_liveToasts.Remove(toast))
            {
                Reflow();
            }
        }

        /// <summary>Closes every toast this manager holds (service shutdown).</summary>
        public void CloseAll()
        {
            while (_liveToasts.Count > 0)
            {
                _liveToasts[_liveToasts.Count - 1].CloseNow();
            }
            _anchored?.CloseNow();
        }

        private void Reflow()
        {
            var area = Screen.PrimaryScreen.WorkingArea;

            var boxes = new ToastBox[_liveToasts.Count];
            for (int i = 0; i < _liveToasts.Count; i++)
            {
                var toast = _liveToasts[i];
                boxes[i] = new ToastBox
                {
                    Position = toast.Position,
                    Width = toast.Width,
                    Height = toast.Height
                };
            }

            Point[] points = ToastLayout.Arrange(boxes, area, Settings.Margin, Settings.Gap);

            for (int i = 0; i < _liveToasts.Count; i++)
            {
                _liveToasts[i].MoveTo(points[i].X, points[i].Y);
            }
        }
    }
}
