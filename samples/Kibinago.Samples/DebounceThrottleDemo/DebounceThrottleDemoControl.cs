using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Samples.DebounceThrottleDemo
{
    /// <summary>
    /// View-driven demo (no presenter) contrasting raw event frequency with <see cref="Debouncer"/>
    /// and <see cref="Throttler"/>.
    /// </summary>
    /// <remarks>
    /// Both rate limiters are constructed on the UI thread, so they capture the
    /// WindowsFormsSynchronizationContext and marshal their callbacks back onto it automatically.
    /// That is why the callbacks below touch labels directly with no <c>Invoke</c> — and why the
    /// exact same code would work under WPF (where the captured context is a
    /// DispatcherSynchronizationContext instead).
    /// </remarks>
    public sealed partial class DebounceThrottleDemoControl : UserControl, IView   // a view-only demo: no presenter, but a view the shell can host
    {
        private readonly Debouncer _searchDebouncer;
        private readonly Throttler _sliderThrottler;

        private int _rawKeystrokes;
        private int _debouncedSearches;
        private int _rawSliderEvents;
        private int _throttledUpdates;

        public DebounceThrottleDemoControl()
        {
            InitializeComponent();

            // Debounce: fire once, 300 ms after the user stops typing — the "search as you type" idiom.
            _searchDebouncer = RateLimit.Debounce(300, OnSearchSettled,
                onError: ex => MessageBox.Show(ex.Message, "Debounce error"));

            // Throttle: while dragging the slider, update at most once every 150 ms (leading + trailing).
            _sliderThrottler = RateLimit.Throttle(150, OnThrottledSliderUpdate,
                onError: ex => MessageBox.Show(ex.Message, "Throttle error"));
        }

        private void OnSearchSettled()
        {
            _debouncedSearches++;
            _debouncedLabel.Text = "Debounced searches: " + _debouncedSearches;
            _searchTermLabel.Text = "Searching for: \"" + _searchBox.Text + "\"";
        }

        private void OnThrottledSliderUpdate()
        {
            _throttledUpdates++;
            _throttledLabel.Text = "Throttled updates: " + _throttledUpdates;
        }

        protected override void Dispose(bool disposing)
        {
            // Released with the control (here: when its tab's category is left). This demo has no pending
            // state worth keeping, so we just dispose. If you needed the last pending invocation to run
            // (e.g. a deferred save), you would call _searchDebouncer.Flush() on the UI thread *before*
            // Dispose(); Dispose alone discards it.
            if (disposing)
            {
                _searchDebouncer.Dispose();
                _sliderThrottler.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
