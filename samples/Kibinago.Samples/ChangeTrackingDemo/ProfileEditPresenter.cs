using System;
using System.Linq;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ChangeTrackingDemo
{
    public static class ProfileEditActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("ProfileEdit");

        public static readonly ViewAction Save = Factory.Create(nameof(Save));
        public static readonly ViewAction Revert = Factory.Create(nameof(Revert));
    }

    /// <summary>
    /// ChangeTracker, "one object edited in place": TrackChanges(_profile) keeps a snapshot; IsChanged compares
    /// the object with it by content (editing a value back to the original is "not changed"). UpdatedAt is
    /// ignored for this screen. Detection only: after saving, AcceptChanges(); to revert, reload and track anew.
    /// In a window, the same IsChanged drives the "保存しますか" of CanClose.
    /// </summary>
    public class ProfileEditPresenter : ControlPresenterBase<IProfileEditView>
    {
        private readonly PatientProfileRepository _repository = new PatientProfileRepository();
        private PatientProfile _profile;
        private ChangeTracker<PatientProfile> _tracker;

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ProfileEditActions.Save, OnSave, canExecute: () => _tracker?.IsChanged == true);
            Dispatcher.Register(ProfileEditActions.Revert, OnRevert, canExecute: () => _tracker?.IsChanged == true);
        }

        protected override void OnInitialize()
        {
            SubscribeViewEvent(h => View.Edited += h, h => View.Edited -= h, OnEdited);
            Load();
        }

        private void Load()
        {
            _profile = _repository.Load();
            _tracker = TrackChanges(_profile).Ignore(p => p.UpdatedAt);
            View.PatientName = _profile.Name;
            View.BloodType = _profile.BloodType;
            View.Allergies = _profile.Allergies;
            View.Memo = _profile.Memo;
            Refresh();
        }

        private void OnEdited()
        {
            _profile.Name = View.PatientName;
            _profile.BloodType = View.BloodType;
            _profile.Allergies = View.Allergies.ToList();
            _profile.Memo = View.Memo;
            _profile.UpdatedAt = DateTime.Now;   // changes on every keystroke, yet is not "a change" (ignored)
            Refresh();
        }

        private void OnSave()
        {
            _repository.Save(_profile);
            _tracker.AcceptChanges();   // the saved state is the new baseline
            View.Log($"{DateTime.Now:HH:mm:ss} 保存しました (AcceptChanges)。");
            Refresh();
        }

        private void OnRevert()
        {
            Load();   // no undo in the tracker: reload and track the reloaded object
            View.Log($"{DateTime.Now:HH:mm:ss} 保存した内容に戻しました (読み直して新しい tracker)。");
        }

        private void Refresh()
        {
            View.ShowUpdatedAt(_profile.UpdatedAt);
            View.ShowDirty(_tracker.IsChanged);
            Dispatcher.RaiseCanExecuteChanged();
        }
    }
}
