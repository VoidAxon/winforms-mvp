using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinFormsCloseReason = System.Windows.Forms.CloseReason;
using MvpCloseReason = WinformsMVP.Common.CloseReason;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Bridges a WinForms <see cref="Form"/> close to the framework's
    /// <see cref="IWindowView.Closing"/> abstraction, for windows that are NOT shown through
    /// <see cref="IWindowNavigator"/> — e.g. an application shell created with
    /// <c>Application.Run(new MainForm())</c>, or a legacy form being migrated incrementally.
    /// </summary>
    /// <remarks>
    /// Windows shown via <see cref="IWindowNavigator"/> get this bridge wired automatically and do
    /// not need this type. It covers only the Pull direction (reason mapping + forwarding the veto).
    /// Skipping the gate for Presenter-initiated closes is a navigator-internal concern; code that
    /// owns its own close calls is responsible for finalizing dirty state on that path itself.
    /// </remarks>
    public static class WindowClosingBridge
    {
        /// <summary>
        /// Maps WinForms <see cref="WinFormsCloseReason"/> to the framework abstraction
        /// <see cref="MvpCloseReason"/>. This is the single source of the mapping;
        /// <c>WindowNavigator</c> delegates here.
        /// </summary>
        public static MvpCloseReason MapCloseReason(WinFormsCloseReason reason)
        {
            switch (reason)
            {
                case WinFormsCloseReason.UserClosing:
                    return MvpCloseReason.Normal;
                case WinFormsCloseReason.WindowsShutDown:
                    return MvpCloseReason.SystemShutdown;
                case WinFormsCloseReason.TaskManagerClosing:
                    return MvpCloseReason.TaskManager;
                case WinFormsCloseReason.FormOwnerClosing:
                case WinFormsCloseReason.MdiFormClosing:
                    return MvpCloseReason.ParentClosing;
                case WinFormsCloseReason.ApplicationExitCall:
                    return MvpCloseReason.Normal;
                default:
                    return MvpCloseReason.Unknown;
            }
        }

        /// <summary>
        /// Maps the reason, raises <see cref="IWindowView.OnClosing"/> so the subscribed Presenter
        /// can run its dirty check, and applies the resulting veto back to the WinForms event.
        /// Call from a Form's <c>OnFormClosing</c> override (or a <c>FormClosing</c> handler) when
        /// the window is not managed by <see cref="IWindowNavigator"/>.
        /// </summary>
        public static void ForwardClosing(IWindowView view, FormClosingEventArgs e)
        {
            var args = new WindowClosingEventArgs(MapCloseReason(e.CloseReason));
            view.OnClosing(args);
            if (args.Cancel) e.Cancel = true;
        }
    }
}
