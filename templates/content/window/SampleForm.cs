//#if (Validation)
using System;
//#endif
using System.ComponentModel;
using System.Windows.Forms;
//#if (Validation)
using Kibinago.Common.Validation;
using Kibinago.Common.Validation.Core;
//#endif
using Kibinago.ViewActions;

namespace MvpNamespace
{
    /// <summary>Binds controls to actions and shows what the presenter sets. No business or closing logic.</summary>
    public partial class SampleForm : Form, ISampleView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
//#if (Validation)
        private readonly ErrorProviderDisplay _errors;
//#endif

        public SampleForm()
        {
            InitializeComponent();
            _binder.Add(SampleActions.Ok, btnOk);
            _binder.Add(SampleActions.Cancel, btnCancel);
//#if (Validation)

            _errors = new ErrorProviderDisplay(errorProvider)
                .Map(nameof(SampleModel.Title), txtTitle)
                .Map(ValidationState.FormLevel, btnOk);
            txtTitle.TextChanged += (s, e) => DataChanged?.Invoke(this, EventArgs.Empty);
//#endif
        }

        public IViewActionBinder ActionBinder => _binder;
//#if (Validation)

        public IValidationErrorDisplay ValidationErrors => _errors;

        public event EventHandler DataChanged;

        public void FocusError(string field) => _errors.TryFocusError(field);
//#endif

        // View data, not a designer property: keep it out of the .Designer.cs file.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title
        {
            get => txtTitle.Text;
            set => txtTitle.Text = value;
        }
    }
}
