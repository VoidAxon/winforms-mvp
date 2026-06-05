using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Framework-internal close sink injected into a window Presenter. The Presenter pushes a
    /// close by calling <see cref="RequestCloseExtensions.RequestClose{TResult}"/> (typed) or the
    /// base <c>RequestClose(status)</c> (untyped); both route here. The result is carried as
    /// <see cref="object"/> and cast back to the concrete result type at the show boundary, so a
    /// single non-generic controller can serve presenters of any result type.
    /// </summary>
    internal interface ICloseSink
    {
        void Close(object result, InteractionStatus status);
    }
}
