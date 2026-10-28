using System;
using System.Linq;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>
    /// Three-level cascade with two parents at the last level:
    ///   診療科 → 医師 (Cascade.Bind) and 医師 + 曜日 → 予約枠 (Cascade.Combine).
    /// Choosing another 診療科 clears 医師, whose clearing clears 予約枠 — each level only reloads its own list.
    /// </summary>
    public class ReservationDemoPresenter : ControlPresenterBase<IReservationDemoView>
    {
        private static readonly string[] DayNames = { "日", "月", "火", "水", "木", "金", "土" };

        private readonly ReservationRepository _repository = new ReservationRepository();
        private readonly Selection<Department> _department = new Selection<Department>();
        private readonly Selection<Doctor> _doctor = new Selection<Doctor>();
        private readonly Selection<SelectableItem<DayOfWeek>> _day = new Selection<SelectableItem<DayOfWeek>>();
        private readonly Selection<Slot> _slot = new Selection<Slot>();

        protected override void OnInitialize()
        {
            // The user's choices go to the selections …
            SubscribeViewEvent(h => View.DepartmentSelected += h, h => View.DepartmentSelected -= h, () => _department.Select(View.SelectedDepartment));
            SubscribeViewEvent(h => View.DoctorSelected += h, h => View.DoctorSelected -= h, () => _doctor.Select(View.SelectedDoctor));
            SubscribeViewEvent(h => View.DaySelected += h, h => View.DaySelected -= h, () => _day.Select(View.SelectedDay));
            SubscribeViewEvent(h => View.SlotSelected += h, h => View.SlotSelected -= h, () => _slot.Select(View.SelectedSlot));

            // … and the cascades reload the lists below them.
            Cascade.Bind(_department, _doctor, department =>
            {
                View.Doctors = department == null ? Array.Empty<Doctor>() : _repository.GetDoctors(department.Id);
                View.Log(department == null ? "医師: 空にしました" : $"医師: {department.Name} で読み直し");
            }).DisposeWith(Disposables);

            Cascade.Combine(_doctor, _day, _slot, (doctor, day) =>
            {
                View.Slots = doctor == null || day == null ? Array.Empty<Slot>() : _repository.GetSlots(doctor.Id, day.Value);
                View.Log(doctor == null || day == null
                    ? "予約枠: 空にしました (医師と曜日の両方が必要)"
                    : $"予約枠: {doctor.Name} × {day.Text}曜日 で読み直し");
            }).DisposeWith(Disposables);

            _slot.CurrentChanged += OnSlotChanged;
            Disposable.Create(() => _slot.CurrentChanged -= OnSlotChanged).DisposeWith(Disposables);

            View.Departments = _repository.GetDepartments();
            View.Days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
                .Select(d => SelectableItem.Of(d, DayNames[(int)d])).ToList();   // a value type wrapped for Selection
            OnSlotChanged(this, EventArgs.Empty);
        }

        private void OnSlotChanged(object sender, EventArgs e) =>
            View.ShowReservation(_slot.Current == null
                ? "予約枠を選んでください。"
                : $"予約: {_department.Current?.Name} / {_doctor.Current?.Name} / {_day.Current?.Text}曜日 {_slot.Current.Start:hh\\:mm}");
    }
}
