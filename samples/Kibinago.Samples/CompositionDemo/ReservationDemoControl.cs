using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Common;

namespace Kibinago.Samples.CompositionDemo
{
    public partial class ReservationDemoControl : UserControl, IReservationDemoView
    {
        private bool _filling;   // replacing a list is not a user choice

        public ReservationDemoControl()
        {
            InitializeComponent();
            _departments.SelectedIndexChanged += (s, e) => Raise(DepartmentSelected);
            _doctors.SelectedIndexChanged += (s, e) => Raise(DoctorSelected);
            _days.SelectedIndexChanged += (s, e) => Raise(DaySelected);
            _slots.SelectedIndexChanged += (s, e) => Raise(SlotSelected);
        }

        // No actions: the view raises events instead (IControlView has an ActionBinder; null = none to bind).
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => null;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<Department> Departments { set => Fill(_departments, value); }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<Doctor> Doctors { set => Fill(_doctors, value); }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<SelectableItem<DayOfWeek>> Days { set => Fill(_days, value); }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<Slot> Slots { set => Fill(_slots, value); }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Department SelectedDepartment => _departments.SelectedItem as Department;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Doctor SelectedDoctor => _doctors.SelectedItem as Doctor;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SelectableItem<DayOfWeek> SelectedDay => _days.SelectedItem as SelectableItem<DayOfWeek>;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Slot SelectedSlot => _slots.SelectedItem as Slot;

        public event EventHandler DepartmentSelected;
        public event EventHandler DoctorSelected;
        public event EventHandler DaySelected;
        public event EventHandler SlotSelected;

        public void Log(string text) => _log.Items.Insert(0, text);

        public void ShowReservation(string text) => _reservationLabel.Text = text;

        private void Fill<T>(ListBox list, IEnumerable<T> items)
        {
            _filling = true;
            try
            {
                list.Items.Clear();
                foreach (var item in items)
                    list.Items.Add(item);
            }
            finally { _filling = false; }
        }

        private void Raise(EventHandler handler)
        {
            if (!_filling)
                handler?.Invoke(this, EventArgs.Empty);
        }
    }
}
