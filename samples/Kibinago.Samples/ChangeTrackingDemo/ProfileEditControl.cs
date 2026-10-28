using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ChangeTrackingDemo
{
    public partial class ProfileEditControl : UserControl, IProfileEditView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private bool _loading;   // values set by the presenter are not edits

        public ProfileEditControl()
        {
            InitializeComponent();
            _binder.Add(ProfileEditActions.Save, _saveButton);
            _binder.Add(ProfileEditActions.Revert, _revertButton);

            _name.TextChanged += (s, e) => RaiseEdited();
            _bloodType.SelectedIndexChanged += (s, e) => RaiseEdited();
            _allergies.ItemCheck += (s, e) =>
            {
                if (!_loading)
                    BeginInvoke((MethodInvoker)RaiseEdited);   // ItemCheck comes before the state changes: report it after
            };
            _memo.TextChanged += (s, e) => RaiseEdited();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PatientName { get => _name.Text; set => SetQuietly(() => _name.Text = value); }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string BloodType { get => _bloodType.SelectedItem as string; set => SetQuietly(() => _bloodType.SelectedItem = value); }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<string> Allergies
        {
            get => _allergies.CheckedItems.Cast<string>().ToList();
            set => SetQuietly(() =>
            {
                for (var i = 0; i < _allergies.Items.Count; i++)
                    _allergies.SetItemChecked(i, value.Contains((string)_allergies.Items[i]));
            });
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Memo { get => _memo.Text; set => SetQuietly(() => _memo.Text = value); }

        public event EventHandler Edited;

        public void ShowUpdatedAt(DateTime value) => _updatedAt.Text = $"UpdatedAt: {value:HH:mm:ss.fff} (編集のたびに変わるが、変更とは数えない)";

        public void ShowDirty(bool isDirty)
        {
            _dirty.Text = isDirty ? "● 未保存の変更があります" : "変更はありません";
            _dirty.ForeColor = isDirty ? Color.Firebrick : Color.SeaGreen;
        }

        public void Log(string text) => _log.Items.Insert(0, text);

        private void SetQuietly(Action set)
        {
            _loading = true;
            try { set(); }
            finally { _loading = false; }
        }

        private void RaiseEdited()
        {
            if (!_loading && !IsDisposed)
                Edited?.Invoke(this, EventArgs.Empty);
        }
    }
}
