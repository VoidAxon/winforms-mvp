# ViewAction システム

UI イベント (ボタンクリック等) を **宣言的なアクション** に変換し、Presenter のハンドラと結びつける仕組みです。
WPF の `ICommand` を WinForms 向けに持ち込んだものと考えるとほぼ正確で、`CanExecute` による自動 `Enabled` 制御まで含めて同等の体験を提供します。

> **位置づけ**: ViewAction は本フレームワークの **中核機能** です。Presenter が `Button` を一切知らずに済むのは、この仕組みのおかげです。
>
> ただし **必須ではありません**。ボタンが数個の単純な画面なら、View インターフェイスに `event EventHandler SaveRequested;` のような意味イベントを置き、Presenter が購読する素の MVP で十分です。ViewAction が効いてくるのは、操作が多くてイベントが増えすぎる画面や、`CanExecute` による Enabled 制御・非同期ハンドラの多重実行防止・操作ログ (オブザーバー) といったフレームワークの支援を使いたい画面です。同じアプリ内で画面ごとに使い分けて構いません。

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
- [操作ログ・計測 (IActionObserver)](#操作ログ計測-iactionobserver)
- [関連ページ](#関連ページ)

---

## 構成要素

| コンポーネント | 役割 |
|--------------|------|
| `ViewAction` | アクションの **不変な識別子** (struct)。画面の Actions クラスで `ViewAction.Factory.WithQualifier("Module")` の Factory から作る (単発なら `ViewAction.Create("Module.Save")`) |
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
    private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("UserEditor");

    public static readonly ViewAction Save   = Factory.Create(nameof(Save));     // "UserEditor.Save"
    public static readonly ViewAction Delete = Factory.Create(nameof(Delete));
    public static readonly ViewAction Cancel = Factory.Create(nameof(Cancel));
}
```

プレフィックス (画面名) は 1 回だけ書き、名前は `nameof` で付けます。文字列の重複がなく、1 つだけ画面名を書き間違えることもありません。

### Step 2: View インターフェイスを定義する

```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    bool HasSelectedUser { get; }
}
```

`ActionBinder` (`IViewActionBinder`) は `IWindowView` / `IControlView` が継承する `IActionBindable` のメンバーなので、View インターフェイスに書き直しません。ViewAction を使わない View は `ActionBinder => null` を返します。

### Step 3: Form 内部で `ViewActionBinder` を構成する

```csharp
public class UserEditorForm : Form, IUserEditorView
{
    private Button _btnSave;
    private Button _btnDelete;
    private ViewActionBinder _binder;

    public IViewActionBinder ActionBinder => _binder;

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

### アプリに基底 Form があるとき: Binder を基底クラスに置く

画面ごとに `new ViewActionBinder()` と `ActionBinder` の実装を書くのが煩わしければ、アプリの**既存の基底 Form** に 2 行足します。

```csharp
public class AppForm : Form, IActionBindable   // アプリが既に持っている基底 Form (PoisonForm 等の派生でもよい)
{
    // ... 既存のコード ...

    protected ViewActionBinder Binder { get; } = new ViewActionBinder();
    IViewActionBinder IActionBindable.ActionBinder => Binder;
}

public partial class UserEditorForm : AppForm, IUserEditorView
{
    public UserEditorForm()
    {
        InitializeComponent();
        Binder.Add(UserEditorActions.Save,   _btnSave);    // new も ActionBinder の実装も不要
        Binder.Add(UserEditorActions.Delete, _btnDelete);
    }
}
```

- `IUserEditorView` が `IWindowView` 経由で要求する `ActionBinder` には、基底クラスの明示的実装がそのまま使われます。
- `Binder` は `protected` なので View インターフェイスには現れず、WFO1000 (公開プロパティの `DesignerSerializationVisibility`) も出ません。
- Explicit パターン (後述) の画面は、基底クラスを使わずに `ActionBinder => null` を自分で書きます。

フレームワークは基底 Form を提供しません。C# は単一継承なので、PoisonForm・XtraForm などのサードパーティ Form や、レガシーアプリが既に持っている基底 Form と両立できなくなるためです。この 2 行はアプリ側の基底クラスに置いてください。

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

### Presenter の破棄とバインド解除 (`Unbind`)

`IViewActionBinder` は `Bind(dispatcher)` と対になる `Unbind()` を持ちます。

```csharp
public interface IViewActionBinder
{
    void Bind(ViewActionDispatcher dispatcher);
    void Unbind();   // フレームワークが Presenter の破棄時に呼ぶ
}
```

- **フレームワークが呼ぶ**: Presenter が破棄されると、その Presenter が Bind した `ActionBinder` の `Unbind()` が呼ばれます (`Lifetime` のキャンセル直後、`Cleanup` の前)。View 側で呼ぶ必要はありません。
- **`ViewActionBinder.Unbind()`** はコントロールのイベントと Dispatcher の両方から切り離します。以後クリックしても何もディスパッチされず、`Enabled` も更新されません。コントロールと View はそのまま、構成済みのバインディングも残るので `Bind(dispatcher)` で再度 Bind できます。2 回目以降や Bind 前の呼び出しは何もしません。
- **独自の `IViewActionBinder` 実装も `Unbind()` を実装してください** (破壊的変更)。Dispatcher への参照と購読を外し、以後のトリガーを無害にするのが契約です。テスト用の `CapturingActionBinder` は Unbind 後の `Dispatch` を何もしない操作にし、`IsBound` が `false` になります ([HowTo: Presenter をテストする](HowTo-Test-A-Presenter))。

**破棄された Presenter の Dispatcher は何も実行しません。** `Unbind` より前、破棄の最初の段階で、Presenter (と [`AttachChild`](Reference-Presenter-Base-Classes#子-presenter-を所有する-attachchild) で所有する子孫すべて) の Dispatcher が停止します:

- `CanDispatch` は常に `false`
- `Dispatch` / `DispatchAsync` はハンドラを実行せず (オブザーバーにも通知せず)、未登録キーやペイロード不一致でも Strict モードの例外を出さない (`DispatchAsync` はそのまま完了したタスクを返す)
- `CanExecuteChanged` は発火しない — 破棄前から実行中だった非同期ハンドラが完了したときも、`RaiseCanExecuteChanged()` を呼んでも

そのため、Presenter より長生きするコントロール (取り除いた明細行パネル、閉じ処理中のフォーム) を後からクリックしても、破棄済みの Presenter のハンドラは走りません。

---

## ActionKey の定義方針

### 静的クラスにまとめる

文字列リテラルを直に渡さず、必ず静的クラスで定数として宣言します。標準的な動詞 (Save / Cancel / Delete / Refresh / Ok / ...) は**フレームワーク同梱の `StandardActions`** をそのまま使えます — 再定義しないでください。自前の静的クラスで宣言するのは **モジュール固有のアクション** だけです。

`StandardActions` のキーも画面のアクションと同じく修飾されています: `StandardActions.Save` のキーは `"Standard.Save"` (`"Standard."` + メンバー名)。そのため同梱のキー同士が重なることはなく (`FileSave` と `Save` は別のアクション)、ログや操作ログでもどこのキーか分かります。Presenter ごとに登録するので、2 つの画面が両方 `StandardActions.Save` を使っても衝突しません。

```csharp
using Kibinago.ViewActions;

// 標準動詞は出荷済みの StandardActions を使う (再定義しない)
Dispatcher.Register(StandardActions.Save, OnSave);

// モジュール固有のアクションだけ静的クラスで宣言する
public static class UserEditorActions
{
    private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("UserEditor");

    public static readonly ViewAction EditUser       = Factory.Create(nameof(EditUser));
    public static readonly ViewAction AddUser        = Factory.Create(nameof(AddUser));
    public static readonly ViewAction ChangePassword = Factory.Create(nameof(ChangePassword));
}
```

**命名規約**: `Module.Action` 形式 (ドット区切り)。これにより文字列が衝突せず、ログでも判別しやすくなります。

### `ViewActionFactory` と `ViewAction.Create` の使い分け

**画面の Actions クラスは Factory で書く (推奨)。** プレフィックスを 1 回だけ書き、各キーは `nameof` で名前を付けます。テンプレート (`dotnet new mvp-window` 等) もこの形です。

```csharp
public static class MainActions
{
    private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Main");

    public static readonly ViewAction New  = Factory.Create(nameof(New));
    public static readonly ViewAction Open = Factory.Create(nameof(Open));
    public static readonly ViewAction<string> OpenRecent = Factory.Create<string>(nameof(OpenRecent));   // 型付き
}
// 生成される ActionKey: "Main.New", "Main.Open", "Main.OpenRecent"
```

`WithQualifier` は階層も作れます (`ViewAction.Factory.WithQualifier("Sales", "OrderEdit")` → `"Sales.OrderEdit.Save"`)。

**`ViewAction.Create("Scope.Name")` は単発のキーに使う。** 複数画面で共有する 1 つのキーなど、Factory を置くほどでもない場合です。

静的フィールドは記述順に初期化されるので、`Factory` はキーより**前に**宣言してください。

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
| **`OnInitialize` の後・`OnInitializeAsync` の完了時** | 自動 (バインドは `OnInitialize` の前なので、そこで読み込んだデータで改めて再評価) |
| **任意のアクションのハンドラが終わった直後** (失敗・キャンセルも) | 自動 (Dispatcher が `CanExecuteChanged` を発生し、全 CanExecute を再評価) |
| **非同期ハンドラの開始時・終了時** | 自動 (実行中のアクションは `CanDispatch == false`) |
| **アクション実行を伴わない状態変化** | 手動 (`Dispatcher.RaiseCanExecuteChanged()` を呼ぶ) |
| **Presenter の破棄後** | 再評価しない (Dispatcher は停止済み — [Unbind](#presenter-の破棄とバインド解除-unbind) 参照) |

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
    View.Content = _documents.Load(filePath);   // _documents: コンストラクタで注入したサービス
    // ...
}
```

**型不一致は実行時拒否**: `Register<string>` に対して `Dispatch(action, 42)` (int) を呼ぶと、既定 (Lenient) では Dispatcher はハンドラを呼ばずにログだけ出します。オブザーバーにも通知されません (後述の Strict モードでは例外になります)。

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

// テスト: CapturingActionBinder (Kibinago.Testing) も型付きで呼べる
view.Binder.Dispatch(DocumentActions.SetZoom, 150);
```

- **任意** です。従来の `ViewAction` + `Register<T>` はそのまま使え、同じ Dispatcher 内で混在できます。
- 型付きの API: `ViewAction.Create<T>` / `ViewActionFactory.Create<T>`、Dispatcher の `Register` (`Action<T>` / `Func<T, Task>` / `Func<T, CancellationToken, Task>`)・`Dispatch`・`DispatchAsync`・`CanDispatch`、Binder の `Add(key, control, valueSelector)`。
- `ViewAction<T>` から `ViewAction` への暗黙変換はありません (型付きキーが型なしのオーバーロードに黙って渡らないように)。型なしの API が必要な場面 (オブザーバーでのキー比較、`Add(key, controls)` のような値なしバインドなど) では `.Key` を使います。`Name` は `.Key.Name` と同じです。
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

**推奨される使い方**: Debug ビルドでのみ Strict を有効化します。起動時の設定で指定すると、すべての Presenter の Dispatcher に (View のアタッチ時、全 Dispatch より前に) 適用されます:

```csharp
MvpApplication.Configure(framework: mvp =>
{
#if DEBUG
    mvp.Actions.StrictValidation();   // ActionDispatcherOptions を登録 → ValidationMode = Strict
#endif
});
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
    // ハンドラが終わると Dispatcher が CanExecuteChanged を発生し、
    // 全ての CanExecute が再評価される。手動コードは不要。
}
```

### Pattern 2: 状態駆動 (手動トリガー)

ユーザー入力等、アクション実行を伴わない状態変化では、明示的に再評価をトリガーします。

```csharp
protected override void OnViewAttached()
{
    // 選択状態が変わったら CanExecute を再評価
    SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h,
        Dispatcher.RaiseCanExecuteChanged);
}
```

非同期ハンドラの開始・完了時の再評価はフレームワークが自動で行います ([非同期アクション](#非同期アクション) 参照)。

### WPF との対応

| 機能 | WPF `ICommand` | このフレームワーク |
|------|---------------|------------------|
| イベント名 | `CanExecuteChanged` | `CanExecuteChanged` |
| 手動トリガー | `command.RaiseCanExecuteChanged()` | `dispatcher.RaiseCanExecuteChanged()` |
| 自動トリガー | なし | ハンドラの終了時に自動 (拡張機能) |

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

- **実行中はアクションを無効化** — ハンドラの実行中は `CanDispatch` が `false` になり、バインド済みコントロールはグレーアウトします。連打などの重複トリガーは無視されます。開始時と終了時に全 `CanExecute` が再評価されるので、ビジーフラグや手動の `RaiseCanExecuteChanged()` は不要です (他のアクションも同時に止めたいときだけ、`canExecute` が参照するフラグを持ちます — [`samples/Kibinago.Samples/AsyncDemo/AsyncDemoPresenter.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/AsyncDemo/AsyncDemoPresenter.cs) 参照)。
- **完了時にオブザーバーへ通知** — [`IActionObserver`](#操作ログ計測-iactionobserver) を登録していれば、同期ハンドラと同じく、ハンドラが終わった時点 (`await` の後まで) で 1 回呼ばれます (`IsAsync == true`、`Elapsed` は非同期処理の全体)。
- **失敗はログ出力・再有効化のあと再スロー** — 未処理の例外はログに出してアクションを再び有効にし、そのあと通常の WinForms のイベントハンドラと同じように外へ伝わります (`Application.ThreadException` で受けられます)。同期部分の例外は `Dispatch` からそのまま、`await` の後で起きた例外は `Dispatch` を呼んだときの `SynchronizationContext` に投げ直されます (`async void` と同じ)。想定内のエラーはハンドラ内で `try`/`catch` して `Messages` で伝え、想定外のものは `Program.cs` の `Application.ThreadException` で受けます。
- **破棄時のキャンセルは静か** — トークンは Presenter の `Lifetime` で、Presenter の破棄 (ウィンドウを閉じる) と同時にキャンセルされます。これによる `OperationCanceledException` はエラー扱いされません。ユーザーの「キャンセル」ボタンを実装する場合は `CancellationTokenSource.CreateLinkedTokenSource(ct)` でリンクします。

### 完了を待つ: `DispatchAsync`

`Dispatch` は非同期ハンドラの完了を待たずに戻ります。完了を待ちたい場合 (主にテスト、またはアクションの後に処理を続けたいコード) は `DispatchAsync` を使います。ハンドラが失敗すると、例外を投げ直す代わりに **返るタスクがその例外で fault** します (テストでは `await Assert.ThrowsAsync<...>(() => view.Binder.DispatchAsync(...))`)。非同期ハンドラのテストは `Dispatch` ではなく `DispatchAsync` で書いてください — `Dispatch` の場合、`await` 後の例外はコンテキストに投げ直されるだけで、テストの失敗にならないことがあります。

```csharp
await Dispatcher.DispatchAsync(CustomerActions.Refresh);
// テストではモック View の CapturingActionBinder (Kibinago.Testing) から:
await view.Binder.DispatchAsync(CustomerActions.Refresh);
```

---

## 操作ログ・計測 (IActionObserver)

アプリ全体の操作ログ (誰が・いつ・どの画面で・何をしたか) や処理時間の計測は、起動時に **`IActionObserver`** を 1 つ登録して書きます。ハンドラも Presenter もログのことを知りません。

```csharp
public sealed class OperationLog : IActionObserver
{
    private readonly ILogger _logger;
    public OperationLog(ILoggerFactory loggers) => _logger = loggers.CreateLogger("Operations");

    public void OnExecuted(ActionExecution e)
    {
        _logger.LogInformation("{Presenter} {Action} {Payload} {Outcome} {Ms}ms",
            e.PresenterType?.Name, e.Action, e.Payload, e.Outcome, (int)e.Elapsed.TotalMilliseconds);
        if (e.Elapsed > TimeSpan.FromSeconds(2))
            _logger.LogWarning("Slow action {Action}: {Ms}ms", e.Action, (int)e.Elapsed.TotalMilliseconds);
    }
}

MvpApplication.Configure(framework: mvp =>
    mvp.Actions.Observe(sp => new OperationLog(sp.Resolve<ILoggerFactory>())));
// M.E.DI: services.AddKibinago(viewRegistry, mvp => mvp.Actions.Observe(...));
//         または services.AddSingleton<IActionObserver, OperationLog>();
```

`OnExecuted` は **ハンドラが実行されるたびに、終わったときに 1 回** 呼ばれます。開始時には呼ばれません (開始時刻は `StartedAt`)。

| `ActionExecution` | 内容 |
|---|---|
| `Action` | 実行したアクションのキー (型付きキーも `ViewAction` で) |
| `Payload` | ハンドラに渡したペイロード (なしなら `null`) |
| `PresenterType` | アクションを登録した Presenter の型 (どの画面か) |
| `StartedAt` / `Elapsed` | 開始時刻 (UTC) とハンドラの所要時間 (非同期ハンドラは最後の `await` の後まで) |
| `IsAsync` | 非同期ハンドラか |
| `Outcome` | `Succeeded` (ハンドラが例外なく終わった) / `Failed` / `Canceled` |
| `Exception` | `Failed` のときだけ、ハンドラの例外 (`Application.ThreadException` に伝わるのと同じオブジェクト)。`Succeeded` / `Canceled` は `null` |

- **呼ばれる時点** — アクションが再び有効になり、`CanExecuteChanged` でコントロールの状態が更新された後。UI スレッド (ディスパッチしたスレッド) で呼ばれます。画面を閉じた後に終わった非同期ハンドラも通知されます (`Canceled` または、トークンを見ずに完走すれば `Succeeded`)。
- **呼ばれないディスパッチ** — 何も実行されなかったもの: `canExecute` が `false`、未登録のキー、ペイロードの型違い、非同期ハンドラの実行中の二度押し、破棄後のディスパッチ。
- **受け身** — オブザーバーは実行を止める・やり直す・例外を握りつぶすことができず、ハンドラの例外はそのまま伝わります。オブザーバー自身が投げた例外はログに出して無視します。重い処理 (ファイル・ネットワークへの書き込み) はキューに入れて別スレッドで。
- **1 アプリに 1 つ** — 後の `Observe` が前のものを置き換えます。複数に送りたいときは、1 つのオブザーバーの中で振り分けます。登録したインスタンスはフレームワークが破棄しません。
- **オーバーヘッド** — オブザーバーがなければ時刻も読みません。あれば 1 回の実行ごとに `Stopwatch` と `ActionExecution` を 1 つずつ。

### ほかの横断的処理

以前のミドルウェア (`Dispatcher.Use`) は削除しました。用途ごとに次の書き方をします。

| やりたいこと | 書き方 |
|---|---|
| 操作ログ・監査・計測 | `IActionObserver` (上記) |
| 想定外のエラーをまとめてダイアログで表示 | `Program.cs` の `Application.ThreadException` (ハンドラの例外はログの後そこへ伝わる) |
| 想定内のエラー (通信失敗など) | ハンドラの中で `try` / `catch` して `Messages` |
| 権限がないと実行させない | `canExecute: () => _user.CanEdit` (ボタンも無効になる) |
| 特定のハンドラだけのログ | そのハンドラの中で `Logger` |

テストでは `TestServices.CreateFakes()` が `RecordingActionObserver` を登録します (`fakes.Actions.Executions`)。

---

## 関連ページ

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Dispatcher` プロパティ、`RegisterViewActions` フック
- [Platform Services](Reference-Platform-Services) — Dispatcher を含むサービスの構成
- [MVP 設計ルール](Design-Rules) — ルール 4 (UI 型を View インターフェイスに露出しない) 、ルール 7 (Tell-Don't-Ask)
- サンプルコード:
  - [`samples/Kibinago.Samples/ViewActionExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/ViewActionExample.cs) — Implicit パターンの最小例
  - [`samples/Kibinago.Samples/ViewActionExplicitEventExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/ViewActionExplicitEventExample.cs) — Explicit パターン
  - [`samples/Kibinago.Samples/ViewActionWithParametersExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/ViewActionWithParametersExample.cs) — パラメータ付きアクション
  - [`samples/Kibinago.Samples/ViewActionStateChangedExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/ViewActionStateChangedExample.cs) — 状態駆動更新
  - [`samples/Kibinago.Samples/CheckBoxDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/CheckBoxDemo) — CheckBox/RadioButton バインド
  - [`samples/Kibinago.Samples/BulkBindingDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/BulkBindingDemo) — 一括バインド
  - [`samples/Kibinago.Samples/ActionLogDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/ActionLogDemo) — 操作ログ (`IActionObserver`)
