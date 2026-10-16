using System;
using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Runs view-side work on the UI thread when a caller may be on a worker thread. Uses the
    /// first open form with a handle as the marshalling target; with no such form there is no
    /// UI thread to protect and the action runs inline.
    /// </summary>
    internal static class UiThread
    {
        internal static void Run(Action action)
        {
            Form host = null;
            try
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form.IsHandleCreated && !form.IsDisposed) { host = form; break; }
                }
            }
            catch (InvalidOperationException)
            {
                // OpenForms was modified while enumerating from a worker thread; fall through.
            }

            if (host != null && host.InvokeRequired)
            {
                try
                {
                    host.BeginInvoke(action);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Handle destroyed between the check and the call; run inline below.
                }
            }
            action();
        }
    }
}
