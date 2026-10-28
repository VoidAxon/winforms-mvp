using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.CompositionDemo
{
    public static class ContactsDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("ContactsDemo");

        public static readonly ViewAction AddRow = Factory.Create(nameof(AddRow));
        public static readonly ViewAction Save = Factory.Create(nameof(Save));
    }

    /// <summary>
    /// Panels added and removed at runtime: each row is a child presenter (AttachChild with the row's
    /// model). The handle is kept by row view; removing a row disposes the handle first, then the view
    /// removes the control. Disposing this presenter disposes the remaining rows.
    /// </summary>
    public class ContactsDemoPresenter : ControlPresenterBase<IContactsDemoView>
    {
        private readonly List<ContactRow> _contacts = new List<ContactRow>();
        private readonly Dictionary<IContactRowView, (ContactRow Row, IDisposable Handle)> _rows =
            new Dictionary<IContactRowView, (ContactRow, IDisposable)>();

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ContactsDemoActions.AddRow, () => AddRow(new ContactRow()));
            Dispatcher.Register(ContactsDemoActions.Save, Save, canExecute: () => _contacts.Count > 0);
        }

        protected override void OnInitialize()
        {
            SubscribeViewEvent<IContactRowView>(h => View.RowRemoveRequested += h, h => View.RowRemoveRequested -= h, RemoveRow);
            AddRow(new ContactRow { Name = "佐藤 花子", Relation = "妻", Phone = "090-1234-5678" });
        }

        private void AddRow(ContactRow row)
        {
            var view = View.AddRow();
            _contacts.Add(row);
            _rows.Add(view, (row, AttachChild(new ContactRowPresenter(), view, row)));
            Report();
        }

        private void RemoveRow(IContactRowView view)
        {
            if (!_rows.TryGetValue(view, out var entry))
                return;
            entry.Handle.Dispose();    // the row's presenter first …
            View.RemoveRow(view);      // … then its control
            _rows.Remove(view);
            _contacts.Remove(entry.Row);
            Report();
        }

        private void Save()
        {
            var lines = _contacts.Select((c, i) => $"{i + 1}. {c.Name} ({c.Relation}) {c.Phone}");
            Messages.ShowInfo("保存する連絡先:\r\n" + string.Join("\r\n", lines), "保存");
        }

        private void Report()
        {
            View.ShowStatus($"連絡先 {_contacts.Count} 件 (子 Presenter {_rows.Count} 個)。");
            Dispatcher.RaiseCanExecuteChanged();
        }
    }
}
