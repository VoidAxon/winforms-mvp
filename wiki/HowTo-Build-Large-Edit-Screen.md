# HowTo: 入力項目の多い編集画面を作る (Supervising Controller)

入力項目が数十ある編集画面を、View インターフェイスを肥大化させずに作る方法です。結論は **Supervising Controller**: 普通の入力項目はフォームが入力モデルにデータバインドし、Presenter はモデルを操作します。選択肢の一覧・選択行・組み立てた表示など、バインドに向かないものだけを View のメンバーにします。

> 位置づけは [MVP パターンとは](Concept-MVP-Pattern) の「使い分けの目安」を参照してください。Passive View と Supervising Controller は連続したスペクトラムで、同じ画面の中で意図的に混ぜて構いません。

---

## 目次

- [1. 役割を分ける](#1-役割を分ける)
- [2. 入力モデルと View インターフェイス](#2-入力モデルと-view-インターフェイス)
- [3. 読み込みとバインド](#3-読み込みとバインド)
- [4. コントロールごとの注意点](#4-コントロールごとの注意点)
- [5. 検証と登録](#5-検証と登録)
- [6. 変更検出と閉じる](#6-変更検出と閉じる)
- [7. テスト](#7-テスト)
- [8. 代案: Passive View + 入力 DTO](#8-代案-passive-view--入力-dto)

---

## 1. 役割を分ける

すべての入力項目を View のプロパティにすると (Passive View)、項目ごとに **View インターフェイス・Form の実装・テスト用のフェイク View** の 3 か所に同じものを書くことになり、入力が 40 を超える画面では View インターフェイスだけで 40 メンバーを超えます。

| 置き場所 | 何を | 例 |
|---|---|---|
| **入力モデル** (バインド) | 利用者がそのまま入力する値 | 氏名、カナ、日付、時刻、メモ、コード値の選択 |
| **View のメンバー** (明示) | バインドに向かないもの | 選択肢の一覧 (`SetXxxOptions`)、一覧の選択行、複数の値を組み立てた表示 (`ShowXxx`)、表示中のタブ、フォーカス移動 |
| **Presenter の状態** | 画面の外から選ばれて決まるもの | 別画面で選んだ顧客・担当者、明細の一覧 |

項目数ではなく、**ふるまいの複雑さ** で決めてください。条件で表示が変わる項目や、Presenter がテストで観測したい UI 副作用は、明示メンバーにした方が分かりやすいことがあります。

## 2. 入力モデルと View インターフェイス

入力モデルは `BindableBase` を継承し、**保存用のエンティティとは別** に作ります (画面の編集セッションの契約です)。

```csharp
public sealed class CustomerEditInput : BindableBase
{
    private string _name = "", _kana = "", _memo = "";
    private DateTime _contractDate;
    private IReadOnlyList<int> _tagIds = new int[0];

    // 空と null を区別しない項目は "" に揃える (TextBox の "" とモデルの null が変更検出を狂わせないように)
    public string Name { get => _name; set => SetProperty(ref _name, value ?? ""); }
    public string Kana { get => _kana; set => SetProperty(ref _kana, value ?? ""); }
    public string Memo { get => _memo; set => SetProperty(ref _memo, value ?? ""); }
    public DateTime ContractDate { get => _contractDate; set => SetProperty(ref _contractDate, value.Date); }

    // コレクションは SetProperty が参照で比べるので、内容で比べてから通知する
    public IReadOnlyList<int> TagIds
    {
        get => _tagIds;
        set
        {
            var sorted = (value ?? new int[0]).Distinct().OrderBy(t => t).ToList();
            if (!sorted.SequenceEqual(_tagIds)) SetProperty(ref _tagIds, sorted);
        }
    }
}
```

View インターフェイスは「モデルを渡す」メンバーと、バインドに向かないものだけです:

```csharp
public interface ICustomerEditView : IWindowView, IValidatableView
{
    void Bind(CustomerEditInput input);                 // フォームがコントロールをバインドする
    void SetTagOptions(IReadOnlyList<Tag> tags);
    void ShowContact(ContactDisplay contact);           // 別画面で選んだ担当者の表示
    int? SelectedLineIndex { get; }
    void FocusError(string field);                      // 登録失敗時のフォーカス移動 (5 章)
    event EventHandler LineSelectionChanged;
}
```

## 3. 読み込みとバインド

Presenter が入力モデルを持ち、値を入れてから `View.Bind` に渡します。選択肢は先に設定してください (選択肢がないと値を選べないコントロールがあります)。

```csharp
private readonly CustomerEditInput _input = new CustomerEditInput();

protected override void OnInitialize(CustomerEditParameters p)
{
    var customer = _repository.Get(p.CustomerId);
    View.SetTagOptions(_masters.GetTags());

    _input.Name = customer.Name;
    _input.Kana = customer.Kana;
    _input.ContractDate = customer.ContractDate;
    _input.TagIds = customer.TagIds;
    View.Bind(_input);

    // モデルは Presenter 自身のもので寿命も同じ: 解除は不要
    _input.PropertyChanged += (s, e) => Revalidate();
    _tracker = TrackChanges(ReadModel());
}
```

Presenter が値を変えるとき (定例文の追記、担当者のコピーなど) も **モデルを変更** します。バインドが画面に反映するので、View の setter は要りません:

```csharp
private void OnAppendTemplate() => _input.Memo += PickTemplate();
```

フォーム側:

```csharp
void ICustomerEditView.Bind(CustomerEditInput input)
{
    txtName.Bind(input, x => x.Name);                                   // WinForms の TextBox
    poisonKana.BindProperty(input, x => x.Kana, nameof(Control.Text));  // TextBox を継承しない他社製コントロール
    dtpContract.Bind(input, x => x.ContractDate);                       // DateTime? なら ShowCheckBox = true で未入力を表す
    tagPicker.BindProperty(input, x => x.TagIds, nameof(TagPicker.CheckedIds),
        DataSourceUpdateMode.OnPropertyChanged, formattingEnabled: true);   // 4 章
}
```

## 4. コントロールごとの注意点

| 場面 | 書き方 |
|---|---|
| `TextBox` を継承しない他社製のテキストボックス | `BindProperty(model, x => x.Name, nameof(Control.Text))`。型ごとの `Bind` は WinForms の型にしか付いていません |
| 自由入力のコンボボックス (`DropDown`) | `cboMethod.BindText(model, x => x.Method)`。`ComboBox.Bind` は **`SelectedValue`** (一覧から選ぶ `DropDownList` 用) です |
| 「未入力」がある日付 (`DateTime?`) | `ShowCheckBox = true` にして `dtpDue.Bind(model, x => x.DueDate)`。チェックなし = `null`。値はモデルで変えます (コードで `Checked` を書き換えてもモデルには届きません) |
| インターフェイス型のプロパティ (`IReadOnlyList<int>` など) | `formattingEnabled: true` を指定します。既定の `false` では、WinForms は「同じ型かサブクラス」の値しか受け付けず、`List<int>` を入れると実行時に「値を指定した型にフォーマットできません」になります |
| 自作のコントロール | バインドで値を書き戻すには「プロパティ名 + `Changed`」のイベント (`CheckedIdsChanged`) が必要です (WinForms の規約)。例は [Data Binding](Reference-Data-Binding#自作他社製コントロール) |
| 変換に失敗した入力 | `formattingEnabled: true` のとき、変換できない入力ではモデルは **直前の有効な値のまま** です。`Binding.BindingComplete` で検出し、エラー中の登録を止めてください |

## 5. 検証と登録

検証はモデルに対して行います。`PropertyChanged` のたびにルールを評価し、項目の横のアイコンを更新します (`ErrorProviderDisplay.Map(nameof(CustomerEditInput.Name), txtName)`)。

登録 (Submit) に失敗したときは、**WinForms の慣習どおりメッセージボックスで知らせます**。アイコンだけでは「ボタンを押しても何も起きない」ように見えるためです。最初のエラーを画面の順で 1 つ出し、閉じたらその項目にフォーカスを移します (全部を一覧で出す書き方もできます):

```csharp
private static readonly string[] FieldOrder =
    { nameof(CustomerEditInput.Name), nameof(CustomerEditInput.Kana), nameof(CustomerEditInput.ContractDate) };

private bool TrySave()
{
    Revalidate();
    Validation.ShowAll();                 // 未訪問の項目のアイコンも出す
    if (Validation.HasErrors)
    {
        // 画面の順で最初のエラー。並び順にない項目やフォームレベルのエラー
        // (.NET Framework の [Compare] など) だけのときも、何か 1 つは必ず出す
        var field = FieldOrder.FirstOrDefault(f => Validation.GetErrors(f).Count > 0)
                    ?? Validation.Errors.Keys.First();
        Messages.ShowWarning(Validation.GetErrors(field)[0]);
        if (field != ValidationState.FormLevel)
            View.FocusError(field);       // どのコントロールかは Form が決める
        return false;
    }
    ...
}
```

「登録」ボタンはエラー中も有効のままにします (押したときに未訪問の項目のエラーを見せるため)。

## 6. 変更検出と閉じる

変更検出はモデルのスナップショットで行います。Presenter が持つ状態 (別画面で選んだ担当者・明細) も含めた読み取り用の型を作ると、1 つの `ChangeTracker` で済みます:

```csharp
private CustomerEditModel ReadModel() => new CustomerEditModel
{
    // 入力モデルの項目はすべて入れる (漏れた項目だけの変更は「保存しますか？」が出ずに失われる)
    Name = _input.Name, Kana = _input.Kana, Memo = _input.Memo, ContractDate = _input.ContractDate,
    TagIds = _input.TagIds.ToList(), ContactId = _contactId, Lines = _lines.Select(Copy).ToList(),
};

protected override bool CanClose(CloseReason reason)
{
    if (!_tracker.IsChangedWith(ReadModel())) return true;
    switch (Messages.ConfirmYesNoCancel("保存しますか？"))
    {
        case ConfirmResult.Cancel: return false;
        case ConfirmResult.No: return true;
        default:
            if (!TrySave(out var id)) return false;
            RequestClose(id, InteractionStatus.Ok);   // このクローズの結果
            return true;
    }
}
```

保存に成功したら `_tracker.AcceptChanges(model)`。詳しくは [ChangeTracker](Reference-ChangeTracker) と [ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) を参照してください。

## 7. テスト

フェイク View は `Bind` で受け取ったモデルを持つだけです。テストはモデルの値を直接変えます (実際のバインドと同じく `PropertyChanged` が飛ぶので、Presenter の再検証も走ります):

```csharp
private sealed class FakeView : ICustomerEditView
{
    public CustomerEditInput Input { get; private set; }
    public void Bind(CustomerEditInput input) => Input = input;
    public string FocusedField { get; private set; }
    public void FocusError(string field) => FocusedField = field;
    ...
}

[Fact]
public void Save_WithoutName_ShowsTheErrorAndFocusesTheField()
{
    var host = PresenterTestHost.Start(services, presenter, view, parameters);
    view.Input.Name = "";

    view.Binder.Dispatch(CustomerEditActions.Save);

    Assert.False(host.CloseRequested);
    Assert.Equal(nameof(CustomerEditInput.Name), view.FocusedField);
}
```

バインドそのもの (コントロールとモデルの往復、変換) は Presenter のテストでは確かめられません。注意点の多いコントロール (4 章) は、Windows 上の結合テストか手動確認で確かめてください。

## 8. 代案: Passive View + 入力 DTO

データバインドを使いたくない画面では、関連する入力をまとめた DTO を一度に読み書きする Passive View にできます:

```csharp
public interface ICustomerEditView : IWindowView
{
    CustomerEditInput ReadInput();        // スナップショット
    void ShowInput(CustomerEditInput input);
    event EventHandler InputChanged;
    ...
}
```

項目ごとのプロパティよりメンバーは大幅に減ります。対応付けは Form に 1 回書くだけで、テストのフェイクも単純です。代わりに「どの項目が変わったか」の粒度は失われ、Presenter が値を変えるたびに `ShowInput` を呼ぶ必要があります。

| | Supervising Controller (バインド) | Passive View + DTO |
|---|---|---|
| View のメンバー | 少ない | 少ない |
| Presenter → 画面の反映 | モデルを変えるだけ | `ShowInput` を呼ぶ |
| 入力の変化の通知 | モデルの `PropertyChanged` (項目単位) | `InputChanged` (まとめて) |
| 注意が要るところ | コントロールごとのバインド (4 章) | 表示時に入力イベントを起こさないこと |

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — Passive View と Supervising Controller の使い分け
- [Data Binding](Reference-Data-Binding) — `BindProperty` / `Bind` / `BindRadioGroup`
- [フォーム入力を検証する](HowTo-Validate-Form-Input) — `ValidationState` と `ErrorProviderDisplay`
- [Presenter をテストする](HowTo-Test-A-Presenter)
