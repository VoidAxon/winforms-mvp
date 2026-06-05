using System;
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Gives <see cref="IRequestClose{TResult}"/> presenters a strongly-typed Push call. The
    /// result is forwarded through the internal close sink (boxed to <see cref="object"/>) and
    /// cast back to <typeparamref name="TResult"/> at the show boundary.
    /// </summary>
    public static class RequestCloseExtensions
    {
        public static void RequestClose<TResult>(this IRequestClose<TResult> presenter,
            TResult result, InteractionStatus status = InteractionStatus.Ok)
        {
            var participant = presenter as ICloseParticipant;
            if (participant == null)
                throw new InvalidOperationException(
                    presenter.GetType().Name + " implements IRequestClose<" + typeof(TResult).Name +
                    "> but does not derive from WindowPresenterBaseCore<TView>. IRequestClose<TResult> " +
                    "must only be applied to window presenter classes.");
            participant.RequestCloseCore(result, status);
        }
    }
}
