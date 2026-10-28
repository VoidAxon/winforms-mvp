using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.BindingDemo
{
    public partial class OutpatientControl : UserControl, IOutpatientView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public OutpatientControl()
        {
            InitializeComponent();
            _binder.Add(OutpatientActions.FillSample, _fillButton);
            _binder.Add(OutpatientActions.Clear, _clearButton);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        // One line per control (Kibinago.Views binding helpers): checked by the compiler, one property per expression.
        public void Bind(OutpatientInput form, IReadOnlyList<DepartmentItem> departments)
        {
            _department.DisplayMember = nameof(DepartmentItem.Name);
            _department.ValueMember = nameof(DepartmentItem.Id);
            _department.DataSource = departments;

            _name.Bind(form, x => x.Name);
            _birthDate.Bind(form, x => x.BirthDate);            // DateTime?: the check box means "no date"
            _department.Bind(form, x => x.DepartmentId);         // SelectedValue
            new Dictionary<RadioButton, Gender>
            {
                [_unknown] = Gender.Unknown,
                [_male] = Gender.Male,
                [_female] = Gender.Female,
            }.BindRadioGroup(form, x => x.Gender);
            _firstVisit.Bind(form, x => x.FirstVisit);
            _temperature.Bind(form, x => x.Temperature);
        }

        public void ShowModel(string text) => _model.Text = text;
    }
}
