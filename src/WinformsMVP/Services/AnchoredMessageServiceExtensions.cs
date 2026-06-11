using WinformsMVP.Common;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Convenience overloads for <see cref="IAnchoredMessageService"/>. The interface itself
    /// carries only the two full-parameter methods; everything here forwards to them.
    /// </summary>
    public static class AnchoredMessageServiceExtensions
    {
        /// <summary>Shows an anchored toast with default options.</summary>
        public static void ShowToast(this IAnchoredMessageService service, string text, ToastType type)
            => service.ShowToast(text, type, null);

        /// <summary>Shows an anchored information message.</summary>
        public static void ShowInfo(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Information);

        /// <summary>Shows an anchored warning message.</summary>
        public static void ShowWarning(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Warning);

        /// <summary>Shows an anchored error message.</summary>
        public static void ShowError(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.Ok, MessageIcon.Error);

        /// <summary>Shows an anchored Yes/No confirmation. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.YesNo, MessageIcon.Question) == ConfirmResult.Yes;

        /// <summary>Shows an anchored OK/Cancel confirmation. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.OkCancel, MessageIcon.Question) == ConfirmResult.Yes;

        /// <summary>Shows an anchored Yes/No/Cancel confirmation and returns the raw result.</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IAnchoredMessageService service, string text, string caption = "")
            => service.ShowMessage(text, caption, MessageButtons.YesNoCancel, MessageIcon.Question);
    }
}
