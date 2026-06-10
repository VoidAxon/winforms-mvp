namespace WinformsMVP.Common
{
    /// <summary>
    /// Selects a built-in toast appearance. Map to a renderer via the framework's resolver;
    /// a custom <see cref="ToastRenderer"/> set on <c>ToastOptions.Renderer</c> or
    /// <c>ToastDefaults.Renderer</c> overrides the style.
    /// </summary>
    public enum ToastStyle
    {
        /// <summary>Light tinted background, filled-circle icon, dark text (rounded corners). The default style.</summary>
        Soft,

        /// <summary>White card with a colored left accent bar and filled-circle icon (rounded corners).</summary>
        Card,

        /// <summary>Solid color background with white text and a left icon (square corners). The original look.</summary>
        Solid
    }
}
