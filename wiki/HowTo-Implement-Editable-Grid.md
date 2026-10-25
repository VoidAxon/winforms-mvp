# HowTo: グリッドで明細を直接編集する

DataGridView (やサードパーティのグリッド) のセルで明細を編集する画面の書き方です。フレームワークはグリッド専用の部品を持ちません — グリッドの扱いは View (Form) の仕事で、Presenter はグリッドを知りません。このページは、その分担と、どの画面でも同じになる書き方・落とし穴をまとめたものです。

## 目次

- [分担: 業務イベントは Presenter、グリッドの操作は View](#分担-業務イベントは-presenterグリッドの操作は-view)
- [行キー](#行キー)
- [View インターフェイス](#view-インターフェイス)
- [セルの確定: 拒否と書き換え](#セルの確定-拒否と書き換え)
- [行の検証とエラー表示](#行の検証とエラー表示)
- [保存のタイミングと変更検出](#保存のタイミングと変更検出)
- [落とし穴](#落とし穴)
- [サードパーティのグリッド](#サードパーティのグリッド)
- [テスト](#テスト)

---

## 分担: 業務イベントは Presenter、グリッドの操作は View

判断の基準は **業務上の意味があるかどうか** です。

| グリッドで起きること | 担当 | Presenter が受け取るもの |
|---|---|---|
| セルを離れる (入力の確定) | View が `CellValidating` を翻訳 | 行キー・列・入力文字列 (拒否できる) |
| コンボで値を選ぶ | View がすぐ確定して翻訳 | 行キー・列・選んだ値 |
| ダブルクリック / Enter で選択画面を開く、Delete で消す | View | 「この行の○○を選びたい / 消したい」 |
| 選択行が変わる | View | 選択変更 (行キーは View から読む) |
| 書き換えた値を編集中のセルに入れる | View | なし (Presenter は `ShowRow` するだけ) |
| Presenter が行を表示し直す間にイベントを出さない (再入防止) | View | なし |
| Enter で右へ移動、クリックで編集開始 | View | なし |
| エラーアイコンをどのセルに出すか | View | なし (Presenter は `Validation` にエラーを置くだけ) |

下半分を Presenter に渡すと、Presenter が「編集コントロール」「セルの表示状態」を知ることになり、View の仕事が Presenter に移ります。しかもそのコードはグリッドがないと動かないので、Presenter に置いてもテストできません。

## 行キー

新しい行は保存するまで ID がありません。画面の中で行を識別するキー (`int`) を Presenter が振り、View との受け渡しはすべてこのキーで行います。

- 採番カウンタは Presenter に置き、**変更検出で追跡するモデルには入れない** (private フィールドも比較されるため、「追加して削除しただけ」で変更ありになる)。
- View は行の `Tag` (DataGridView) / `UserData` (C1FlexGrid) に表示用の行オブジェクトを置き、キーで行を探します。
- 「登録」の後は保存結果 (ID 付き) を読み直して、行とキーを作り直します。

## View インターフェイス

```csharp
public interface IOrderLinesView : IWindowView, IValidatableView
{
    void ShowRows(IReadOnlyList<OrderLineRow> rows);    // 全行を置き換える
    void ShowRow(OrderLineRow row);                     // 1 行を表示し直す (編集中のセルは新しい値を受け取る)
    int? SelectedRowKey { get; set; }                   // 設定時はイベントを出さない
    void FocusCell(int rowKey, string column);          // エラーを見せるとき

    event EventHandler<ValueChangingEventArgs<CellEdit>> CellCommitting;   // 文字・数値のセル (Cancel で拒否)
    event EventHandler<CellEdit> ChoiceChanged;                            // コンボのセル
    event EventHandler<int> ProductRequested;                              // 選択画面を開く列 (行キー)
    event EventHandler RowSelectionChanged;
}

public sealed class CellEdit { public int RowKey; public string Column; public string Text; }   // 業務の型 (EventArgs 不要)
public sealed class OrderLineRow { public int RowKey; public OrderLine Line; public IReadOnlyList<Unit> Units; /* 行ごとの選択肢など */ }
```

列名はモデルのプロパティ名 (`nameof(OrderLine.Quantity)`) にそろえると、Presenter・View・検証の項目名が 1 つの約束で済みます。

## セルの確定: 拒否と書き換え

`ValueChangingEventArgs<T>` は拒否 (`Cancel`) だけを運び、書き換えた値は運びません。書き換えは **Presenter がイベントの中で `ShowRow` を呼ぶ** ことで伝えます。

```csharp
// Presenter
private void OnCellCommitting(ValueChangingEventArgs<CellEdit> e)
{
    var line = _input.Find(e.NewValue.RowKey);
    if (!TryParseQuantity(e.NewValue.Text, out var quantity))   // 全角→半角、小数の切り捨てもここで
    {
        e.Cancel = true;                                         // セルに留まる
        Toasts.Show("0 以上の数値を入力してください", ToastType.Warning);
        return;
    }
    line.Line.Quantity = quantity;
    View.ShowRow(GridRow(line));                                 // 書き換えた値もこれで届く
    View.IsDirty = _tracker.IsChanged;
}
```

```csharp
// Form (DataGridView)
private void dgv_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
{
    if (_showing || !dgv.IsCurrentCellInEditMode) return;
    var args = new ValueChangingEventArgs<CellEdit>(old, new CellEdit { RowKey = KeyOf(e.RowIndex), Column = Column(e), Text = (string)e.FormattedValue });
    CellCommitting?.Invoke(this, args);
    e.Cancel = args.Cancel;
}

void IOrderLinesView.ShowRow(OrderLineRow row)
{
    var gridRow = Find(row.RowKey);
    var editing = dgv.IsCurrentCellInEditMode && dgv.CurrentCell.RowIndex == gridRow.Index ? dgv.CurrentCell.ColumnIndex : -1;
    Showing(() => Fill(gridRow, row, skipColumn: editing));      // 編集中のセルの Value は触らない
    // 文字のセルだけ: 書き換えた値を編集コントロールに入れる → 検証の後でグリッドがこれを確定する
    if (editing >= 0 && dgv.Columns[editing] is DataGridViewTextBoxColumn)
        dgv.EditingControl.Text = Format(row, dgv.Columns[editing].Name);
}
```

- 編集コントロールに入れた値は、検証の後にグリッドが確定する値になります (.NET 10 の DataGridView で、全角「１．２３４５」→「1.234」を実機で確認)。グリッドの種類や版によって確定の仕組みが違う場合は、確定の後 (`CellEndEdit` / `AfterEdit`) に `ShowRow` の値でセルを表示し直す形にします。
- 拒否のメッセージはトースト (`Toasts`) が無難です。`CellValidating` の中でモーダルを出すと、グリッドのフォーカスと競合しやすくなります。
- コンボのセルは `CurrentCellDirtyStateChanged` で `CommitEdit` してすぐ `CellValueChanged` を出させ、`ChoiceChanged` に翻訳します。

## 行の検証とエラー表示

行のルールは Presenter のルール (`Validation.SetError` / `ClearError`) で、項目名は `列[行キー]` (例 `"Quantity[3]"`) です。

- **削除した行のエラーは自動では消えません。** ルールを計算し直すたびに、前回置いて今回ない項目を `ClearError` します (Presenter に項目の集合を持つ)。
- **送信失敗時の最初のエラー**は、項目がモデルのプロパティではないので画面の順を渡して決めます: `Validation.ErrorsInOrder(画面の順の項目名…)[0]` → `Messages.ShowWarning` → `View.FocusCell(行キー, 列)`。
- **表示**: `ErrorProviderDisplay` はコントロール単位なのでセルには使えません。View で `IValidationErrorDisplay` を実装し、`DataGridViewCell.ErrorText` に出します。行を表示し直した後に貼り直すこと。セルごとの「離れた後に表示」の追跡はないので、`revealAll` (送信後) のときだけ出すのが簡単です。

```csharp
private sealed class GridErrorDisplay : IValidationErrorDisplay
{
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _errors = new Dictionary<string, IReadOnlyList<string>>();
    private bool _revealAll;
    // ShowErrors / Reset で保持して Apply。ShowRows の後にも Apply を呼ぶ
    public void Apply()
    {
        foreach (DataGridViewRow row in _grid.Rows)
            foreach (DataGridViewCell cell in row.Cells)
            {
                var field = _grid.Columns[cell.ColumnIndex].Name + "[" + ((OrderLineRow)row.Tag).RowKey + "]";
                cell.ErrorText = _revealAll && _errors.TryGetValue(field, out var m) ? m[0] : "";
            }
    }
}
```

`Submit` (「登録」) はエラーがあっても押せるままにします (押すことでエラーが見える)。

## 保存のタイミングと変更検出

画面によって 2 通りあり、変更検出 (`ChangeTracker`) の単位が変わります — [ChangeTracker § 一覧 (明細) を追跡する単位](Reference-ChangeTracker#一覧-明細-を追跡する単位--保存のタイミングで決まる)。

- **全部「登録」でまとめて保存**: 一覧全体に 1 つの tracker。
- **行の追加・削除・並べ替えはその場で保存、セルは「登録」で**: 行ごとに 1 つの tracker。

どちらも「保存しますか？」は `CanClose` / `CanCloseAsync` だけで出します。

## 落とし穴

| 症状 | 原因 | 対策 |
|---|---|---|
| コンボで選んだ値が空になる | `ShowRow` が編集中のコンボの編集コントロールに文字を入れた / 行ごとの `DataSource` を編集中に設定し直した | 編集コントロールへの書き込みは文字の列だけ。行ごとの選択肢は行を作るときに 1 回だけ設定 |
| Presenter が行を表示し直すと、また Presenter のイベントが来る | `Rows.Clear` / `CurrentCell` 設定 / `Value` 設定がグリッドのイベントを出す | 表示中フラグ (`_showing`) の間はイベントを翻訳しない |
| グリッド既定のエラーダイアログが出る | コンボの値が一覧にない等で `DataError` | `DataError` で `e.ThrowException = false` |
| 最小化中に例外 | 表示行がないときの `FirstDisplayedScrollingRowIndex` 設定 | 最小化中・表示行 0 のときはスクロールしない |
| Enter で下に移動してしまう | DataGridView の既定 | Form の `ProcessCmdKey` で Enter を拾い、編集中なら `EndEdit()` (拒否されたら留まる)、次の編集可能なセルへ |
| 選択行が読み直しで消える | 全行の置き換え | `ShowRows` の後に `SelectedRowKey` をキーで戻す |

## サードパーティのグリッド

分担と Presenter・View インターフェイス・テストは同じで、Form でつなぐイベントだけが変わります。C1FlexGrid の場合の対応:

| すること | DataGridView | C1FlexGrid |
|---|---|---|
| セルを離れるときに検査・拒否 | `CellValidating` (`e.Cancel`) | `ValidateEdit` (`e.Cancel`) |
| 編集確定の後 | `CellEndEdit` | `AfterEdit` |
| 行ごとの選択肢 | セルの `DataSource` | `SetupEditor` で `ComboList` / 列の `DataMap` |
| 行とキー | `Row.Tag` | `Row.UserData` |
| 選択行の変更 (取り消し可) | `SelectionChanged` (取り消し不可) | `BeforeRowColChange` (`e.Cancel`) / `AfterSelChange` |
| セルのエラー表示 | `ErrorText` (アイコン付き) | 既定なし: セルのスタイルや `OwnerDrawCell` で描く |
| Enter で右へ | 自前 | `KeyActionEnter = MoveAcross` |

## テスト

Presenter はグリッドなしでテストします。偽の View は行のリストを持ち、「ユーザーの操作」をメソッドにします:

```csharp
public bool Commit(int rowKey, string column, string text)     // セルに入力して離れる: 受け入れられたら true
{
    var e = new ValueChangingEventArgs<CellEdit>(new CellEdit { RowKey = rowKey, Column = column, Text = "" },
                                                 new CellEdit { RowKey = rowKey, Column = column, Text = text });
    CellCommitting?.Invoke(this, e);
    return !e.Cancel;
}

[Fact]
public void FullWidthAndFourDecimals_AreRewritten()
{
    Start();
    Assert.True(_view.Commit(_view.Keys[0], nameof(OrderLine.Quantity), "１．２３４５"));
    Assert.Equal(1.234m, _view.Row(_view.Keys[0]).Line.Quantity);
}
```

グリッドの操作 (上の表の下半分) は自動テストの外です。画面ごとに Windows での確認項目を残してください。

## 関連ページ

- [ChangeTracker](Reference-ChangeTracker) — 一覧を追跡する単位
- [フォーム入力の検証](HowTo-Validate-Form-Input) — `ErrorsInOrder(params fieldOrder)`
- [マスター/詳細](HowTo-Implement-Master-Detail) — 行ごとに UserControl + Presenter を持つ場合
- [Presenter をテストする](HowTo-Test-A-Presenter)
