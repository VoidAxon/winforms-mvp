using System;
using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.ChangeTrackingDemo
{
    public interface IProfileEditView : IControlView
    {
        string PatientName { get; set; }
        string BloodType { get; set; }
        IReadOnlyList<string> Allergies { get; set; }
        string Memo { get; set; }

        void ShowUpdatedAt(DateTime value);

        /// <summary>Shows whether there are unsaved changes.</summary>
        void ShowDirty(bool isDirty);

        void Log(string text);

        /// <summary>The user edited a field.</summary>
        event EventHandler Edited;
    }
}
