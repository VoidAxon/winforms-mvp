# ViewAction システム

UI イベント (ボタンクリック等) を **宣言的なアクション** に変換し、Presenter のハンドラと結びつける仕組みです。
WPF の `ICommand` を WinForms 向けに持ち込んだものと考えるとほぼ正確で、`CanExecute` による自動 `Enabled` 制御まで含めて同等の体験を提供します。

> **位置づけ**: ViewAction は本フレームワークの **中核機能** です。Presenter が `Button` を一切知らずに済むのは、この仕組みのおかげです。
>
> ただし **必須ではありません**。ボタンが数個の単純な画面なら、View インターフェイスに `event EventHandler SaveRequested;` のような意味イベントを置き、Presenter が購読する素の MVP で十分です。ViewAction が効いてくるのは、操作が多くてイベントが増えすぎる画面や、`CanExecute` による Enabled 制御・非同期ハンドラの多重実行防止・ミドルウェアといったフレームワークの支援を使いたい画面です。同じアプリ内で画面ごとに使い分けて構いません。

---

## 目次

- [構成要素](#構成要素)
- [基本的な使い方](#基本的な使い方)
- [ActionBinder プロパティパターン](#actionbinder-プロパティパターン)
- [ActionKey の定義方針](#actionkey-の定義方針)
- [CanExecute と自動 Enabled 制御](#canexecute-と自動-enabled-制御)
- [パラメータ付きアクション](#パラメータ付きアクション)
- [状態変化への対応](#状態変化への対応)
- [非同期アクション](#非同期アクション)
- [一括バインド (AddRange)](#一括バインド-addrange)
- [対応するコントロール](#対応するコントロール)
- [Implicit パターン vs Explicit パターン](#implicit-パターン-vs-explicit-パターン)
- [ミドルウェアパイプライン](#ミドルウェアパイプライン)
- [関連ページ](#関連ページ)

---

## 構成要素

| コンポーネント | 役割 |
|--------------|------|
| `ViewAction` | アクションの **不変な識別子** (struct)。`ViewAction.Create("Module.Save")` で作る |
| `ViewActionBinder` | UI コントロール (Button、MenuItem 等) を ViewAction にマッピングする (View 側) |
| `ViewActionDispatcher` | ViewAction をハンドラに振り分ける (Presenter 側)。`CanExecute` 判定も担う |

3 者の関係:

```
┌──────────────┐                ┌──────────────────────┐
│  View (Form) │                │  Presenter           │
│              │                │                      │
│   Button     │                │   Dispatcher         │
│     │        │                │     .Register(       │
│     │        │   ActionBinder │       Save,          │
│     │ ───────┼───────┐        │       OnSave,        │
│   Button     │  add  │        │       canExecute:    │
│     │        │       ▼        │         () => HasUC) │
│              │     ┌─────────┐│                      │
│ ActionBinder │ ──▶ │ Dispatch│└──────────────────────┘
│              │     └─────────┘
└──────────────┘         │
                         ▼
                  CanExecute 判定 → Handler 実行 → 全 Binder の Enabled 自動更新
```

---

## 基本的な使い方

最小構成は以下の 4 ステップです。

### Step 1: ActionKey を定義する

```csharp
public static class UserEditorActions
{
    public static readonly ViewAction Save   = ViewAction.Create("UserEditor.Save");
    public static readonly ViewAction Delete = ViewAction.Create("UserEditor.Delete");
    public static readonly ViewAction Cancel = ViewAction.Create("UserEditor.Cancel");
}
```

### Step 2: View インターフェイスで `ActionBinder` を公開する

```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    bool HasSelectedUser { get; }
}
```

### Step 3: Form 内部で `ViewActionBinder` を構成する

```csharp
public class UserEditorForm : Form, IUserEditorView
{
    private Button _btnSave;
    private Button _btnDelete;
    private ViewActionBinder _binder;

    public ViewActionBinder ActionBinder => _binder;

    public UserEditorForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();
        _binder.Add(UserEditorActions.Save,   _btnSave);
        _binder.Add(UserEditorActions.Delete, _btnDelete);
        // DO NOT call _binder.Bind(...) here — framework does it
    }

    // ... プロパティ実装
}
```

### Step 4: Presenter でハンドラを登録する

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(UserEditorActions.Save, OnSave);

        Dispatcher.Register(UserEditorActions.Delete, OnDelete,
            canExecute: () => View.HasSelectedUser);   // 選択があるときだけ有効
        Dispatcher.Register(UserEditorActions.Cancel, OnCancel);

        // After this method returns, the framework calls
        //   View.ActionBinder?.Bind(Dispatcher);
        // automatically. No manual call needed.
    }

    private void OnSave()   { /* ... */ }
    private void OnDelete() { /* ... */ }
    private void OnCancel() { /* ... */ }
}
```

---

## ActionBinder プロパティパターン

### なぜ「メソッド」ではなく「プロパティ」か

過去設計では `void BindActions(ViewActionDispatcher dispatcher)` というメソッドを View インターフェイスに置いていましたが、現在は **`IViewActionBinder ActionBinder { get; }` プロパティ** に統一されています（`IWindowView` から継承 — View インターフェイス側での再宣言は不要です）。

| 観点 | プロパティの利点 |
|------|----------------|
| **副作用の防止** | プロパティアクセスは「データ取得」だけ。複雑なロジックを潜ませると即座に code smell になる |
| **テスト安全性** | モックでプロパティを返すだけで済む。メソッド呼び出しのように業務処理を誤発火しない |
| **WPF との整合** | WPF の `ICommand` バインドと同じメンタルモデル |

### `InitializeActionBindings()` は UI バインドだけに使う

> **業務ロジックを `InitializeActionBindings()` に書かないこと。**

```csharp
// ❌ NG — DB 呼び出しが View の初期化に紛れ込んでいる
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(StandardActions.Save, _saveButton);

    var users = _database.GetAllUsers();  // ❌ こんなものを書かない
    if (users.Count > 0)
        _binder.Add(UserActions.Delete, _deleteButton);
}

// ✅ OK — 純粋なバインドのみ
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(StandardActions.Save,   _saveButton);
    _binder.Add(StandardActions.Delete, _deleteButton);
    _binder.Add(UserActions.Edit,     _editButton);
}
```

データに応じた条件付き有効化が必要な場合は、`CanExecute` predicate で制御してください (バインド自体は無条件に行う)。

---

## ActionKey の定義方針

### 静的クラスにまとめる

文字列リテラルを直に渡さず、必ず静的クラスで定数として宣言します。標準的な動詞 (Save / Cancel / Delete / Refresh / Ok / ...) は**フレームワーク同梱の `StandardActions`** をそのまま使えます — 再定義しないでください。自前の静的クラスで宣言するのは **モジュール固有のアクション** だけです。

```csharp
using WinformsMVP.MVP.ViewActions;

// 標準動詞は出荷済みの StandardActions を使う (再定義しない)
Dispatcher.Register(StandardActions.Save, OnSave);

// モジュール固有のアクションだけ静的クラスで宣言する
public static class UserEditorActions
{
    public static readonly ViewAction EditUser       = ViewAction.Create("UserEditor.Edit");
    public static readonly ViewAction AddUser        = ViewAction.Create("UserEditor.Add");
    public static readonly ViewAction ChangePassword = ViewAction.Create("UserEditor.ChangePassword");
}
```

**命名規約**: `Module.Action` 形式 (ドット区切り)。これにより文字列が衝突せず、ログでも判別しやすくなります。

### `ViewActionFactory` でプレフィックスを共通化

同じプレフィックスを繰り返したくない場合は、Factory を使います。

```csharp
public static class MainActions
{
    private static readonly ViewActionFactory Factory =
        ViewAction.Factory.WithQualifier("Main");

    public static readonly ViewAction New     = Factory.Create("New");
    public static readonly ViewAction Open    = Factory.Create("Open");
    public static readonly ViewAction Save    = Factory.Create("Save");
}
// 生成される ActionKey: "Main.New", "Main.Open", "Main.Save"
```

---

## CanExecute と自動 Enabled 制御

`Dispatcher.Register` の第 3 引数 `canExecute` を指定すると、フレームワークがバインド済みコントロールの `Enabled` プロパティを自動更新します。

```csharp
Dispatcher.Register(
    StandardActions.Delete,
    OnDelete,
    canExecute: () => View.HasSelectedItem);
```

- `View.HasSelectedItem` が `false` のとき、`_deleteButton.Enabled` は自動で `false` になる
- 選択が変わったタイミングで `Dispatcher.RaiseCanExecuteChanged()` を呼べば `Enabled` が追従する

### いつ再評価されるか

| トリガー | 動作 |
|---------|------|
| **任意のアクション実行直後** | 自動 (フレームワークが `ActionExecuted` 後に全 CanExecute を再評価) |
| **非同期ハンドラの開始時・終了時** | 自動 (実行中のアクションは `CanDispatch == false`) |
| **アクション実行を伴わない状態変化** | 手動 (`Dispatcher.RaiseCanExecuteChanged()` を呼ぶ) |

詳細は [状態変化への対応](#状態変化への対応) を参照。

---

## パラメータ付きアクション

ハンドラに値を渡したい場合は `Register<T>` を使います。

```csharp
// Step 1: パラメータ型を持つハンドラを登録
Dispatcher.Register<string>(
    DocumentActions.Open,
    OnOpenDocument,
    canExecute: () => true);

// Step 2: ディスパッチ時にパラメータを渡す
Dispatcher.Dispatch(DocumentActions.Open, "C:\\path\\to\\file.txt");

// Handler
private void OnOpenDocument(string filePath)
{
    var content = Files.ReadAllText(filePath);
    // ...
}
```

**型不一致は実行時拒否**: `Register<string>` に対して `Dispatch(action, 42)` (int) を呼ぶと、既定 (Lenient) では Dispatcher はハンドラを呼ばずにログだけ出します。これによりミドルウェアパイプラインも走りません (後述の Strict モードでは例外になります)。

### 型付き ActionKey (`ViewAction<T>`)

キー自体にペイロード型を持たせると、型不一致が **コンパイルエラー** になります。Form 側 (`Add` の値の取り出し) と Presenter 側 (ハンドラの引数) の両方が同じキーの `T` に縛られるので、片方だけ型を変えるとビルドが通りません。

```csharp
public static class DocumentActions
{
    public static readonly ViewAction<string> Open    = ViewAction.Create<string>("Document.Open");
    public static readonly ViewAction<int>    SetZoom = ViewAction.Create<int>("Document.SetZoom");
}

// View: valueSelector は int を返さなければならない
_binder.Add(DocumentActions.SetZoom, _numZoom, n => (int)n.Value);

// Presenter: ハンドラの引数は int (同期・非同期どちらも可)
Dispatcher.Register(DocumentActions.SetZoom, zoom => _model.Zoom = zoom);
Dispatcher.Register(DocumentActions.Open, async (path, ct) => await LoadAsync(path, ct));

// テスト: CapturingActionBinder も型付きで呼べる
view.Binder.Dispatch(DocumentActions.SetZoom, 150);
```

- **任意** です。従来の `ViewAction` + `Register<T>` はそのまま使え、同じ Dispatcher 内で混在できます。
- 型付きの API: `ViewAction.Create<T>` / `ViewActionFactory.Create<T>`、Dispatcher の `Register` (`Action<T>` / `Func<T, Task>` / `Func<T, CancellationToken, Task>`)・`Dispatch`・`DispatchAsync`・`CanDispatch`、Binder の `Add(key, control, valueSelector)`。
- `ViewAction<T>` から `ViewAction` への暗黙変換はありません (型付きキーが型なしのオーバーロードに黙って渡らないように)。型なしの API が必要な場面 (ミドルウェアでのキー比較、`Add(key, controls)` のような値なしバインドなど) では `.Key` を使います。`Name` は `.Key.Name` と同じです。
- 値を取り出さないボタン (`Add(key, button)`) には型なしの `ViewAction` を使います。

---

## 厳格な検証モード (DispatchValidationMode)

Dispatcher の既定動作は **グレースフルデグラデーション** です。以下の 2 つの「配線ミス」は、ログを出すだけで握りつぶされます (本番では堅牢で望ましい挙動):

- **未登録のアクションキーをディスパッチ** (= `Register` し忘れ、またはキーのタイプミス) → 何も起きない
- **ペイロードの型不一致** (`Register<string>` に `int` を渡す等) → ハンドラは呼ばれない

便利な反面、開発中はこれらが「ボタンを押しても無反応」という **静かな失敗** になり、原因の特定が難しくなります。`ViewActionDispatcher.ValidationMode` でこの挙動を切り替えられます。

```csharp
public enum DispatchValidationMode
{
    Lenient = 0,  // 既定。ログを出して無視 (本番向け)
    Strict  = 1,  // 上記 2 つの配線ミスで InvalidOperationException を投げる
}
```

**Strict にしてよい範囲 / してはいけない範囲:**

| 事象 | Lenient (既定) | Strict |
|------|---------------|--------|
| 未登録キーのディスパッチ | ログのみ | **例外** |
| ペイロード型不一致 | ログのみ | **例外** |
| ハンドラが例外を投げた | ログ後に再スロー | ログ後に再スロー (**変化なし**) |
| `CanExecute` が `false` (無効状態) | 何もしない | 何もしない (**変化なし**) |

Strict が対象とするのは **設定ミスだけ** です。ハンドラの例外はどちらのモードでもログ出力後に再スローされ (`Application.ThreadException` へ)、`CanExecute` の例外は常にログ出力して「無効」とみなします。無効化されたアクション (`CanExecute == false`) はエラー扱いしません。

なお、次の配線ミスはモードに関係なく **常に登録時点で例外** になります:

| 事象 | 挙動 |
|------|------|
| `default(ViewAction)` (未初期化のキー) を `Register` / `ViewActionBinder.Add` に渡す | `ArgumentException` (`Dispatch` に渡した場合は無視) |
| `Register` に null ハンドラを渡す | `ArgumentNullException` |
| 同じコントロールを 2 つのアクションに `Add` する | `InvalidOperationException` (以前は最後の 1 つが黙って残っていた) |

**推奨される使い方**: Debug ビルドでのみ Strict を有効化します。`IDispatcherConfigurer` をサービスプロバイダに登録することで配線できます (全 Dispatch より前に適用されます):

```csharp
ServiceLocator.Configure(reg => reg.RegisterInstance<IDispatcherConfigurer>(
    new ActionDispatcherConfigurer(d =>
    {
#if DEBUG
        d.ValidationMode = DispatchValidationMode.Strict;
#endif
    })));
```

これにより「`Register` し忘れ / キーのタイプミス / ペイロード型違い」が、初回ディスパッチ時に **その場で例外として表面化** します。本番ビルドは既定の Lenient のままなので、ユーザー影響はありません。

---

## 状態変化への対応

### Pattern 1: アクション駆動 (自動)

アクションが実行されたあとは、フレームワークが自動で全 CanExecute を再評価します。

```csharp
private void OnSave()
{
    SaveDocument();
    // この直後にフレームワークが Dispatcher.ActionExecuted を発火し、
    // 全ての CanExecute が再評価される。手動コードは不要。
}
```

### Pattern 2: 状態駆動 (手動トリガー)

ユーザー入力等、アクション実行を伴わない状態変化では、明示的に再評価をトリガーします。

```csharp
protected override void OnInitialize()
{
    View.SelectionChanged += (s, e) =>
    {
        // 選択状態が変わったので CanExecute を再評価
        Dispatcher.RaiseCanExecuteChanged();
    };
}
```

非同期ハンドラの開始・完了時の再評価はフレームワークが自動で行います ([非同期アクション](#非同期アクション) 参照)。

### WPF との対応

| 機能 | WPF `ICommand` | このフレームワーク |
|------|---------------|------------------|
| イベント名 | `CanExecuteChanged` | `CanExecuteChanged` |
| 手動トリガー | `command.RaiseCanExecuteChanged()` | `dispatcher.RaiseCanExecuteChanged()` |
| 自動トリガー | なし | `ActionExecuted` イベントで自動 (拡張機能) |

---

## 非同期アクション

`Dispatcher.Register` には非同期ハンドラ用のオーバーロードがあります。`async` ラムダや `async Task` メソッドはこちらに束縛されるので、**`async void` ハンドラを書く必要はありません**。

| オーバーロード | 用途 |
|---|---|
| `Register(key, Func<Task>)` | パラメータなし |
| `Register(key, Func<CancellationToken, Task>)` | パラメータなし + トークン |
| `Register<T>(key, Func<T, Task>)` | パラメータ付き |
| `Register<T>(key, Func<T, CancellationToken, Task>)` | パラメータ付き + トークン |

```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(CustomerActions.Refresh, OnRefreshAsync);
    Dispatcher.Register(CustomerActions.Save, OnSaveAsync, canExecute: () => View.HasChanges);
}

private async Task OnRefreshAsync(CancellationToken ct)
{
    View.StatusMessage = "読み込み中...";
    try
    {
        View.Customers = await _repository.GetAllAsync(ct);   // ct はウィンドウが閉じるとキャンセルされる
        View.StatusMessage = "完了";
    }
    catch (IOException ex)
    {
        // 想定内のエラーはハンドラ内で捕まえてユーザーに伝える
        Messages.ShowError($"読み込みに失敗しました: {ex.Message}");
    }
}
```

フレームワークが行うこと:

- **実行中はアクションを無効化** — ハンドラの実行中は `CanDispatch` が `false` になり、バインド済みコントロールはグレーアウトします。連打などの重複トリガーは無視されます。開始時と終了時に全 `CanExecute` が再評価されるので、ビジーフラグや手動の `RaiseCanExecuteChanged()` は不要です (他のアクションも同時に止めたいときだけ、`canExecute` が参照するフラグを持ちます — `samples/.../AsyncDemo/AsyncDemoPresenter.cs` 参照)。
- **完了時に `ActionExecuted`** — 同期ハンドラと同じく、ハンドラが正常に終わった時点で発火します。
- **失敗はログ出力・再有効化のあと再スロー** — 未処理の例外はログに出してアクションを再び有効にし、そのあと通常の WinForms のイベントハンドラと同じように外へ伝わります (`Application.ThreadException` で受けられます)。同期部分の例外は `Dispatch` からそのまま、`await` の後で起きた例外は `Dispatch` を呼んだときの `SynchronizationContext` に投げ直されます (`async void` と同じ)。想定内のエラーはハンドラ内で `try`/`catch` して `Messages` で伝え、想定外のものは `Program.cs` の `Application.ThreadException` で受けます。
- **破棄時のキャンセルは静か** — トークンは Presenter の `Lifetime` で、Presenter の破棄 (ウィンドウを閉じる) と同時にキャンセルされます。これによる `OperationCanceledException` はエラー扱いされません。ユーザーの「キャンセル」ボタンを実装する場合は `CancellationTokenSource.CreateLinkedTokenSource(ct)` でリンクします。

### 完了を待つ: `DispatchAsync`

`Dispatch` は非同期ハンドラの完了を待たずに戻ります。完了を待ちたい場合 (主にテスト、またはアクションの後に処理を続けたいコード) は `DispatchAsync` を使います。ハンドラが失敗すると、例外を投げ直す代わりに **返るタスクがその例外で fault** します (テストでは `await Assert.ThrowsAsync<...>(() => view.Binder.DispatchAsync(...))`)。非同期ハンドラのテストは `Dispatch` ではなく `DispatchAsync` で書いてください — `Dispatch` の場合、`await` 後の例外はコンテキストに投げ直されるだけで、テストの失敗にならないことがあります。

```csharp
await Dispatcher.DispatchAsync(CustomerActions.Refresh);
// テストではモック View の CapturingActionBinder から:
await view.Binder.DispatchAsync(CustomerActions.Refresh);
```

### ミドルウェアとの関係

ミドルウェアが包むのはハンドラの**同期部分 (最初の `await` まで)** だけです。非同期処理全体を計測・監査するには `ctx.Completion` (ハンドラのタスク。同期ハンドラでは完了済みタスク) を観察します。`ctx.CancellationToken` はハンドラに渡されたトークンです。`await` 後の失敗はミドルウェアがすでに戻った後なので、`ctx.Exception` ではなく Dispatcher のログで報告されます。非同期ハンドラを開始した後でミドルウェアが例外を投げた場合も、ハンドラは最後まで追跡され (完了までアクションは無効のまま)、`ActionExecuted` は発火しません。

```csharp
Dispatcher.Use((ctx, next) =>
{
    var sw = Stopwatch.StartNew();
    next(ctx);
    ctx.Completion.ContinueWith(_ => Logger.LogInformation("{Action}: {Ms}ms", ctx.Action, sw.ElapsedMilliseconds));
});
```

---

## 一括バインド (AddRange)

似た UI が大量にある場合 (アンケート用 RadioButton 群等)、`AddRange` で 1 行にまとめられます。

### タプル形式 (推奨)

```csharp
_binder.AddRange(
    (ThemeActions.Light,         _lightRadio),
    (ThemeActions.Dark,          _darkRadio),
    (ThemeActions.Auto,          _autoRadio),
    (ThemeActions.HighContrast,  _highContrastRadio)
);
```

### Dictionary 形式

```csharp
var mapping = new Dictionary<ViewAction, RadioButton>
{
    [QuestionActions.StronglyDisagree] = _radio1,
    [QuestionActions.Disagree]         = _radio2,
    [QuestionActions.Neutral]          = _radio3,
    [QuestionActions.Agree]            = _radio4,
    [QuestionActions.StronglyAgree]    = _radio5,
};
_binder.AddRange(mapping);
```

**得られるメリット**: 行数が N 行 → 1 行になる、追加・削除の保守が楽、対応関係が一目で分かる。

---

## 対応するコントロール

`ViewActionBinder` がサポートするコントロールタイプは以下です。

| コントロール | 購読するイベント | 備考 |
|-------------|---------------|------|
| `CheckBox` | `CheckedChanged` | チェック状態が変わるたびにアクション発火 |
| `RadioButton` | `CheckedChanged` | 同上 |
| `ButtonBase` | `Click` | 通常のボタン |
| `ComboBox` | `SelectionChangeCommitted` | ユーザーが選択を確定したときのみ発火 (programmatic な `SelectedIndex` 変更では発火しない) |
| `NumericUpDown` | `ValueChanged` | 値変更で発火。programmatic な `Value` 代入でも発火 |
| `DateTimePicker` | `ValueChanged` | 同上 |
| `MonthCalendar` | `DateChanged` | 同上。`DateRangeEventHandler` を内部でアダプトして購読 |
| `ToolStripItem` | `Click` | `ToolStripButton`、`ToolStripMenuItem` 等 |
| `Control` (fallback) | `Click` | 上記に当てはまらない任意のコントロール |

**重要**: `CheckBox` と `RadioButton` は `Click` ではなく `CheckedChanged` に紐付くため、キーボードや programmatic な変更でも発火します。

**注意 (値系コントロール)**: `NumericUpDown` / `DateTimePicker` / `MonthCalendar` は `ComboBox` のような「確定時のみ」イベントを持たないため、コード上で `Value` / `SelectionStart` を代入したときも発火します。初期化やデータ再設定で意図せず発火させたくない場合は、代入の前後で `Unbind()` / `Bind()` するか、ガードフラグで抑制してください。

### コントロールの値をペイロードとして渡す

`Add(action, control, valueSelector)` オーバーロードを使うと、トリガー時にコントロールの現在値を読み取り、そのままアクションのペイロードとして渡せます。View 側に if-else や `ActionRequest` の配線を書く必要はありません。

```csharp
// View: バインドと同じ場所に「値の取り出し方」を書く
_binder.Add(SettingsActions.SetMode,  _checkBox, c => c.Checked);         // bool
_binder.Add(SettingsActions.SetZoom,  _numZoom,  n => (int)n.Value);      // int
_binder.Add(SettingsActions.PickDate, _calendar, c => c.SelectionStart);  // DateTime

// Presenter: 型付きハンドラを登録するだけ
Dispatcher.Register<bool>(SettingsActions.SetMode, OnSetMode);
Dispatcher.Register<int>(SettingsActions.SetZoom, OnSetZoom);
```

Implicit パターンでは `Dispatcher.Dispatch(key, payload)` に直接流れ、Explicit パターンでは値付きの `ActionRequestEventArgs<T>` が `ActionTriggered` 経由で発行されます。値を指定しない従来の `Add(action, controls)` は引き続きペイロード無しで動作します。

### CanExecute と CheckBox/RadioButton

`CanExecute` が制御するのは **`Enabled` プロパティ** (グレーアウト) であって、`Checked` ではありません。

```csharp
// ログイン中のときだけチェックを変更可能にする
Dispatcher.Register(
    SettingsActions.ToggleAutoSave,
    OnToggleAutoSave,
    canExecute: () => _isUserLoggedIn);
```

### 一覧のダブルクリック (行を開く)

「行のダブルクリック = 編集」は **グリッドを Binder に登録しません**。登録したコントロールの `Enabled` は `canExecute` に従うので、「編集」(選択行があるときだけ実行可) にグリッドを登録すると、未選択の間はグリッドごと無効になり、行を選ぶことすらできなくなります。

View が意味のあるイベントを出し、Presenter がそれを同じアクションとしてディスパッチします。`Dispatch` は `canExecute` を確認するので、ボタンと同じ条件で動きます:

```csharp
// View インターフェイス
event EventHandler RecordActivated;

// Form: 見出し行 (RowIndex = -1) のダブルクリックは除く
private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
{
    if (e.RowIndex >= 0) RecordActivated?.Invoke(this, EventArgs.Empty);
}

// Presenter
protected override void OnViewAttached()
{
    SubscribeViewEvent(h => View.RecordActivated += h, h => View.RecordActivated -= h,
                       () => Dispatcher.Dispatch(RecordListActions.Edit));
}
```

`ListBox` / `ListView` の `DoubleClick`、`Enter` キーでの決定も同じ書き方です。

### カスタムコントロールに対応する

デフォルトに無いコントロールや、別のイベントで発火させたい場合は `RegisterStrategy<T>()` で登録します。オーバーロードは 2 つあり、コントロールのイベントのデリゲート型で使い分けます。

**1. イベントが `EventHandler` 型の場合** (Click / `*Changed` など) — attacher / detacher を渡します。

```csharp
_binder.RegisterStrategy<TrackBar>(
    (c, h) => c.ValueChanged += (EventHandler)h,
    (c, h) => c.ValueChanged -= (EventHandler)h);
```

**2. イベントが `EventHandler` 以外のデリゲート型の場合** (`MonthCalendar.DateChanged` の `DateRangeEventHandler`、`TreeView.AfterSelect` の `TreeViewEventHandler` 等) — 購読してから「解除コールバック」を返す `subscribe` オーバーロードを使います。

```csharp
_binder.RegisterStrategy<TreeView>((c, h) =>
{
    TreeViewEventHandler d = (s, e) => ((EventHandler)h)(s, e);
    c.AfterSelect += d;
    return () => c.AfterSelect -= d;   // captures the same instance -> clean detach on Unbind
});
```

`subscribe` が返すクロージャは購読した実際のデリゲートインスタンスをキャプチャするため、`Unbind()` が正しく解除できます (ラムダで包んだ別インスタンスを `-=` しようとして解除できない、という問題を回避します)。`EventHandler` 版 (1.) は内部でこの `subscribe` 版に橋渡しされています。

実装の詳細は `src/WinformsMVP/MVP/ViewActions/ViewActionBinder.cs` を参照してください。

---

## Implicit パターン vs Explicit パターン

ViewAction の使い方には **2 つのパターン** があります。

### Implicit パターン (推奨・ほぼ常にこちら)

フレームワークが `View.ActionBinder.Bind(Dispatcher)` を **自動的に** 呼び、CanExecute による UI 自動更新も働きます。
コードが最小限で済み、本ページの上半分で示してきたパターンがこれです。

| 観点 | Implicit パターン |
|------|----------------|
| 自動 Bind | ✅ あり |
| 自動 CanExecute 更新 | ✅ あり |
| コード量 | 最小 |
| デバッグの見通し | 低い (Magic) |
| 使いどころ | 通常の CRUD など、ほとんどのケース |

### Explicit パターン

View が `ActionRequest` イベントを明示的に発火し、Presenter が明示的に購読する形です。主に既存 Form の段階的な移行向けで、通常は Implicit パターンを使ってください。
**ActionBinder プロパティは `null` を返し**、View 自身が `_binder.Bind()` (Dispatcher なし版) を呼びます。

> **ルーティングは呼んだ `Bind` で決まります。** `Bind(dispatcher)` は常に Dispatcher へ派発し、その後で `ActionTriggered` を通知として発火します。`Bind()` は `ActionTriggered` を発火するだけです。`ActionTriggered` を購読しても派発の有無は変わりません (以前は購読者がいると派発をスキップしていたため、ステータスバー表示などの購読だけで全ボタンが無反応になっていました)。Implicit パターンの View で `ActionTriggered` を通知 (ログ・ステータス表示) に使うのは安全です。

```csharp
// View 側
public ViewActionBinder ActionBinder => null;  // ← 自動 Bind を抑止

public event EventHandler<ActionRequestEventArgs> ActionRequest;

private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(UserEditorActions.Save, _btnSave);
    _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
    _binder.Bind();  // ← Dispatcher なし版 (イベントだけ発行)
}

// Presenter 側
protected override void OnViewAttached()
{
    View.ActionRequest += OnViewActionTriggered;   // ← 基底クラスのヘルパー
    Disposable.Create(() => View.ActionRequest -= OnViewActionTriggered).DisposeWith(Disposables);

    // Binder は Dispatcher に接続されていないため、ボタンの有効/無効は自動更新されない。
    // 可否は意味のある View メンバー経由で伝える (Form 側が Enabled に反映する)。
    View.SelectionChanged += OnSelectionChanged;
    Disposable.Create(() => View.SelectionChanged -= OnSelectionChanged).DisposeWith(Disposables);
}

private void OnSelectionChanged(object sender, EventArgs e) => View.CanDelete = View.HasSelection;
```

| 観点 | Explicit パターン |
|------|---------------|
| イベント購読が見える | ✅ F12 ナビ可能 |
| ブレークポイント設定 | ✅ できる |
| 自動 CanExecute 更新 | ❌ コントロールの有効/無効は自動同期されない。`View.CanDelete` のような View メンバーで伝える (`RaiseCanExecuteChanged()` だけでは反映されない) |
| コード量 | 多い |
| 使いどころ | 既存 Form の段階的移行・動作を完全に追跡したい場合 |

### `OnViewActionTriggered` ヘルパー

`PresenterBase` には Explicit パターン用のヘルパーがあります。

```csharp
protected void OnViewActionTriggered(object sender, ActionRequestEventArgs e)
{
    DispatchAction(e);
}

protected void DispatchAction(ActionRequestEventArgs e)
{
    if (e == null) return;

    var key = e.ActionKey;
    object payload = null;

    if (e is IActionRequestEventArgsWithValue valueProvider)
        payload = valueProvider.GetValue();

    Dispatcher.Dispatch(key, payload);
}
```

これにより、Explicit パターンでも `View.ActionRequest += OnViewActionTriggered;` の 1 行で済みます (手動の if-else チェーンは不要)。

### 選び方

ほとんどのアプリケーションは **Implicit パターン** で十分です。Explicit は以下のような状況で:

- アクションルーティングの問題をデバッグする
- 動作の透明性を最大化したい
- フレームワークの動作を完全に理解したい (学習目的)

---

## ミドルウェアパイプライン

`ViewActionDispatcher` には **オプトインのミドルウェアパイプライン** があり、横断的関心事 (監査ログ、パフォーマンス計測、認可、エラー処理戦略等) を集約できます。

> **シンプルな Presenter にはミドルウェアは不要です**。`Use(...)` を呼ばない限り、ファストパス (ゼロ・オーバーヘッド) で動きます。

### こんなときは別の手段を使う

| やりたいこと | ミドルウェアではなくこちら |
|------------|------------------------|
| 1 つのアクションを実行するかしないか判断 | `canExecute: () => ...` |
| 成功時に状態変化に反応 | `Dispatcher.ActionExecuted` イベント |
| 特定のハンドラだけログを取りたい | そのハンドラ内で `Logger.LogInformation` |
| 横断的処理が一切ない通常 CRUD | ミドルウェアを使わない |

### メンタルモデル: オニオン

ASP.NET Core と同じ「オニオンモデル」です。各ミドルウェアは `(context, next)` を受け取り、`next(context)` の前後で前処理・後処理を実行します。`next` を呼ばないとパイプラインが短絡し、ハンドラは実行されません。

```
Dispatch(Save)
  ┌─ AuditMiddleware.pre
  │  ┌─ ErrorDialogMiddleware.try {
  │  │   ┌─ PerformanceMiddleware (Stopwatch.Start)
  │  │   │   ┌─ [Handler: OnSave]       ← 末端
  │  │   │   └─
  │  │   └─ PerformanceMiddleware (Stopwatch.Stop, log if slow)
  │  └─ ErrorDialogMiddleware } catch { ShowDialog }
  └─ AuditMiddleware.post   (context.Exception を見て監査記録)
```

### 2 つの登録階層

#### Global (アプリ全体) — 最外殻

```csharp
// ServiceLocator (DI なし) の場合
ServiceLocator.Configure(reg => reg.RegisterInstance<IDispatcherConfigurer>(
    new ActionDispatcherConfigurer(d => d
        .Use(new AuditMiddleware(auditSink, () => CurrentUser.Name))
        .Use(new ErrorDialogMiddleware(messages, dispatchLogger)))));

// M.E.DI の場合
services.AddSingleton<IDispatcherConfigurer>(
    new ActionDispatcherConfigurer(d => d
        .Use(new AuditMiddleware(auditSink, () => CurrentUser.Name))
        .Use(new ErrorDialogMiddleware(messages, dispatchLogger))));
```

#### Local (Presenter ごと) — グローバルの内側

```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Use(new PerformanceMiddleware(Logger, slowThresholdMs: 50));
    Dispatcher.Register(StandardActions.Save, OnSave);
}
```

「**Global が先に登録 = 最外殻**」が契約です。`PresenterBase.SetView` が `RegisterViewActions` より前に Global ミドルウェアを構成するので、Presenter 側がそれを迂回することはできません。監査・認可等の絶対迂回不可な処理は **Global** に置きます。

### ミドルウェアの書き方

再利用可能な場合は `IDispatchMiddleware` を実装:

```csharp
public sealed class PerformanceMiddleware : IDispatchMiddleware
{
    private readonly ILogger _logger;
    private readonly int _thresholdMs;

    public PerformanceMiddleware(ILogger logger, int thresholdMs = 100)
    {
        _logger = logger ?? NullLogger.Instance;
        _thresholdMs = thresholdMs;
    }

    public void Invoke(DispatchContext context, DispatchDelegate next)
    {
        var sw = Stopwatch.StartNew();
        try { next(context); }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds > _thresholdMs)
                _logger.LogWarning("Slow dispatch: {Action} took {Ms}ms",
                    context.Action, sw.ElapsedMilliseconds);
        }
    }
}
```

1 回限りの用途ならインライン形式:

```csharp
Dispatcher.Use((ctx, next) =>
{
    if (_isReadOnly && IsDestructive(ctx.Action))
        return;                  // 短絡: ハンドラを呼ばずに終わる
    next(ctx);
});
```

### `DispatchContext` の主な API

| プロパティ | 型 | 備考 |
|----------|----|----|
| `Action` | `ViewAction` | ディスパッチ中のアクション |
| `Payload` | `object` | 読み取り専用 (型変換禁止) |
| `ExpectedPayloadType` | `Type` | パラメータなしハンドラの場合は `null` |
| `HandlerExecuted` | `bool` | 末端ハンドラが呼ばれたとき `true` (非同期ハンドラは開始した時点) |
| `Exception` | `Exception` | ハンドラが投げた例外 (ハンドラを抜けた時点で設定される記録) |
| `Completion` | `Task` | 非同期ハンドラのタスク (同期ハンドラでは完了済み)。非同期処理全体の計測・監査用 |
| `CancellationToken` | `CancellationToken` | 非同期ハンドラに渡されたトークン (Presenter 破棄時にキャンセル) |

`Items` 辞書のような汎用拡張はありません。状態を保持したいミドルウェアは、自分のフィールドに持たせてください。

### 例外フロー

ハンドラの例外はミドルウェアチェーンを上方向に伝播します。捕捉したい場合は `next(context)` を `try`/`catch` で囲みます (例: `ErrorDialogMiddleware`)。**ミドルウェアが `catch` した例外は処理済み** とみなされ、それ以上は伝わりません (`context.Exception` は記録として残ります)。誰も捕捉しない場合はフレームワークがログを出力し、`Dispatch` の呼び出し元へ再スローします (`Application.ThreadException` へ)。ミドルウェアが別の例外を投げた場合は、その例外が伝わります。`ActionExecuted` イベントは「ハンドラが完走 **かつ** `context.Exception` が `null`」のときだけ発火します。

ハンドラが投げた例外は、**ハンドラを抜けた時点で** `context.Exception` に設定されます。そのため外側のミドルウェアは、例外を捕捉しなくても `finally` ブロックで `context.Exception` を参照できます (監査・計測向け)。また、例外を投げたハンドラの後には `CanExecuteChanged` が発火し、コントロールの有効/無効状態が更新されます。

### ミドルウェアが見えないケース

パイプラインは Dispatcher の前提チェック **後** に走ります。以下のディスパッチではミドルウェアは呼ばれません:

- 登録されていない ActionKey
- `CanExecute` が `false` の場合
- ペイロードの型不一致

「無効化中でも操作試行を監査したい」場合は、自分のコードで `CanDispatch` を呼んでから `Dispatch` を呼ぶ等の工夫が必要です。

### パフォーマンス特性

- **ファストパス** (ミドルウェア未登録): アロケーションなし、追加のインダイレクションなし
- **スローパス** (ミドルウェア登録あり): ディスパッチごとに `DispatchContext` 1 個ヒープ確保 + N 個のクロージャ呼び出し (N = ミドルウェア数)。コンパイル済みパイプラインは Dispatcher にキャッシュされ、ミドルウェア追加時のみ再構築される
- リフレクション・Expression Trees は使用していないので、どのターゲットでも追加コストはありません

---

## 関連ページ

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Dispatcher` プロパティ、`RegisterViewActions` フック
- [Platform Services](Reference-Platform-Services) — Dispatcher を含むサービスの構成
- [MVP 設計ルール](Design-Rules) — ルール 4 (UI 型を View インターフェイスに露出しない) 、ルール 7 (Tell-Don't-Ask)
- サンプルコード:
  - `samples/WinformsMVP.Samples/ViewActionExample.cs` — Implicit パターンの最小例
  - `samples/WinformsMVP.Samples/ViewActionExplicitEventExample.cs` — Explicit パターン
  - `samples/WinformsMVP.Samples/ViewActionWithParametersExample.cs` — パラメータ付きアクション
  - `samples/WinformsMVP.Samples/ViewActionStateChangedExample.cs` — 状態駆動更新
  - `samples/WinformsMVP.Samples/CheckBoxDemo/` — CheckBox/RadioButton バインド
  - `samples/WinformsMVP.Samples/BulkBindingDemo/` — 一括バインド
  - `samples/WinformsMVP.Samples/ViewActionMiddlewareExample.cs` — ミドルウェア (Audit / Performance / ErrorDialog)
