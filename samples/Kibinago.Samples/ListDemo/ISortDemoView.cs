using System;
using System.Collections.Generic;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Samples.ListDemo
{
    /// <summary>A grid of patients that sorts by its headers, a ward filter and the sort options.</summary>
    public interface ISortDemoView : IControlView
    {
        /// <summary>The list the grid binds to (given once; the presenter keeps changing its contents).</summary>
        void SetRows(SortableBindingList<PatientRow> rows);

        /// <summary>The wards for the filter ("すべて" is the view's own first entry).</summary>
        void SetWards(IReadOnlyList<string> wards);

        /// <summary>The ward chosen in the filter, or null for all.</summary>
        string SelectedWard { get; }

        /// <summary>Whether "move edited rows to their place" is checked.</summary>
        bool KeepSorted { get; }

        void ShowStatus(string text);

        /// <summary>The filter changed.</summary>
        event EventHandler WardChanged;

        /// <summary>The KeepSorted check box changed.</summary>
        event EventHandler KeepSortedChanged;
    }
}
