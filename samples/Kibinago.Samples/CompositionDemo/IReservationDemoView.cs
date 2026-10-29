using System;
using System.Collections.Generic;
using Kibinago.Common;
using Kibinago.Views;

namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>診療科, 医師, 曜日 and 予約枠 lists, and a log of what was reloaded.</summary>
    public interface IReservationDemoView : IControlView
    {
        IReadOnlyList<Department> Departments { set; }
        IReadOnlyList<Doctor> Doctors { set; }
        IReadOnlyList<SelectableItem<DayOfWeek>> Days { set; }
        IReadOnlyList<Slot> Slots { set; }

        Department SelectedDepartment { get; }
        Doctor SelectedDoctor { get; }
        SelectableItem<DayOfWeek> SelectedDay { get; }
        Slot SelectedSlot { get; }

        void Log(string text);
        void ShowReservation(string text);

        // Raised only when the USER changes a selection (not when a list is replaced).
        event EventHandler DepartmentSelected;
        event EventHandler DoctorSelected;
        event EventHandler DaySelected;
        event EventHandler SlotSelected;
    }
}
