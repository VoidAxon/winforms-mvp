using System;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Samples.ListDemo
{
    /// <summary>A list of phrases in display order, with 上へ / 下へ / 最上へ / 最下へ.</summary>
    public interface IMoveDemoView : IActionableView
    {
        void SetRows(SortableBindingList<PhraseRow> rows);

        /// <summary>The id of the current row, or null.</summary>
        int? CurrentId { get; }

        void SelectRow(int id);

        /// <summary>Adds a line to the log of what was saved.</summary>
        void Log(string text);

        /// <summary>The current row changed.</summary>
        event EventHandler CurrentRowChanged;
    }
}
