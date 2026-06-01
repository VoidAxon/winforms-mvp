namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages
{
    /// <summary>
    /// Message published when the order total changes.
    /// </summary>
    public class TotalChangedMessage
    {
        public decimal OldTotal { get; set; }
        public decimal NewTotal { get; set; }

        public TotalChangedMessage(decimal oldTotal, decimal newTotal)
        {
            OldTotal = oldTotal;
            NewTotal = newTotal;
        }
    }
}
