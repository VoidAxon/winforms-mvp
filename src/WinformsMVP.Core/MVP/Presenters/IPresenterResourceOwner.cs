using System;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Lets framework hosts tie a resource to a presenter's lifetime (e.g. the DI scope the
    /// presenter was resolved from). The resource is disposed right after the presenter's
    /// <c>Cleanup</c>, together with its <c>Disposables</c>.
    /// </summary>
    internal interface IPresenterResourceOwner
    {
        void OwnUntilDisposed(IDisposable resource);
    }
}
