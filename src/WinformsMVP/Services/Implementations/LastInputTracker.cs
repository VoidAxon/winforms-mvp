using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Tracks whether the most recent user input on the UI thread was keyboard or mouse, by
    /// watching the raw input messages through <see cref="Application.AddMessageFilter"/>
    /// (managed WinForms API — no interop). This is the app-level equivalent of the mechanism
    /// Windows itself uses to decide whether to draw keyboard focus cues.
    /// </summary>
    /// <remarks>
    /// Action handlers run synchronously inside the processing of the input message that
    /// triggered them, so at resolve time "the last input" IS the input that triggered the
    /// current action: Ctrl+S sees its own WM_KEYDOWN, a menu click sees its WM_LBUTTONDOWN.
    /// Mouse moves and the wheel are deliberately not tracked — parking or scrolling the mouse
    /// must not flip the modality. The filter never swallows messages.
    /// </remarks>
    internal static class LastInputTracker
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONDBLCLK = 0x0206;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        private static bool _installed;
        private static bool? _lastInputWasKeyboard;

        /// <summary>
        /// <c>true</c> = last input was a key press; <c>false</c> = a mouse button press;
        /// <c>null</c> = nothing observed yet (filter freshly installed).
        /// </summary>
        public static bool? LastInputWasKeyboard
        {
            get
            {
                EnsureInstalled();
                return _lastInputWasKeyboard;
            }
        }

        /// <summary>Installs the message filter once. Call on the UI thread.</summary>
        public static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;
            Application.AddMessageFilter(new InputModalityFilter());
        }

        private sealed class InputModalityFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                        _lastInputWasKeyboard = true;
                        break;
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONDBLCLK:
                    case WM_RBUTTONDOWN:
                    case WM_RBUTTONDBLCLK:
                    case WM_MBUTTONDOWN:
                    case WM_XBUTTONDOWN:
                        _lastInputWasKeyboard = false;
                        break;
                }
                return false;   // observe only; never swallow
            }
        }
    }
}
