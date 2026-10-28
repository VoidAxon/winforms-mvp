using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Kibinago.Samples.CompositionDemo
{
    public partial class ContactRowControl : UserControl, IContactRowView
    {
        public ContactRowControl()
        {
            InitializeComponent();
            _name.TextChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
            _relation.TextChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
            _phone.TextChanged += (s, e) => Edited?.Invoke(this, EventArgs.Empty);
            _removeButton.Click += (s, e) => RemoveClicked?.Invoke(this, EventArgs.Empty);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ContactName { get => _name.Text; set => _name.Text = value; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Relation { get => _relation.Text; set => _relation.Text = value; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Phone { get => _phone.Text; set => _phone.Text = value; }

        public void ShowPhoneError(string message) => _phoneError.Text = message ?? "";

        public event EventHandler Edited;

        /// <summary>For the parent view, which turns it into its own RowRemoveRequested.</summary>
        internal event EventHandler RemoveClicked;
    }
}
