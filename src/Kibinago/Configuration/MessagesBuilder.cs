using System;
using Kibinago.Services;

namespace Kibinago.Configuration
{
    /// <summary>Message box settings (<c>mvp.Messages</c>).</summary>
    public sealed class MessagesBuilder
    {
        private readonly KibinagoBuilder _owner;
        internal readonly MessageDialogOptions Options = new MessageDialogOptions();

        internal MessagesBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Title used when a call passes no caption. Default: "System".</summary>
        public string DefaultCaption
        {
            get => Options.DefaultCaption;
            set { _owner.EnsureNotBuilt(); Options.DefaultCaption = value; }
        }

        /// <summary>Shows every message box (presenter <c>Messages</c> and <c>AnchoredMessageBox</c>) with <paramref name="backend"/>.</summary>
        public MessagesBuilder UseDialogs(IMessageDialogBackend backend)
        {
            _owner.SetInstance(typeof(IMessageDialogBackend), backend ?? throw new ArgumentNullException(nameof(backend)));
            return this;
        }

        /// <summary>Like <see cref="UseDialogs(IMessageDialogBackend)"/>, built on first use with access to other services.</summary>
        public MessagesBuilder UseDialogs(Func<IServiceProvider, IMessageDialogBackend> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _owner.SetFactory(typeof(IMessageDialogBackend), sp => factory(sp));
            return this;
        }
    }
}
