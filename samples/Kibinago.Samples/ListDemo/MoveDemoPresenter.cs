using System;
using System.Linq;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ListDemo
{
    public static class MoveDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("MoveDemo");

        public static readonly ViewAction MoveUp = Factory.Create(nameof(MoveUp));
        public static readonly ViewAction MoveDown = Factory.Create(nameof(MoveDown));
        public static readonly ViewAction MoveToTop = Factory.Create(nameof(MoveToTop));
        public static readonly ViewAction MoveToBottom = Factory.Create(nameof(MoveToBottom));
    }

    /// <summary>
    /// Row moves (ListMoveExtensions): the rule lives here, the view only reports the current row.
    /// After each move only the rows whose display order changed are saved, at once.
    /// </summary>
    public class MoveDemoPresenter : ControlPresenterBase<IMoveDemoView>
    {
        private readonly PhraseRepository _repository = new PhraseRepository();
        private readonly SortableBindingList<PhraseRow> _rows = new SortableBindingList<PhraseRow>();

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(MoveDemoActions.MoveUp, () => Move(row => _rows.MoveUp(row)), canExecute: () => Index() > 0);
            Dispatcher.Register(MoveDemoActions.MoveDown, () => Move(row => _rows.MoveDown(row)), canExecute: () => Index() >= 0 && Index() < _rows.Count - 1);
            Dispatcher.Register(MoveDemoActions.MoveToTop, () => Move(row => _rows.MoveToTop(row)), canExecute: () => Index() > 0);
            Dispatcher.Register(MoveDemoActions.MoveToBottom, () => Move(row => _rows.MoveToBottom(row)), canExecute: () => Index() >= 0 && Index() < _rows.Count - 1);
        }

        protected override void OnInitialize()
        {
            SubscribeViewEvent(h => View.CurrentRowChanged += h, h => View.CurrentRowChanged -= h, Dispatcher.RaiseCanExecuteChanged);
            _rows.ReplaceAll(_repository.List());
            View.SetRows(_rows);
        }

        private int Index()
        {
            var id = View.CurrentId;
            for (var i = 0; i < _rows.Count; i++)
                if (_rows[i].Id == id) return i;
            return -1;
        }

        // move: one of the ListMoveExtensions on the list (MoveUp / MoveDown / MoveToTop / MoveToBottom),
        // false when the row did not move.
        private void Move(Func<PhraseRow, bool> move)
        {
            var row = _rows.FirstOrDefault(r => r.Id == View.CurrentId);
            if (row == null || !move(row)) return;   // nothing moved: nothing to save

            var changes = _rows.Select((r, i) => (Row: r, Order: i + 1)).Where(x => x.Row.DisplayOrder != x.Order).ToList();
            _repository.SaveOrders(changes.Select(x => (x.Row.Id, x.Order)));
            foreach (var (r, order) in changes)
                r.DisplayOrder = order;

            View.Log($"保存: {changes.Count} 行 — " + string.Join("、", changes.Select(x => $"#{x.Row.Id} → {x.Order}")));
            View.SelectRow(row.Id);
            Dispatcher.RaiseCanExecuteChanged();
        }
    }
}
