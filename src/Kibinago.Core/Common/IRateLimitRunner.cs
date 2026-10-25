namespace Kibinago.Common
{
    /// <summary>What <see cref="Debouncer"/> and <see cref="Throttler"/> need from a runner (sync or async action).</summary>
    internal interface IRateLimitRunner
    {
        /// <summary>A fire from the timer or a leading edge: runs on the context, coalescing with a run in flight.</summary>
        void Request();

        /// <summary>Flush: runs now on the calling thread.</summary>
        void RunInlineNow();

        /// <summary>Disposal: cancels a run in flight, if it can be cancelled.</summary>
        void CancelInFlight();
    }
}
