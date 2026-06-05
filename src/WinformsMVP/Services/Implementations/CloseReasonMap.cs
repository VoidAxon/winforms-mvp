using WinFormsCloseReason = System.Windows.Forms.CloseReason;
using MvpCloseReason = WinformsMVP.Common.CloseReason;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Single source of the WinForms-to-framework close-reason mapping. Internal because the
    /// only callers are the <see cref="WindowCloseController"/> and <c>WindowNavigator</c>;
    /// presenters see only the framework <see cref="MvpCloseReason"/>.
    /// </summary>
    internal static class CloseReasonMap
    {
        public static MvpCloseReason From(WinFormsCloseReason reason)
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
    }
}
