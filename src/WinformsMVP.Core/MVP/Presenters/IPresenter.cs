using System;

namespace WinformsMVP.MVP.Presenters
{
    public interface IPresenter : IDisposable
    {
        Type ViewInterfaceType { get; }
    }
}
