# ViewAction システム

UI イベント (ボタンクリック等) を **宣言的なアクション** に変換し、Presenter のハンドラと結びつける仕組みです。
WPF の `ICommand` を WinForms 向けに持ち込んだものと考えるとほぼ正確で、`CanExecute` による自動 `Enabled` 制御まで含めて同等の体験を提供します。

> **位置づけ**: ViewAction は本フレームワークの **中核機能** です。Presenter が `Button` を一切知らずに済むのは、この仕組みのおかげです。

---

## 目次

- [構成要素](#構成要素)
- [基本的な使い方](#基本的な使い方)
- [ActionBinder プロパティパターン](#actionbinder-プロパティパターン)
- [ActionKey の定義方針](#actionkey-の定義方針)
- [CanExecute と自動 Enabled 制御](#canexecute-と自動-enabled-制御)
- [パラメータ付きアクション](#パラメータ付きアクション)
- [状態変化への対応](#状態変化への対応)
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

    ViewActionBinder ActionBinder { get; }
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

過去設計では `void BindActions(ViewActionDispatcher dispatcher)` というメソッドを View インターフェイスに置いていましたが、現在は **`ViewActionBinder ActionBinder { get; }` プロパティ** に統一されています。

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
| ハンドラが例外を投げた | catch してログ | catch してログ (**変化なし**) |
| `CanExecute` が `false` (無効状態) | 何もしない | 何もしない (**変化なし**) |

Strict が対象とするのは **設定ミスだけ** です。ハンドラ/`CanExecute` の例外は常に捕捉・ログされ (集中エラーハンドリングを維持)、無効化されたアクション (`CanExecute == false`) はエラー扱いしません。

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

private async void OnStartBackgroundTask()
{
    _isRunning = true;
    Dispatcher.RaiseCanExecuteChanged();   // タスク開始通知

    await DoWorkAsync();

    _isRunning = false;
    Dispatcher.RaiseCanExecuteChanged();   // タスク完了通知
}
```

### WPF との対応

| 機能 | WPF `ICommand` | このフレームワーク |
|------|---------------|------------------|
| イベント名 | `CanExecuteChanged` | `CanExecuteChanged` |
| 手動トリガー | `command.RaiseCanExecuteChanged()` | `dispatcher.RaiseCanExecuteChanged()` |
| 自動トリガー | なし | `ActionExecuted` イベントで自動 (拡張機能) |

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
| `ToolStripItem` | `Click` | `ToolStripButton`、`ToolStripMenuItem` 等 |
| `Control` (fallback) | `Click` | 上記に当てはまらない任意のコントロール |

**重要**: `CheckBox` と `RadioButton` は `Click` ではなく `CheckedChanged` に紐付くため、キーボードや programmatic な変更でも発火します。

### CanExecute と CheckBox/RadioButton

`CanExecute` が制御するのは **`Enabled` プロパティ** (グレーアウト) であって、`Checked` ではありません。

```csharp
// ログイン中のときだけチェックを変更可能にする
Dispatcher.Register(
    SettingsActions.ToggleAutoSave,
    OnToggleAutoSave,
    canExecute: () => _isUserLoggedIn);
```

### カスタムコントロールに対応する

独自コントロールに対するバインド戦略は、`RegisterStrategy<T>()` で登録できます。
詳細は実装コード (`src/WinformsMVP/MVP/ViewActions/`) を参照してください。

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

View が `ActionRequest` イベントを明示的に発火し、Presenter が明示的に購読する形です。
**ActionBinder プロパティは `null` を返す** ことで自動 Bind を無効化し、二重ディスパッチを防ぎます。

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
}

protected override void OnInitialize()
{
    View.SelectionChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    View.DataChanged      += (s, e) => Dispatcher.RaiseCanExecuteChanged();
}
```

| 観点 | Explicit パターン |
|------|---------------|
| イベント購読が見える | ✅ F12 ナビ可能 |
| ブレークポイント設定 | ✅ できる |
| 自動 CanExecute 更新 | ❌ 自分で `RaiseCanExecuteChanged()` を呼ぶ |
| コード量 | 多い |
| 使いどころ | 動作を完全に追跡したい・複雑なルーティング・学習目的 |

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
| `HandlerExecuted` | `bool` | 末端ハンドラが正常終了したときだけ `true` |
| `Exception` | `Exception` | 安全網またはミドルウェアが捕捉した例外 |

`Items` 辞書のような汎用拡張はありません。状態を保持したいミドルウェアは、自分のフィールドに持たせてください。

### 例外フロー

ハンドラの例外はミドルウェアチェーンを上方向に伝播します。捕捉したい場合は `next(context)` を `try`/`catch` で囲みます (例: `ErrorDialogMiddleware`)。誰も捕捉しない場合はフレームワークの安全網がログを出力し、`context.Exception` に記録します。`ActionExecuted` イベントは「ハンドラが完走 **かつ** `context.Exception` が `null`」のときだけ発火します。

### ミドルウェアが見えないケース

パイプラインは Dispatcher の前提チェック **後** に走ります。以下のディスパッチではミドルウェアは呼ばれません:

- 登録されていない ActionKey
- `CanExecute` が `false` の場合
- ペイロードの型不一致

「無効化中でも操作試行を監査したい」場合は、自分のコードで `CanDispatch` を呼んでから `Dispatch` を呼ぶ等の工夫が必要です。

### パフォーマンス特性

- **ファストパス** (ミドルウェア未登録): アロケーションなし、追加のインダイレクションなし
- **スローパス** (ミドルウェア登録あり): ディスパッチごとに `DispatchContext` 1 個ヒープ確保 + N 個のクロージャ呼び出し (N = ミドルウェア数)。コンパイル済みパイプラインは Dispatcher にキャッシュされ、ミドルウェア追加時のみ再構築される
- リフレクション・Expression Trees は使用していないので、`net40` ホストでも追加コストはありません

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
