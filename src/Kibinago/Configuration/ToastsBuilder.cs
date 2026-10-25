using System;
using Kibinago.Common;
using Kibinago.Services;

namespace Kibinago.Configuration
{
    /// <summary>Toast settings (<c>mvp.Toasts</c>). Values are validated when the toast service is created.</summary>
    public sealed class ToastsBuilder
    {
        private readonly KibinagoBuilder _owner;
        internal readonly ToastHostOptions Options = new ToastHostOptions();

        internal ToastsBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Screen corner toasts appear in. See <see cref="ToastHostOptions.Position"/>.</summary>
        public ToastPosition Position
        {
            get => Options.Position;
            set { _owner.EnsureNotBuilt(); Options.Position = value; }
        }

        /// <summary>Toast size in pixels. See <see cref="ToastHostOptions.Size"/>.</summary>
        public System.Drawing.Size Size
        {
            get => Options.Size;
            set { _owner.EnsureNotBuilt(); Options.Size = value; }
        }

        /// <summary>Size the height to the content (width stays fixed). See <see cref="ToastHostOptions.AutoHeight"/>.</summary>
        public bool AutoHeight
        {
            get => Options.AutoHeight;
            set { _owner.EnsureNotBuilt(); Options.AutoHeight = value; }
        }

        /// <summary>Floor for the auto-sized height. See <see cref="ToastHostOptions.MinHeight"/>.</summary>
        public int MinHeight
        {
            get => Options.MinHeight;
            set { _owner.EnsureNotBuilt(); Options.MinHeight = value; }
        }

        /// <summary>Cap for the auto-sized height. See <see cref="ToastHostOptions.MaxHeight"/>.</summary>
        public int MaxHeight
        {
            get => Options.MaxHeight;
            set { _owner.EnsureNotBuilt(); Options.MaxHeight = value; }
        }

        /// <summary>Message font (owned by you; must outlive the application). Null = Segoe UI 10pt. See <see cref="ToastHostOptions.Font"/>.</summary>
        public System.Drawing.Font Font
        {
            get => Options.Font;
            set { _owner.EnsureNotBuilt(); Options.Font = value; }
        }

        /// <summary>Milliseconds a toast stays visible before fading. See <see cref="ToastHostOptions.Duration"/>.</summary>
        public int Duration
        {
            get => Options.Duration;
            set { _owner.EnsureNotBuilt(); Options.Duration = value; }
        }

        /// <summary>Gap between the toast stack and the screen edge. See <see cref="ToastHostOptions.Margin"/>.</summary>
        public int Margin
        {
            get => Options.Margin;
            set { _owner.EnsureNotBuilt(); Options.Margin = value; }
        }

        /// <summary>Gap between stacked toasts. See <see cref="ToastHostOptions.Gap"/>.</summary>
        public int Gap
        {
            get => Options.Gap;
            set { _owner.EnsureNotBuilt(); Options.Gap = value; }
        }

        /// <summary>Maximum toasts shown at once. See <see cref="ToastHostOptions.MaxVisibleToasts"/>.</summary>
        public int MaxVisibleToasts
        {
            get => Options.MaxVisibleToasts;
            set { _owner.EnsureNotBuilt(); Options.MaxVisibleToasts = value; }
        }

        /// <summary>Opacity while visible, greater than 0 and at most 1. See <see cref="ToastHostOptions.Opacity"/>.</summary>
        public double Opacity
        {
            get => Options.Opacity;
            set { _owner.EnsureNotBuilt(); Options.Opacity = value; }
        }

        /// <summary>Built-in look. See <see cref="ToastHostOptions.Style"/>.</summary>
        public ToastStyle Style
        {
            get => Options.Style;
            set { _owner.EnsureNotBuilt(); Options.Style = value; }
        }

        /// <summary>Draw a close glyph. See <see cref="ToastHostOptions.ShowCloseButton"/>.</summary>
        public bool ShowCloseButton
        {
            get => Options.ShowCloseButton;
            set { _owner.EnsureNotBuilt(); Options.ShowCloseButton = value; }
        }

        /// <summary>Paints every toast with <paramref name="renderer"/> unless a call sets its own.</summary>
        public ToastsBuilder UseRenderer(ToastRenderer renderer)
        {
            _owner.SetInstance(typeof(ToastRenderer), renderer ?? throw new ArgumentNullException(nameof(renderer)));
            return this;
        }

        /// <summary>Like <see cref="UseRenderer(ToastRenderer)"/>, built on first use with access to other services.</summary>
        public ToastsBuilder UseRenderer(Func<IServiceProvider, ToastRenderer> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(ToastRenderer), sp => factory(sp));
            return this;
        }

        /// <summary>
        /// Replaces the whole toast service, for presenters (<see cref="IToastService"/>) and views
        /// (<see cref="IWinFormsToastService"/>, <c>AnchoredToast</c>) alike.
        /// </summary>
        public ToastsBuilder Use(IWinFormsToastService service)
        {
            _owner.SetInstance(typeof(IWinFormsToastService), service ?? throw new ArgumentNullException(nameof(service)));
            return this;
        }

        /// <summary>Like <see cref="Use(IWinFormsToastService)"/>, built on first use with access to other services.</summary>
        public ToastsBuilder Use(Func<IServiceProvider, IWinFormsToastService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IWinFormsToastService), sp => factory(sp));
            return this;
        }
    }
}
