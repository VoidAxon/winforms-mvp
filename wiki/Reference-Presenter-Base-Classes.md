# Presenter 基底クラス

このページでは、本フレームワークが提供する **Presenter の基底クラス 4 種類** とそれらのライフサイクル・公開 API・選び方を一通り解説します。
MVP の思想自体は [MVP パターンとは](Concept-MVP-Pattern) を、結果を返す Presenter の作り方は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

---

## クラス階層

```
PresenterBase<TView>                              [共通基底・直接継承しない]
│
├─ WindowPresenterBase<TView>                     [Form、パラメータなし]
├─ WindowPresenterBase<TView, TParam>             [Form、パラメータあり]
├─ ControlPresenterBase<TView>                    [UserControl、パラメータなし]
└─ ControlPresenterBase<TView, TParam>            [UserControl、パラメータあり]
```

業務結果を返す Presenter は、上記いずれかに加えて `IRequestClose<TResult>` インターフェイスを実装します (詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model))。

---

## どれを選ぶか

| シナリオ | 使う基底クラス |
|---------|--------------|
| 通常の Form (ダイアログ・メインウィンドウ) | `WindowPresenterBase<TView>` |
| 入力データを受け取って開く Form | `WindowPresenterBase<TView, TParam>` |
| 親 Form / 親 UserControl 内に配置する UserControl | `ControlPresenterBase<TView>` |
| 構成情報を持つ UserControl | `ControlPresenterBase<TView, TParam>` |

ポイント:

- **Form vs UserControl の選び分けは、View が `Form` か `UserControl` かで決まる**。Form 系は `WindowNavigator` が動的に生成・表示するため、Presenter 側に View インスタンスを持ち込まない。UserControl 系は既存のコントロールに対して Presenter を後付けする。
- **パラメータの有無は「Presenter を生成するときに、業務的な前提情報が必要か」で決まる**。例: 「ユーザー ID `123` を編集するエディタ」のように、起動前に決まる値があるなら `TParam` 版。

---

## `WindowPresenterBase<TView>` — Form (パラメータなし)

WindowNavigator 経由で表示される Form を扱う、最も基本的な Presenter です。

```csharp
public class AboutDialogPresenter : WindowPresenterBase<IAboutDialogView>
{
    protected override void OnViewAttached()
    {
        // View が注入された直後 (Initialize 前) に呼ばれる。
        // View イベントの購読等を行う。
    }

    protected override void OnInitialize()
    {
        // View アタッチ後、ウィンドウ表示前に 1 回呼ばれる。
        // View プロパティの初期化等を行う。
        View.Version = GetVersion();
    }

    protected override void RegisterViewActions()
    {
        // Dispatcher に ViewAction ハンドラを登録する。
        // フレームワークが View.ActionBinder?.Bind(Dispatcher) を自動で呼ぶ。
        Dispatcher.Register(AboutActions.Close, OnClose);
    }

    private void OnClose() => /* ... */;
}

// 呼び出し側
var presenter = new AboutDialogPresenter();
navigator.ShowWindowAsModal(presenter);
```

---

## `WindowPresenterBase<TView, TParam>` — Form (パラメータあり)

Presenter の生成時にパラメータが必要な場合 (例: 編集対象のエンティティ ID) に使います。

```csharp
public class EditUserParameters
{
    public int UserId { get; set; }
    public bool IsReadOnly { get; set; }
}

public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>
{
    protected override void OnInitialize(EditUserParameters parameters)
    {
        // パラメータあり版では、こちらの override シグネチャを使う。
        // View と Parameters の両方が利用可能。
        var user = LoadUser(parameters.UserId);
        View.UserName = user.Name;
        View.Email = user.Email;
        View.IsReadOnly = parameters.IsReadOnly;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Save, OnSave,
            canExecute: () => View.IsValid);
        Dispatcher.Register(CommonActions.Cancel, OnCancel);
    }

    private void OnSave() => /* ... */;
    private void OnCancel() => /* ... */;
}

// 呼び出し側 — Fluent API でパラメータを渡す
var presenter = new EditUserPresenter();
var parameters = new EditUserParameters { UserId = 123, IsReadOnly = false };
navigator.For(presenter).WithParam(parameters).ShowAsModal();
```

**重要**: パラメータは「業務的な前提情報」だけに使い、DI 管理のサービス (Repository 等) は **コンストラクタ注入** してください。両者を混ぜると DI と Navigator の責務が混乱します。詳細は [Dependency Injection](Reference-DependencyInjection) を参照。

---

## `ControlPresenterBase<TView>` — UserControl (パラメータなし)

Form の中に埋め込む UserControl を制御する Presenter です。

```csharp
public class SettingsPanelPresenter : ControlPresenterBase<ISettingsPanelView>
{
    public SettingsPanelPresenter(ISettingsPanelView view) : base(view)
    {
        // base コンストラクタが自動的に AttachView + Initialize を呼ぶ。
    }

    protected override void OnInitialize()
    {
        LoadSettings();
    }

    protected override void OnControlLoad(object sender, EventArgs e)
    {
        // UserControl が Load されたとき (親 Form がアタッチされ Handle 生成済み) に呼ばれる。
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(SettingsActions.Save, OnSave);
    }

    private void OnSave() => /* ... */;
}

// 親 Form 内での使用
public partial class MainForm : Form
{
    private readonly SettingsPanelPresenter _settingsPresenter;

    public MainForm()
    {
        InitializeComponent();

        // UserControl は既にデザイナーで配置済み
        _settingsPresenter = new SettingsPanelPresenter(_settingsPanel);
        // _settingsPanel.Dispose() と一緒に Presenter も自動 Dispose
    }
}
```

**Form 系との違い**:

- View は既にデザイナーまたはコードで生成済み → コンストラクタで受け取る
- `WindowNavigator` も `IViewMappingRegister` も使わない
- UserControl のライフサイクル (`Load` / `Disposed` 等) を Presenter が自動購読
- UserControl が Dispose されると Presenter も自動 Dispose

---

## `ControlPresenterBase<TView, TParam>` — UserControl (パラメータあり)

UserControl の Presenter にパラメータを渡したいときに使います。

```csharp
public class SearchParameters
{
    public string DefaultKeyword { get; set; }
    public DateTime? DateFrom { get; set; }
}

public class SearchPanelPresenter : ControlPresenterBase<ISearchPanelView, SearchParameters>
{
    public SearchPanelPresenter(ISearchPanelView view, SearchParameters parameters)
        : base(view, parameters)
    {
    }

    protected override void OnInitialize(SearchParameters parameters)
    {
        View.Keyword = parameters.DefaultKeyword;
        View.DateFrom = parameters.DateFrom;
    }
}

// 使用
var parameters = new SearchParameters { DefaultKeyword = "test" };
var presenter = new SearchPanelPresenter(_searchPanel, parameters);
```

---

## ライフサイクル

### Form 系 (WindowNavigator パターン)

1. Presenter インスタンスを生成
2. `WindowNavigator` が `IViewMappingRegister` から View 型を解決して View を生成
3. View を Presenter にアタッチ (`IViewAttacher<TView>.AttachView()`)
4. **`OnViewAttached()` 呼び出し**
5. **`OnInitialize()` または `OnInitialize(parameters)` 呼び出し**
6. **`RegisterViewActions()` 呼び出し** (フレームワークが続けて `View.ActionBinder?.Bind(Dispatcher)` を実行)
7. Form 表示 (Modal / Non-Modal)
8. (Form が閉じられたとき) **`Cleanup()` 呼び出し**

### UserControl 系

1. UserControl は既にデザイナーまたはコードで生成済み
2. Presenter インスタンスを生成 (`new SettingsPresenter(view, parameters?)`)
3. base コンストラクタ内で **View アタッチ** + **初期化** が走る (`OnViewAttached` → `OnInitialize` → `RegisterViewActions`)
4. UserControl の `Load` イベントで `OnControlLoad()` 呼び出し
5. UserControl が `Dispose` されると Presenter も自動 `Dispose`

---

## 主なフック (Override 可能なメソッド)

| メソッド | 呼ばれるタイミング | 主な用途 |
|---------|-----------------|---------|
| `OnViewAttached()` | View が注入された直後 | View イベントの購読 |
| `OnInitialize()` / `OnInitialize(TParam)` | View アタッチ後・表示前 | View プロパティの初期化、データロード |
| `RegisterViewActions()` | `OnInitialize` の後 | `Dispatcher.Register` でアクション登録 (この直後にフレームワークが ActionBinder を自動 Bind) |
| `OnControlLoad(object, EventArgs)` (Control 系のみ) | UserControl が Load されたとき | 親 Form 依存の初期化 |
| `Cleanup()` | Presenter が破棄されるとき | イベント購読解除、リソース解放 |

すべて `protected override`。`Cleanup` 内で View イベントを `-=` で解除する習慣にすると、メモリリークを防げます。

---

## 公開 API は最小限に保つ

> **Presenter は "サービス" ではありません**。

ルール 16 と 17 ([MVP 設計ルール](Design-Rules)) で詳述しますが、公開メンバーが増えるほど契約が広がり、呼び出し側がフレームワークの「View → Action → Dispatcher → Handler」経路を迂回する誘惑が生まれます。

### メソッドの可視性ガイドライン

| メソッドの種類 | 可視性 |
|--------------|------|
| コンストラクタ | `public` (DI が要求) |
| インターフェイス契約 (例: `IRequestClose<T>.CloseRequested`) | `public` (契約が要求) |
| ライフサイクルフック (`OnInitialize`、`OnViewAttached`、`RegisterViewActions`、`Cleanup`) | `protected override` |
| ViewAction ハンドラ (`OnSave`、`OnCancel`、...) | `private` |
| View イベントハンドラ (`OnViewClosing`、...) | `private` |
| ヘルパー (`RaiseClose`、検証、フォーマッタ等) | `private` |

### 公開イベントは原則ゼロ

Presenter が公開するイベントは、`IRequestClose<TResult>` を実装する場合の `CloseRequested` を除いて **ゼロ** にします。他の通知ニーズには別の手段を使ってください:

- **ウィンドウの結果通知** → `IRequestClose<T>.CloseRequested` を実装
- **親子の連携** → 親が子のメソッドを呼ぶ (または最終手段として 1 つだけイベントを購読)
- **共有ステート (注文・認証等)** → ステートは Service に持たせ、Service からイベント発火
- **モジュール横断の通知** → `IEventAggregator` で publish (弱参照、UI スレッド整列)

### NG パターン

```csharp
// ❌ Presenter を Service / State Observer に変えてしまっている
public class BadPresenter : WindowPresenterBase<IMyView>
{
    public void Save() { ... }                       // ❌ Dispatcher / CanExecute を迂回
    public bool CanSave => _changeTracker.IsChanged; // ❌ Tell-Don't-Ask 違反
    public event EventHandler IsDirtyChanged;        // ❌ 内部状態を露出
    public event EventHandler SelectionChanged;      // ❌ View イベントの再発行
}
```

### OK パターン

```csharp
// ✅ コンストラクタ + 契約イベントだけ
public class GoodPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;

    public GoodPresenter(IMyRepository repo) { ... }

    protected override void RegisterViewActions() { ... }
    private void OnSave() { ... }
    private void RaiseClose(MyResult r, InteractionStatus s) => ...;
}
```

---

## テスト時のルール

> **テストのために可視性を緩めない**。

Presenter を **本物のエントリーポイントから** 駆動してください。これによってテストが production と同じ経路を通ります。

```csharp
// ❌ Wrong:   presenter.OnSave();
// ✅ Right:   presenter.Dispatcher.Dispatch(CommonActions.Save);
// ✅ Right:   view.RaiseClosing(CloseReason.Normal);
// ✅ Right:   presenter.CloseRequested += (s, e) => captured = e;
```

`OnSave` を `private` から `internal` / `public` に格上げしてテストすると、production が通らない経路がテストで通ってしまい、CanExecute 判定をスキップする等の不具合を見逃します。

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## 内部ヘルパー (基底クラスが提供)

すべての Presenter は、以下のプロパティ・サービスを継承で取得できます。

| メンバー | 型 | 用途 |
|---------|----|----|
| `View` | `TView` | アタッチされた View インターフェイス |
| `Dispatcher` | `ViewActionDispatcher` | ViewAction のディスパッチャ |
| `Messages` | `IMessageService` | メッセージダイアログ |
| `Dialogs` | `IDialogProvider` | OpenFile / SaveFile / FolderBrowser |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | 構造化ロギング |
| `Navigator` | `IWindowNavigator` | 子ウィンドウの表示 |
| `Platform` | `ICommonServices` | カスタムサービス取得用のコンテナ |

これらは `PlatformServices.Default` から自動取得されます。
DI コンテナと統合する場合の構成方法は [Dependency Injection](Reference-DependencyInjection) を参照してください。

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — Presenter の責務範囲
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — `IRequestClose<TResult>` の詳細
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Register` の全形式
- [WindowNavigator](Reference-WindowNavigator) — Form 系 Presenter の表示方法
- [Platform Services](Reference-Platform-Services) — 内部ヘルパーで使えるサービス群
- [MVP 設計ルール](Design-Rules) — ルール 16 (メソッド可視性) ・ルール 17 (イベント最小化) の詳細
