using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.FirstErrorDemo
{
    public partial class IntakeControl : UserControl, IIntakeView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private readonly Dictionary<string, Control> _fields;

        public IntakeControl()
        {
            InitializeComponent();
            _binder.Add(IntakeActions.Submit, _submitButton);

            // Field (model property) → control, for the focus.
            _fields = new Dictionary<string, Control>
            {
                [nameof(IntakeModel.Name)] = _name,
                [nameof(IntakeModel.Kana)] = _kana,
                [nameof(IntakeModel.BirthDate)] = _birthDate,
                [nameof(IntakeModel.Phone)] = _phone,
                [nameof(IntakeModel.Email)] = _email,
            };
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public IntakeModel ReadModel() => new IntakeModel
        {
            Name = Blank(_name.Text),
            Kana = Blank(_kana.Text),
            BirthDate = _birthDate.Checked ? _birthDate.Value.Date : (System.DateTime?)null,
            Phone = Blank(_phone.Text),
            Email = Blank(_email.Text),
        };

        // A form-level error has no control: the message alone is enough.
        public void FocusError(string field)
        {
            if (_fields.TryGetValue(field, out var control))
                control.Focus();
        }

        public void ShowErrors(IReadOnlyList<(string Field, string Message)> errors)
        {
            _errors.Items.Clear();
            foreach (var (field, message) in errors)
                _errors.Items.Add($"{(field.Length == 0 ? "(画面全体)" : field)}: {message}");
            if (errors.Count == 0)
                _errors.Items.Add("エラーはありません。");
        }

        private static string Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
