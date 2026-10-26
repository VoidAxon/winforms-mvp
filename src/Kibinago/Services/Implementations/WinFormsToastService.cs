using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Logging;

namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// The default toast service: corner-stacked and anchored toasts drawn as layered popups.
    /// Registered as <see cref="IWinFormsToastService"/>; the <see cref="IToastService"/> registration
    /// forwards to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Toast windows and their timers must live on a thread with a message loop. The service binds
    /// to the UI thread — the thread it is created on, or the first thread with a WinForms
    /// <see cref="SynchronizationContext"/> that calls it — and every later call from another
    /// thread is posted there. Before any UI thread is known, it falls back to an open form's
    /// thread. When there is no UI thread at all (before the message loop starts, after the
    /// application closed) or the service is disposed, the toast is dropped and a warning is
    /// logged: a toast is non-critical feedback and never fails the caller.
    /// </para>
    /// <para>
    /// The service binds once, to the first such thread, and stays there. In an application with
    /// more than one UI thread, or with a worker thread that owns WinForms controls (and therefore a
    /// WinForms context) but runs no message loop, create and first use the service on the main
    /// UI thread — the default registration resolves it lazily, typically from a presenter there.
    /// </para>
    /// <para>
    /// Per-call <see cref="ToastOptions"/> (font included) are copied before <c>Show</c> returns,
    /// so a caller may dispose its font immediately.
    /// </para>
    /// </remarks>
    public sealed class WinFormsToastService : IWinFormsToastService, IDisposable
    {
        private readonly ToastManager _manager;
        private readonly Font _ownedDefaultFont;   // created here when ToastHostOptions.Font is null
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private SynchronizationContext _ui;
        private int _uiThreadId;
        private volatile bool _disposed;

        /// <param name="options">Defaults; validated and copied here (later edits have no effect).</param>
        /// <param name="renderer">App-wide custom painter, or <c>null</c> to paint by <see cref="ToastHostOptions.Style"/>.</param>
        /// <param name="loggerFactory">Receives warnings about dropped toasts and failures.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">When an option value cannot work.</exception>
        public WinFormsToastService(ToastHostOptions options, ToastRenderer renderer = null, ILoggerFactory loggerFactory = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var settings = options.Copy();
            settings.Validate();

            _ownedDefaultFont = settings.Font == null ? new Font("Segoe UI", 10f) : null;
            _manager = new ToastManager(settings, settings.Font ?? _ownedDefaultFont, renderer);
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(typeof(WinFormsToastService));
            TryBindToCurrentThread();
        }

        internal ToastManager Manager => _manager;

        /// <summary>
        /// Finds an open form whose thread can host toasts before a UI thread is bound. Replaceable so
        /// tests are not affected by forms other tests have open (<see cref="Application.OpenForms"/>
        /// is process-wide).
        /// </summary>
        internal Func<Form> OpenFormLocator { get; set; } = OpenForms.FindOne;

        /// <summary>The UI thread this service is bound to, or <c>null</c> before it knows one.</summary>
        internal int? BoundUiThreadId
        {
            get { lock (_gate) { return _ui == null ? (int?)null : _uiThreadId; } }
        }

        /// <inheritdoc />
        public void Show(string text, ToastType type, int? duration = null)
            => Dispatch(text, type, new ToastOptions { Duration = duration }, anchor: null);

        /// <inheritdoc />
        public void Show(string text, ToastType type, ToastOptions options)
            => Dispatch(text, type, options?.CopyOwningFont(), anchor: null);

        /// <inheritdoc />
        public void ShowAnchored(string text, ToastType type, Point anchor, ToastOptions options = null)
            => Dispatch(text, type, options?.CopyOwningFont(), anchor);

        /// <summary>
        /// Stops showing toasts: later calls are dropped. Called on the UI thread, it also closes the
        /// toasts on screen; from another thread their closing is posted to the UI thread.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            SynchronizationContext ui;
            int uiThread;
            lock (_gate) { ui = _ui; uiThread = _uiThreadId; }

            if (ui == null || Thread.CurrentThread.ManagedThreadId == uiThread) Shutdown();
            else
            {
                try { ui.Post(_ => Shutdown(), null); }
                catch (Exception ex) { _logger.Log(LogLevel.Warning, ex, "Toast service: could not close toasts on the UI thread"); }
            }
        }

        private void Shutdown()
        {
            _manager.CloseAll();
            _ownedDefaultFont?.Dispose();
        }

        private void TryBindToCurrentThread()
        {
            var current = SynchronizationContext.Current;
            if (!(current is WindowsFormsSynchronizationContext)) return;
            Bind(current);
        }

        private void Bind(SynchronizationContext ui)
        {
            lock (_gate)
            {
                if (_ui != null) return;
                _ui = ui;
                _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        // `owned` is this service's copy: its font (if any) must reach a toast or be disposed here.
        private void Dispatch(string text, ToastType type, ToastOptions owned, Point? anchor)
        {
            if (_disposed)
            {
                Drop(owned, "the toast service is disposed");
                return;
            }

            TryBindToCurrentThread();

            SynchronizationContext ui;
            int uiThread;
            lock (_gate) { ui = _ui; uiThread = _uiThreadId; }

            if (ui != null)
            {
                if (Thread.CurrentThread.ManagedThreadId == uiThread)
                {
                    ShowNow(text, type, owned, anchor);
                    return;
                }
                try
                {
                    ui.Post(_ => ShowNow(text, type, owned, anchor), null);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Toast service: could not post a toast to the UI thread; dropped");
                    owned?.Font?.Dispose();
                }
                return;
            }

            // No UI thread bound yet (e.g. the first toast comes from a worker thread): use the
            // thread of an open form. Never create the toast on this (possibly loop-less) thread.
            var host = OpenFormLocator();
            if (host == null)
            {
                Drop(owned, "no UI thread is available yet");
                return;
            }
            try
            {
                host.BeginInvoke((Action)(() =>
                {
                    // Now on the form's UI thread: bind to it so later calls (and Dispose) marshal
                    // here. Without an installed context (AutoInstall disabled) make one: created
                    // on this thread, it posts to this thread.
                    TryBindToCurrentThread();
                    if (BoundUiThreadId == null) Bind(new WindowsFormsSynchronizationContext());
                    ShowNow(text, type, owned, anchor);
                }));
            }
            catch (InvalidOperationException ex)
            {
                // The form's handle was destroyed between the lookup and the call.
                _logger.Log(LogLevel.Warning, ex, "Toast service: the UI thread went away; toast dropped");
                owned?.Font?.Dispose();
            }
        }

        private void ShowNow(string text, ToastType type, ToastOptions owned, Point? anchor)
        {
            if (_disposed)
            {
                Drop(owned, "the toast service is disposed");
                return;
            }

            ToastNotification toast;
            try
            {
                toast = new ToastNotification(text, type, owned, _manager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toast service: could not create a toast");
                owned?.Font?.Dispose();
                return;
            }

            try
            {
                if (anchor.HasValue) toast.ShowAnchored(anchor.Value);
                else toast.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toast service: could not show a toast");
                toast.CloseNow();   // releases its window, timers and owned font
            }
        }

        private void Drop(ToastOptions owned, string reason)
        {
            _logger.LogWarning("Toast service: toast dropped because {Reason}", reason);
            owned?.Font?.Dispose();
        }
    }
}
