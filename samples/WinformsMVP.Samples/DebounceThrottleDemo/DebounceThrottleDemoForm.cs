using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.DebounceThrottleDemo
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
    public sealed class DebounceThrottleDemoForm : Form
    {
        private readonly Debouncer _searchDebouncer;
        private readonly Throttler _sliderThrottler;

        private int _rawKeystrokes;
        private int _debouncedSearches;
        private int _rawSliderEvents;
        private int _throttledUpdates;

        private TextBox _searchBox;
        private Label _rawKeyLabel;
        private Label _debouncedLabel;
        private Label _searchTermLabel;
        private TrackBar _slider;
        private Label _rawSliderLabel;
        private Label _throttledLabel;

        public DebounceThrottleDemoForm()
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // This demo has no pending state worth keeping, so we just dispose. If you needed the
            // last pending invocation to run on close (e.g. a deferred save), you would call
            // _searchDebouncer.Flush() on the UI thread *before* Dispose(); Dispose alone discards it.
            _searchDebouncer.Dispose();
            _sliderThrottler.Dispose();
            base.OnFormClosed(e);
        }

        private void InitializeComponent()
        {
            Text = "Debounce / Throttle Demo";
            ClientSize = new Size(520, 380);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // ---- Debounce section ------------------------------------------------
            var debounceTitle = new Label
            {
                Text = "Debounce — type quickly; the search fires only after you pause (300 ms)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(16, 16),
                AutoSize = true
            };

            _searchBox = new TextBox
            {
                Location = new Point(16, 44),
                Width = 488
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _rawKeystrokes++;
                _rawKeyLabel.Text = "Raw TextChanged events: " + _rawKeystrokes;
                _searchDebouncer.Signal();
            };

            _rawKeyLabel = new Label { Location = new Point(16, 76), AutoSize = true, Text = "Raw TextChanged events: 0" };
            _debouncedLabel = new Label { Location = new Point(16, 98), AutoSize = true, ForeColor = Color.FromArgb(0, 120, 215), Text = "Debounced searches: 0" };
            _searchTermLabel = new Label { Location = new Point(16, 120), AutoSize = true, ForeColor = Color.DimGray, Text = "Searching for: \"\"" };

            // ---- Throttle section ------------------------------------------------
            var throttleTitle = new Label
            {
                Text = "Throttle — drag the slider; updates fire at most once every 150 ms",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(16, 176),
                AutoSize = true
            };

            _slider = new TrackBar
            {
                Location = new Point(12, 204),
                Width = 492,
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10
            };
            _slider.ValueChanged += (s, e) =>
            {
                _rawSliderEvents++;
                _rawSliderLabel.Text = "Raw ValueChanged events: " + _rawSliderEvents;
                _sliderThrottler.Signal();
            };

            _rawSliderLabel = new Label { Location = new Point(16, 252), AutoSize = true, Text = "Raw ValueChanged events: 0" };
            _throttledLabel = new Label { Location = new Point(16, 274), AutoSize = true, ForeColor = Color.FromArgb(16, 137, 62), Text = "Throttled updates: 0" };

            var hint = new Label
            {
                Text = "Both rate limiters capture this form's SynchronizationContext, so their callbacks\n" +
                       "run on the UI thread automatically — the same code works under WPF.",
                Location = new Point(16, 316),
                AutoSize = true,
                ForeColor = Color.DimGray
            };

            Controls.AddRange(new Control[]
            {
                debounceTitle, _searchBox, _rawKeyLabel, _debouncedLabel, _searchTermLabel,
                throttleTitle, _slider, _rawSliderLabel, _throttledLabel, hint
            });
        }
    }
}
