using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Picks the toast painter most-specific-first: per-call renderer, then per-call style, then
    /// the app-wide default renderer, then the app-wide default style. The built-in style renderers
    /// are stateless and shared as singletons.
    /// </summary>
    internal static class ToastRendererResolver
    {
        private static readonly DefaultToastRenderer Default = new DefaultToastRenderer();
        private static readonly SoftToastRenderer Soft = new SoftToastRenderer();
        private static readonly CardToastRenderer Card = new CardToastRenderer();

        /// <summary>Maps a <see cref="ToastStyle"/> to its built-in renderer singleton.</summary>
        public static ToastRenderer ForStyle(ToastStyle style)
        {
            switch (style)
            {
                case ToastStyle.Soft: return Soft;
                case ToastStyle.Card: return Card;
                case ToastStyle.Default:
                default: return Default;
            }
        }

        /// <summary>
        /// Resolves the renderer for a toast. A custom renderer (per call, then app-wide) always
        /// wins over a style; a per-call value always wins over the app-wide default.
        /// </summary>
        public static ToastRenderer Resolve(ToastRenderer perCallRenderer, ToastStyle? perCallStyle, ToastRenderer defaultRenderer, ToastStyle defaultStyle)
        {
            if (perCallRenderer != null) return perCallRenderer;
            if (perCallStyle.HasValue) return ForStyle(perCallStyle.Value);
            if (defaultRenderer != null) return defaultRenderer;
            return ForStyle(defaultStyle);
        }
    }
}
