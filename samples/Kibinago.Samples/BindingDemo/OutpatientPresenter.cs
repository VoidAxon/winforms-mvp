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

        private readonly OutpatientInput _form = new OutpatientInput { DepartmentId = 1 };

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(OutpatientActions.FillSample, () =>
            {
                // Set the model: the bound controls follow.
                _form.Name = "高橋 健";
                _form.BirthDate = new System.DateTime(1985, 4, 12);
                _form.DepartmentId = 2;
                _form.Gender = Gender.Male;
                _form.FirstVisit = false;
                _form.Temperature = 37.2m;
            });
            Dispatcher.Register(OutpatientActions.Clear, () =>
            {
                _form.Name = "";
                _form.BirthDate = null;   // the picker's check box is cleared
                _form.DepartmentId = 1;
                _form.Gender = Gender.Unknown;
                _form.FirstVisit = true;
                _form.Temperature = 36.5m;
            });
        }

        protected override void OnInitialize()
        {
            PropertyChangedEventHandler changed = (s, e) => ShowModel();
            _form.PropertyChanged += changed;
            Disposable.Create(() => _form.PropertyChanged -= changed).DisposeWith(Disposables);

            View.Bind(_form, Departments);
            ShowModel();
        }

        private void ShowModel() =>
            View.ShowModel(
                $"Name        = \"{_form.Name}\"\r\n" +
                $"BirthDate   = {(_form.BirthDate?.ToString("yyyy/MM/dd") ?? "null")}\r\n" +
                $"DepartmentId= {_form.DepartmentId}\r\n" +
                $"Gender      = {_form.Gender}\r\n" +
                $"FirstVisit  = {_form.FirstVisit}\r\n" +
                $"Temperature = {_form.Temperature}");
    }
}
