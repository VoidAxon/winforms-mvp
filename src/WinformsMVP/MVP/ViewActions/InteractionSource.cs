using System.ComponentModel;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Ambient fact about the in-flight action dispatch: the component whose UI event triggered
    /// it. Set by <see cref="ViewActionBinder"/> around handler invocation (save/restore, so
    /// nested dispatches stay correct) and read by the anchored-feedback service to anchor
    /// keyboard-triggered feedback at the activated control — the focused control alone is not
    /// enough, because e.g. a button mnemonic (Alt+D) clicks the button without moving focus.
    /// </summary>
    /// <remarks>
    /// This records a fact about the input event only; what to do with it (geometry, placement)
    /// is the consumer's decision. UI-thread only, by the same contract as the binder itself.
    /// Outside a binder-initiated dispatch the value is <c>null</c>.
    /// </remarks>
    internal static class InteractionSource
    {
        /// <summary>The component that triggered the in-flight dispatch, or <c>null</c>.</summary>
        public static Component Current { get; private set; }

        /// <summary>Sets the current trigger and returns the previous value for restoring.</summary>
        public static Component Swap(Component trigger)
        {
            var previous = Current;
            Current = trigger;
            return previous;
        }
    }
}
