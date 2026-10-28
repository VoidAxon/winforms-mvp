using System;
using System.Threading;
using System.Windows.Forms;
using Kibinago.Logging;
using Kibinago.Views;

namespace Kibinago.Services
{
    /// <summary>
    /// The WinForms <see cref="IBusyIndicator"/>. While a view is busy its window — the view's form, an MDI
    /// child on its own — shows the wait cursor and takes no input: mouse buttons, wheel and keys meant for
    /// it are dropped (a message filter; the controls keep their look), and input queued while synchronous
    /// work held the UI thread is dropped too. The title bar, Alt+F4, dialogs and other windows stay usable.
    /// With an overlay renderer (<c>mvp.Busy.UseOverlay()</c>) the view is also covered, after
    /// <see cref="BusyOptions.Delay"/>, by windows of their own (never controls of the view, never in
    /// <see cref="Application.OpenForms"/>).
    /// </summary>
    /// <remarks>
    /// Call <see cref="Begin"/> on the view's UI thread (presenters do, through <c>ShowBusy</c>); from another
    /// thread it is posted there (through the nearest control with a handle) and takes effect shortly after. The returned object may be disposed on any
    /// thread. A request also ends when its view or window is closed or disposed, or when the UI thread's
    /// message loop ends.
    /// </remarks>
    public sealed class WinFormsBusyIndicator : IBusyIndicator
    {
        private readonly int _delay;
        private readonly BusyRenderer _overlay;
        private readonly ILogger _logger;

        /// <summary>Creates the indicator.</summary>
        /// <param name="options">The settings (copied); <c>null</c> for the defaults.</param>
        /// <param name="overlay">Paints the overlay; <c>null</c> for the wait cursor only.</param>
        /// <param name="loggerFactory">Receives overlay failures; <c>null</c> for none.</param>
        public WinFormsBusyIndicator(BusyOptions options = null, BusyRenderer overlay = null, ILoggerFactory loggerFactory = null)
        {
            var copy = (options ?? new BusyOptions()).Copy();
            copy.Validate();
            _delay = copy.Delay;
            _overlay = overlay;
            _logger = loggerFactory?.CreateLogger(typeof(WinFormsBusyIndicator));
        }

        /// <inheritdoc />
        public IDisposable Begin(IViewBase view, string message)
        {
            if (!(view is Control control) || control.IsDisposed) return new Token(null);
            var token = new Token(this);
            Action start = () => token.Start(control, message);

            // The UI thread is the one owning the nearest handle (the view's own may not exist yet: a page of a
            // tab not shown, a form still being set up).
            var anchor = NearestWithHandle(control);
            if (anchor != null)
            {
                if (anchor.InvokeRequired) anchor.BeginInvoke(start);
                else start();
            }
            else if (SynchronizationContext.Current is WindowsFormsSynchronizationContext || Application.MessageLoop)
            {
                start();   // the UI thread setting the view up (OnInitializeAsync before the window shows)
            }
            else
            {
                // Not known to be the UI thread and no handle anywhere: start when the root's handle is
                // created, on the thread that creates it. Checked again after subscribing, in case it was
                // created meanwhile (the token starts once).
                var root = control;
                while (root.Parent != null) root = root.Parent;
                EventHandler created = null;
                created = (s, e) => { root.HandleCreated -= created; start(); };
                root.HandleCreated += created;
                var late = NearestWithHandle(control);
                if (late != null)
                {
                    root.HandleCreated -= created;
                    try { late.BeginInvoke(start); }
                    catch (InvalidOperationException) { }   // its handle went away meanwhile: the request ends with the view
                }
            }
            return token;
        }

        private static Control NearestWithHandle(Control control)
        {
            while (control != null && !control.IsHandleCreated) control = control.Parent;
            return control;
        }

        private sealed class Token : IDisposable
        {
            private WinFormsBusyIndicator _owner;
            private BusyCoordinator _coordinator;
            private BusyCoordinator.Request _request;
            private SynchronizationContext _context;
            private Control _marshal;   // posts the release when the UI thread has no context
            private Thread _thread;
            private int _started;
            private int _ended;

            public Token(WinFormsBusyIndicator owner)
            {
                _owner = owner;
                if (owner == null) _ended = 1;
            }

            // On the UI thread.
            public void Start(Control view, string message)
            {
                if (Interlocked.Exchange(ref _started, 1) != 0) return;   // reached by two routes (handle created meanwhile)
                if (Volatile.Read(ref _ended) != 0 || view.IsDisposed) return;
                _coordinator = BusyCoordinator.ForCurrentThread();
                _request = _coordinator.Begin(view, message, _owner._overlay, _owner._delay, _owner._logger);
                Volatile.Write(ref _context, SynchronizationContext.Current);
                Volatile.Write(ref _marshal, view);
                Volatile.Write(ref _thread, Thread.CurrentThread);   // last: Dispose on another thread now posts the release
                if (Volatile.Read(ref _ended) != 0) Release();   // disposed on another thread while starting
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _ended, 1) != 0) return;
                // Snapshots: a Start finishing on the UI thread may release (and clear the fields) meanwhile.
                var thread = Volatile.Read(ref _thread);
                var context = Volatile.Read(ref _context);
                var marshal = Volatile.Read(ref _marshal);
                if (thread == null) return;                           // not started yet: Start sees _ended
                if (Thread.CurrentThread == thread) Release();
                else if (context != null)
                {
                    try { context.Post(_ => Release(), null); }
                    catch (InvalidOperationException) { }             // the UI thread is gone; its exit ended the request
                }
                else if (marshal != null && !PostRelease(marshal))
                {
                    // No context on the UI thread and no handle yet (a form not shown): release when the root's
                    // handle is created, on the UI thread creating it. Checked again after subscribing, in case
                    // it was created meanwhile (a second Release does nothing).
                    var root = marshal;
                    while (root.Parent != null) root = root.Parent;
                    EventHandler created = null;
                    created = (s, e) => { root.HandleCreated -= created; Release(); };
                    root.HandleCreated += created;
                    if (PostRelease(marshal)) root.HandleCreated -= created;
                }
            }

            // Posts the release through the view or its nearest handle; false when none has a handle. A view
            // disposed meanwhile needs none: its disposal ends the request.
            private bool PostRelease(Control view)
            {
                var anchor = NearestWithHandle(view);
                if (anchor == null) return view.IsDisposed;
                if (anchor.IsDisposed) return true;
                try { anchor.BeginInvoke((Action)Release); }
                catch (InvalidOperationException) { }                 // its handle went away meanwhile: its disposal ends the request
                return true;
            }

            private void Release()
            {
                var coordinator = _coordinator;
                var request = _request;
                _coordinator = null;
                _request = null;
                _owner = null;
                Volatile.Write(ref _context, null);
                Volatile.Write(ref _marshal, null);
                if (coordinator != null && request != null) coordinator.End(request);
            }
        }
    }
}
