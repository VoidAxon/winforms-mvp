using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Common;
using Kibinago.Samples.CompositionDemo;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Samples
{
    public class ReservationDemoPresenterTests
    {
        private sealed class MockReservationView : IReservationDemoView
        {
            public IReadOnlyList<Department> DepartmentList = Array.Empty<Department>();
            public IReadOnlyList<Doctor> DoctorList = Array.Empty<Doctor>();
            public IReadOnlyList<SelectableItem<DayOfWeek>> DayList = Array.Empty<SelectableItem<DayOfWeek>>();
            public IReadOnlyList<Slot> SlotList = Array.Empty<Slot>();
            public string Reservation;

            public IReadOnlyList<Department> Departments { set => DepartmentList = value; }
            public IReadOnlyList<Doctor> Doctors { set { DoctorList = value; SelectedDoctor = null; } }
            public IReadOnlyList<SelectableItem<DayOfWeek>> Days { set => DayList = value; }
            public IReadOnlyList<Slot> Slots { set { SlotList = value; SelectedSlot = null; } }

            public Department SelectedDepartment { get; private set; }
            public Doctor SelectedDoctor { get; private set; }
            public SelectableItem<DayOfWeek> SelectedDay { get; private set; }
            public Slot SelectedSlot { get; private set; }

            public void Log(string text) { }
            public void ShowReservation(string text) => Reservation = text;

            public event EventHandler DepartmentSelected;
            public event EventHandler DoctorSelected;
            public event EventHandler DaySelected;
            public event EventHandler SlotSelected;

            public void PickDepartment(string name) { SelectedDepartment = DepartmentList.Single(d => d.Name == name); DepartmentSelected?.Invoke(this, EventArgs.Empty); }
            public void PickDoctor(string name) { SelectedDoctor = DoctorList.Single(d => d.Name == name); DoctorSelected?.Invoke(this, EventArgs.Empty); }
            public void PickDay(DayOfWeek day) { SelectedDay = DayList.Single(d => d.Value == day); DaySelected?.Invoke(this, EventArgs.Empty); }
            public void PickSlot(int index) { SelectedSlot = SlotList[index]; SlotSelected?.Invoke(this, EventArgs.Empty); }
        }

        [Fact]
        public void SlotsNeedBothADoctorAndADay_AndAnotherDepartmentClearsEverythingBelow()
        {
            var view = new MockReservationView();
            using (PresenterTestHost.Start(TestServices.Create(), new ReservationDemoPresenter(), view))
            {
                view.PickDepartment("内科");
                Assert.Equal(new[] { "山田 医師", "井上 医師" }, view.DoctorList.Select(d => d.Name));

                view.PickDoctor("山田 医師");
                Assert.Empty(view.SlotList);                 // no day yet
                view.PickDay(DayOfWeek.Monday);
                Assert.NotEmpty(view.SlotList);              // doctor × day
                view.PickSlot(0);
                Assert.Contains("山田 医師", view.Reservation);

                view.PickDepartment("外科");                  // clears 医師, which clears 予約枠
                Assert.Equal(new[] { "木村 医師" }, view.DoctorList.Select(d => d.Name));
                Assert.Empty(view.SlotList);
                Assert.Equal("予約枠を選んでください。", view.Reservation);
            }
        }

        [Fact]
        public void ADayTheDoctorDoesNotWork_HasNoSlots()
        {
            var view = new MockReservationView();
            using (PresenterTestHost.Start(TestServices.Create(), new ReservationDemoPresenter(), view))
            {
                view.PickDepartment("内科");
                view.PickDoctor("井上 医師");   // Tuesday and Thursday only
                view.PickDay(DayOfWeek.Monday);
                Assert.Empty(view.SlotList);
            }
        }
    }
}
