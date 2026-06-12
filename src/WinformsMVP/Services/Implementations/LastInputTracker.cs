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
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_RBUTTONDBLCLK = 0x0206;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        private static bool _installed;
        private static bool? _lastInputWasKeyboard;
        private static bool _inputLive;

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

        /// <summary>
        /// Whether the synchronous processing chain of the last input message is still running.
        /// Set on every input message; cleared by <see cref="Application.Idle"/> (which fires
        /// when the message queue empties, i.e. the input's dispatch has fully returned). When
        /// <c>false</c>, the current code path was started by something other than user input —
        /// a timer, an async continuation, plain code — and the last observed modality is a
        /// stale leftover that must not be used for anchoring.
        /// </summary>
        public static bool IsInputLive
        {
            get
            {
                EnsureInstalled();
                return _inputLive;
            }
        }

        /// <summary>Installs the message filter once. Call on the UI thread.</summary>
        public static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;
            Application.AddMessageFilter(new InputModalityFilter());
            Application.Idle += OnApplicationIdle;
        }

        private static void OnApplicationIdle(object sender, System.EventArgs e)
        {
            _inputLive = false;
        }

        private sealed class InputModalityFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                // UP messages matter too: Click handlers run while processing BUTTONUP/KEYUP
                // (a held button would otherwise let Idle clear the live flag between down and up).
                switch (m.Msg)
                {
                    case WM_KEYDOWN:
                    case WM_KEYUP:
                    case WM_SYSKEYDOWN:
                    case WM_SYSKEYUP:
                        _lastInputWasKeyboard = true;
                        _inputLive = true;
                        break;
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONUP:
                    case WM_LBUTTONDBLCLK:
                    case WM_RBUTTONDOWN:
                    case WM_RBUTTONUP:
                    case WM_RBUTTONDBLCLK:
                    case WM_MBUTTONDOWN:
                    case WM_MBUTTONUP:
                    case WM_XBUTTONDOWN:
                    case WM_XBUTTONUP:
                        _lastInputWasKeyboard = false;
                        _inputLive = true;
                        break;
                }
                return false;   // observe only; never swallow
            }
        }
    }
}
