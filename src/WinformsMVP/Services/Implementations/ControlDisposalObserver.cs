using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// The WinForms side of <see cref="IViewDisposalObserver"/>: a control-backed view is disposed when
    /// its <see cref="Control.Disposed"/> event fires. Found by Core through <c>ViewDisposal</c> (by name).
    /// </summary>
    internal sealed class ControlDisposalObserver : IViewDisposalObserver
    {
        internal static readonly ControlDisposalObserver Instance = new ControlDisposalObserver();

        public IDisposable ObserveDisposal(IViewBase view, Action onDisposed)
        {
            if (onDisposed == null) throw new ArgumentNullException(nameof(onDisposed));
            if (!(view is Control control)) return Disposable.Create(() => { });

            EventHandler handler = null;
            handler = (s, e) =>
            {
                control.Disposed -= handler;
                onDisposed();
            };
            control.Disposed += handler;
            return Disposable.Create(() => control.Disposed -= handler);
        }

        public bool IsDisposed(IViewBase view) => view is Control control && (control.IsDisposed || control.Disposing);
    }
}
