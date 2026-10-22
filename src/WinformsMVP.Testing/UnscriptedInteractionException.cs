using System;

namespace WinformsMVP.Testing
{
    /// <summary>
    /// Thrown by a fake when the presenter asks something the test did not script — a confirmation,
    /// a file dialog, a modal window — so an unexpected question never silently takes a branch.
    /// Script the answer, or switch the fake to a permissive default
    /// (<c>AnswerUnscripted</c> / <c>CancelUnscripted</c>) when the test does not care.
    /// </summary>
    public class UnscriptedInteractionException : InvalidOperationException
    {
        /// <summary>Creates the exception with a message naming what was asked.</summary>
        public UnscriptedInteractionException(string message) : base(message) { }
    }
}
