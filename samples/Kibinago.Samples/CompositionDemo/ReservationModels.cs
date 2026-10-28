using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Common;

namespace Kibinago.Samples.CompositionDemo
{
    // ISelectable: a Selection compares by Key, so a reloaded list (new instances of the same records)
    // does not count as "another record".
    public sealed class Department : ISelectable
    {
        public int Id { get; set; }
        public string Name { get; set; }
        object ISelectable.Key => Id;
        public override string ToString() => Name;
    }

    public sealed class Doctor : ISelectable
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public string Name { get; set; }
        public DayOfWeek[] Days { get; set; }
        object ISelectable.Key => Id;
        public override string ToString() => Name;
    }

    public sealed class Slot : ISelectable
    {
        public int DoctorId { get; set; }
        public DayOfWeek Day { get; set; }
        public TimeSpan Start { get; set; }
        object ISelectable.Key => (DoctorId, Day, Start);
        public override string ToString() => $"{Start:hh\\:mm} -";
    }

    /// <summary>Stands in for the database.</summary>
    public sealed class ReservationRepository
    {
        private static readonly Department[] Departments =
        {
            new Department { Id = 1, Name = "内科" },
            new Department { Id = 2, Name = "外科" },
            new Department { Id = 3, Name = "小児科" },
        };

        private static readonly Doctor[] Doctors =
        {
            new Doctor { Id = 11, DepartmentId = 1, Name = "山田 医師", Days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday } },
            new Doctor { Id = 12, DepartmentId = 1, Name = "井上 医師", Days = new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday } },
            new Doctor { Id = 21, DepartmentId = 2, Name = "木村 医師", Days = new[] { DayOfWeek.Monday, DayOfWeek.Thursday } },
            new Doctor { Id = 31, DepartmentId = 3, Name = "林 医師", Days = new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Friday } },
            new Doctor { Id = 32, DepartmentId = 3, Name = "清水 医師", Days = new[] { DayOfWeek.Monday, DayOfWeek.Saturday } },
        };

        public IReadOnlyList<Department> GetDepartments() => Departments;

        public IReadOnlyList<Doctor> GetDoctors(int departmentId) => Doctors.Where(d => d.DepartmentId == departmentId).ToList();

        /// <summary>Free slots of a doctor on a weekday: none when the doctor does not work that day.</summary>
        public IReadOnlyList<Slot> GetSlots(int doctorId, DayOfWeek day)
        {
            var doctor = Doctors.First(d => d.Id == doctorId);
            if (!doctor.Days.Contains(day))
                return Array.Empty<Slot>();
            return Enumerable.Range(0, 6)
                .Where(i => (doctorId + (int)day + i) % 3 != 0)   // some are already taken
                .Select(i => new Slot { DoctorId = doctorId, Day = day, Start = TimeSpan.FromHours(9) + TimeSpan.FromMinutes(30 * i) })
                .ToList();
        }
    }
}
