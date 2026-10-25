using System;

namespace Kibinago.Presenters
{
    public interface IPresenter : IDisposable
    {
        Type ViewInterfaceType { get; }
    }
}
