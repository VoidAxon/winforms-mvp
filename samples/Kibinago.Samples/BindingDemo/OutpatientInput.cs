using System;
using Kibinago.Common;

namespace Kibinago.Samples.BindingDemo
{
    public enum Gender { Unknown, Male, Female }

    /// <summary>
    /// The input model the view binds to. NotifyingObject raises PropertyChanged, so a value the
    /// presenter sets appears in the control, and the user's input reaches the model.
    /// </summary>
    public sealed class OutpatientInput : NotifyingObject
    {
        private string _name = "";
        private DateTime? _birthDate;
        private int _departmentId;
        private Gender _gender;
        private bool _firstVisit = true;
        private decimal _temperature = 36.5m;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public DateTime? BirthDate { get => _birthDate; set => SetProperty(ref _birthDate, value); }
        public int DepartmentId { get => _departmentId; set => SetProperty(ref _departmentId, value); }
        public Gender Gender { get => _gender; set => SetProperty(ref _gender, value); }
        public bool FirstVisit { get => _firstVisit; set => SetProperty(ref _firstVisit, value); }
        public decimal Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }
    }

    public sealed class DepartmentItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
