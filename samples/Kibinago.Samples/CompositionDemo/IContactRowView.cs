using System;
using Kibinago.Views;

namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>One row: name, relation, phone, and an error next to the phone.</summary>
    public interface IContactRowView : IControlView
    {
        string ContactName { get; set; }
        string Relation { get; set; }
        string Phone { get; set; }

        void ShowPhoneError(string message);

        /// <summary>The user edited one of the fields.</summary>
        event EventHandler Edited;
    }
}
