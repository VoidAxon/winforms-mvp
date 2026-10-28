using System;
using Kibinago.Views;

namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>
    /// A list of row panels. The parent view creates and removes the row controls; the parent presenter
    /// attaches a presenter to each (AttachChild). The view never removes a row on its own.
    /// </summary>
    public interface IContactsDemoView : IActionableView
    {
        /// <summary>Adds a row control at the end and returns it.</summary>
        IContactRowView AddRow();

        /// <summary>Disposes and removes a row control.</summary>
        void RemoveRow(IContactRowView row);

        void ShowStatus(string text);

        /// <summary>The 削除 button of a row was clicked.</summary>
        event EventHandler<IContactRowView> RowRemoveRequested;
    }
}
