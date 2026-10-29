using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Views;

namespace Kibinago.Services
{
    /// <summary>
    /// The WinForms side of <see cref="IViewDisposalObserver"/>: a control-backed view is disposed when
    /// its <see cref="System.ComponentModel.Component.Disposed"/> event fires. Found by Core through <c>ViewDisposal</c> (by name).
    /// </summary>
    /// <remarks>
    /// Presenter tests may load this assembly without the Windows Desktop runtime (a test project that
    /// references the app but runs on Linux / without System.Windows.Forms). So a view is recognized as
    /// a control by its base type names first, and only then does code that references
    /// <see cref="Control"/> run — in separate, non-inlined methods, so plain fake views never make the
    /// runtime load System.Windows.Forms.
    /// </remarks>
    internal sealed class ControlDisposalObserver : IViewDisposalObserver
    {
        internal static readonly ControlDisposalObserver Instance = new ControlDisposalObserver();

        private const string ControlTypeName = "System.Windows.Forms.Control";
        private const string FormTypeName = "System.Windows.Forms.Form";

        public IDisposable ObserveDisposal(IView view, Action onDisposed)
        {
            if (onDisposed == null) throw new ArgumentNullException(nameof(onDisposed));
            return IsControl(view) ? ObserveControl(view, onDisposed) : Disposable.Create(() => { });
        }

        public bool IsDisposed(IView view) => IsControl(view) && IsControlDisposed(view);

        public bool IsWindow(IView view) => Derives(view, FormTypeName);

        private static bool IsControl(object view) => Derives(view, ControlTypeName);

        private static bool Derives(object view, string typeName)
        {
            for (var type = view?.GetType(); type != null; type = type.BaseType)
                if (type.FullName == typeName) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDisposable ObserveControl(object view, Action onDisposed)
        {
            var control = (Control)view;
            EventHandler handler = null;
            handler = (s, e) =>
            {
                control.Disposed -= handler;
                onDisposed();
            };
            control.Disposed += handler;
            return Disposable.Create(() => control.Disposed -= handler);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsControlDisposed(object view)
        {
            var control = (Control)view;
            return control.IsDisposed || control.Disposing;
        }
    }
}
