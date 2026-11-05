namespace Kibinago.Presenters
{
    /// <summary>
    /// Told by the close host how an attempt the gate approved ended. A window whose tree has page stacks keeps
    /// them reserved from the approval until the attempt settles, so no page can be pushed or jumped to between
    /// "approved" and "closed". Separate from <see cref="ICloseParticipant"/> so hosts without page stacks need
    /// nothing new.
    /// </summary>
    internal interface ICloseAttemptSettler
    {
        /// <summary>True while an approved attempt holds page stacks (only windows with page stacks): the host
        /// then reports how the attempt ended. False otherwise, so hosts do nothing extra.</summary>
        bool HoldsApprovedClose { get; }

        /// <summary>
        /// <paramref name="closed"/> = <c>true</c>: the window closed (the reservation stays; the stacks end with
        /// the presenter). <c>false</c>: the approved attempt was abandoned (another FormClosing handler cancelled
        /// it, or it was only a permission query) — the stacks are released. Does nothing when no approved
        /// attempt holds them.
        /// </summary>
        void SettleCloseAttempt(bool closed);
    }
}
