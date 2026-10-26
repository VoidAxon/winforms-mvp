using System;
using System.Windows.Forms;

namespace Kibinago.Services
{
    /// <summary>Finds a UI thread through an open form, for services called from a worker thread.</summary>
    internal static class OpenForms
    {
        /// <summary>An open form whose handle exists (its thread runs a message loop), or <c>null</c>.</summary>
        public static Form FindOne()
        {
            try
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form.IsHandleCreated && !form.IsDisposed) return form;
                }
            }
            catch (InvalidOperationException)
            {
                // OpenForms was modified while enumerating from a worker thread.
            }
            return null;
        }
    }
}
