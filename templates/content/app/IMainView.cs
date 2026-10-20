using WinformsMVP.MVP.Views;

namespace MyMvpApp
{
    /// <summary>What the main window shows: data and behavior only, never controls.</summary>
    public interface IMainView : IWindowView
    {
        string StatusText { set; }
    }
}
