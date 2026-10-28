using System.Collections.Generic;
using System.Linq;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ListDemo
{
    public static class SortDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("SortDemo");

        public static readonly ViewAction SortByWard = Factory.Create(nameof(SortByWard));
        public static readonly ViewAction Resort = Factory.Create(nameof(Resort));
        public static readonly ViewAction RemoveSort = Factory.Create(nameof(RemoveSort));
        public static readonly ViewAction Reload = Factory.Create(nameof(Reload));
    }

    /// <summary>
    /// SortableBindingList: the presenter owns the list, the view binds the grid to it once.
    /// Header clicks sort in the view; the business order (ward, then number) and the filter
    /// (ReplaceAll + LINQ, keeping the user's sort) are use-case logic, so they live here.
    /// </summary>
    public class SortDemoPresenter : ControlPresenterBase<ISortDemoView>
    {
        private readonly SortableBindingList<PatientRow> _rows = new SortableBindingList<PatientRow>();
        private IReadOnlyList<PatientRow> _all = PatientRow.Sample();

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(SortDemoActions.SortByWard, () =>
            {
                _rows.ApplySort(s => s.By(r => r.Ward).ThenBy(r => r.No));   // columns checked by the compiler
                View.ShowStatus("病棟 → 番号 の順に並べました (Presenter の ApplySort)。");
            });
            Dispatcher.Register(SortDemoActions.Resort, () =>
            {
                _rows.Resort();
                View.ShowStatus("選んだ並べ替えで並べ直しました (Resort)。");
            });
            Dispatcher.Register(SortDemoActions.RemoveSort, () =>
            {
                _rows.RemoveSort();
                View.ShowStatus("並べ替えを解除しました (今の並びのまま)。");
            });
            Dispatcher.Register(SortDemoActions.Reload, () =>
            {
                _all = PatientRow.Sample();
                ApplyFilter();
                View.ShowStatus("読み直しました (ReplaceAll: 並べ替えは保たれます)。");
            });
        }

        protected override void OnInitialize()
        {
            SubscribeViewEvent(h => View.WardChanged += h, h => View.WardChanged -= h, ApplyFilter);
            SubscribeViewEvent(h => View.KeepSortedChanged += h, h => View.KeepSortedChanged -= h, () =>
            {
                _rows.KeepSorted = View.KeepSorted;
                View.ShowStatus(_rows.KeepSorted
                    ? "KeepSorted: 並べ替えの列を編集すると、確定後にその行が正しい位置へ移ります。"
                    : "既定: 編集した行は動きません (Excel と同じ)。並べ直すには「並べ直す」。");
            });

            View.SetWards(_all.Select(r => r.Ward).Distinct().OrderBy(w => w).ToList());
            View.SetRows(_rows);
            ApplyFilter();
            View.ShowStatus("見出しをクリック: その列で並べ替え。Shift+クリック: 列を追加。Ctrl+クリック: 列を外す。");
        }

        // The filter is the presenter's: LINQ over all rows, ReplaceAll keeps the sort (one Reset).
        private void ApplyFilter() =>
            _rows.ReplaceAll(_all.Where(r => View.SelectedWard == null || r.Ward == View.SelectedWard));
    }
}
