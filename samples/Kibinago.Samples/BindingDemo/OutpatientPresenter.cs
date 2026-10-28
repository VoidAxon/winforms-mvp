using System.ComponentModel;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.BindingDemo
{
    public static class OutpatientActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Outpatient");

        public static readonly ViewAction FillSample = Factory.Create(nameof(FillSample));
        public static readonly ViewAction Clear = Factory.Create(nameof(Clear));
    }

    /// <summary>
    /// Supervising Controller with binding: the presenter owns the model and changes it; the view binds
    /// its controls to it (two-way). The presenter never reads or writes the controls.
    /// </summary>
    public class OutpatientPresenter : ControlPresenterBase<IOutpatientView>
    {
        private static readonly DepartmentItem[] Departments =
        {
            new DepartmentItem { Id = 1, Name = "内科" },
            new DepartmentItem { Id = 2, Name = "外科" },
            new DepartmentItem { Id = 3, Name = "小児科" },
        };

        private readonly OutpatientInput _input = new OutpatientInput { DepartmentId = 1 };

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(OutpatientActions.FillSample, () =>
            {
                // Set the model: the bound controls follow.
                _input.Name = "高橋 健";
                _input.BirthDate = new System.DateTime(1985, 4, 12);
                _input.DepartmentId = 2;
                _input.Gender = Gender.Male;
                _input.FirstVisit = false;
                _input.Temperature = 37.2m;
            });
            Dispatcher.Register(OutpatientActions.Clear, () =>
            {
                _input.Name = "";
                _input.BirthDate = null;   // the picker's check box is cleared
                _input.DepartmentId = 1;
                _input.Gender = Gender.Unknown;
                _input.FirstVisit = true;
                _input.Temperature = 36.5m;
            });
        }

        protected override void OnInitialize()
        {
            PropertyChangedEventHandler changed = (s, e) => ShowModel();
            _input.PropertyChanged += changed;
            Disposable.Create(() => _input.PropertyChanged -= changed).DisposeWith(Disposables);

            View.Bind(_input, Departments);
            ShowModel();
        }

        private void ShowModel() =>
            View.ShowModel(
                $"Name        = \"{_input.Name}\"\r\n" +
                $"BirthDate   = {(_input.BirthDate?.ToString("yyyy/MM/dd") ?? "null")}\r\n" +
                $"DepartmentId= {_input.DepartmentId}\r\n" +
                $"Gender      = {_input.Gender}\r\n" +
                $"FirstVisit  = {_input.FirstVisit}\r\n" +
                $"Temperature = {_input.Temperature}");
    }
}
