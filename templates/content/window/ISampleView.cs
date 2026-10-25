//#if (Validation)
using System;
//#endif
using Kibinago.Views;

namespace MvpNamespace
{
    /// <summary>What the window shows: data, state and events only, never controls.</summary>
    public interface ISampleView : IWindowView
//#if (Validation)
        , IValidatableView
//#endif
    {
        string Title { get; set; }
//#if (Validation)

        /// <summary>Raised when the user edits any field.</summary>
        event EventHandler DataChanged;

        /// <summary>Moves the focus to the control of a field (an error is being shown).</summary>
        void FocusError(string field);
//#endif
    }
}
