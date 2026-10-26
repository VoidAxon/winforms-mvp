namespace Kibinago.Services
{
    /// <summary>
    /// Screen corner that toast notifications appear in and stack from.
    /// Top corners stack downward; bottom corners stack upward.
    /// </summary>
    public enum ToastPosition
    {
        /// <summary>Top-left corner of the working area.</summary>
        TopLeft,
        /// <summary>Top-right corner of the working area.</summary>
        TopRight,
        /// <summary>Bottom-left corner of the working area.</summary>
        BottomLeft,
        /// <summary>Bottom-right corner of the working area.</summary>
        BottomRight
    }
}
