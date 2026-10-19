using WinformsMVP.MVP.Views;

namespace MvpNamespace
{
    /// <summary>What the control shows: data, state and events only, never controls.</summary>
    public interface ISampleView : IActionableView
    {
        string Summary { set; }
    }
}
