using System;
using Kibinago.Common;

namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// The <see cref="IToastService"/> registration: forwards to whichever
    /// <see cref="IWinFormsToastService"/> won registration, so presenters and views share one toast
    /// service. It is a separate, non-disposable object on purpose — registering the toast service
    /// itself under a second factory would let a container dispose a caller-owned instance.
    /// </summary>
    internal sealed class ToastServiceForwarder : IToastService
    {
        public ToastServiceForwarder(IWinFormsToastService target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        internal IWinFormsToastService Target { get; }

        public void Show(string text, ToastType type, int? duration = null) => Target.Show(text, type, duration);
    }
}
