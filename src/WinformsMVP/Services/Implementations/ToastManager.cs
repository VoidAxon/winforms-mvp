using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Owns the set of on-screen toasts and arranges them. A single global stack on the UI thread:
    /// it tracks live toasts, enforces the <see cref="ToastDefaults.MaxVisibleToasts"/> cap, and
    /// repositions everyone whenever a toast appears or closes.
    /// </summary>
    /// <remarks>
    /// This is the glue layer: it reads the screen working area and moves windows, but delegates
    /// the actual stacking math to <see cref="ToastLayout"/> (which is pure and unit-tested).
    /// <see cref="ToastNotification"/> handles a single toast's window, painting, and fade; the
    /// manager handles the collection. Only ever touched on the UI thread.
    /// </remarks>
    internal static class ToastManager
    {
        // Live toasts in oldest-first order; this list also defines the stacking order.
        private static readonly List<ToastNotification> LiveToasts = new List<ToastNotification>();

        // The single anchored (point-positioned) toast, if any. It does NOT participate in the
        // stack: it never appears in LiveToasts and is never reflowed.
        private static ToastNotification _anchored;

        /// <summary>Registers a freshly shown toast, evicts overflow, and re-lays out the stack.</summary>
        public static void Add(ToastNotification toast)
        {
            LiveToasts.Add(toast);

            // Cap the stack: evict the oldest toasts so it never overflows the screen. Iterate by
            // index 0 because CloseNow() calls back into Remove(), shrinking the list.
            while (LiveToasts.Count > ToastDefaults.MaxVisibleToasts)
            {
                LiveToasts[0].CloseNow();
            }

            Reflow();
        }

        /// <summary>
        /// Registers a freshly shown anchored toast as the single one, closing any previous anchored
        /// toast. Anchored toasts are standalone — they are not stacked or reflowed.
        /// </summary>
        public static void RegisterAnchored(ToastNotification toast)
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

        /// <summary>Drops a closed toast: clears the anchored slot, or removes from the stack and reflows.</summary>
        public static void Remove(ToastNotification toast)
        {
            if (ReferenceEquals(_anchored, toast))
            {
                _anchored = null;
                return;
            }

            if (LiveToasts.Remove(toast))
            {
                Reflow();
            }
        }

        private static void Reflow()
        {
            var area = Screen.PrimaryScreen.WorkingArea;

            var boxes = new ToastBox[LiveToasts.Count];
            for (int i = 0; i < LiveToasts.Count; i++)
            {
                var toast = LiveToasts[i];
                boxes[i] = new ToastBox
                {
                    Position = toast.Position,
                    Width = toast.Width,
                    Height = toast.Height
                };
            }

            Point[] points = ToastLayout.Arrange(boxes, area, ToastDefaults.Margin, ToastDefaults.Gap);

            for (int i = 0; i < LiveToasts.Count; i++)
            {
                LiveToasts[i].MoveTo(points[i].X, points[i].Y);
            }
        }
    }
}
