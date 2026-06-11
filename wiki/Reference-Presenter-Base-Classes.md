# Presenter 基底クラス

このページでは、本フレームワークが提供する **Presenter の基底クラス 4 種類** とそれらのライフサイクル・公開 API・選び方を一通り解説します。
MVP の思想自体は [MVP パターンとは](Concept-MVP-Pattern) を、結果を返す Presenter の作り方は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

---

## クラス階層

```
PresenterBase<TView>                              [共通基底・直接継承しない]
│
├─ WindowPresenterBaseCore<TView>                 [クローズ機構の共有基底・直接継承しない]
│   ├─ WindowPresenterBase<TView>                 [Form、パラメータなし]
│   └─ WindowPresenterBase<TView, TParam>         [Form、パラメータあり]
├─ ControlPresenterBase<TView>                    [UserControl、パラメータなし]
└─ ControlPresenterBase<TView, TParam>            [UserControl、パラメータあり]
```

`WindowPresenterBaseCore<TView>` には Pull ゲート (`CanClose` オーバーライド)、Push helper (`RequestClose`)、および `Connect` で使われるクローズシンクバインディングが集約されています。直接継承せず、`WindowPresenterBase<TView>` または `WindowPresenterBase<TView, TParam>` を使ってください。

業務結果を返す Presenter は、`RequestClose(result, status)` 基底メソッドを呼ぶだけです — インターフェイスの実装も拡張メソッドも不要。`TResult` は引数から推論されます (詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model))。

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
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => View.IsValid);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
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
    // コンストラクタは Presenter 自身の依存だけを受け取る (この例では無し)。
    // View は Connect で渡す — Window 系と同じ二段構築。

    protected override void OnInitialize()
    {
        // Connect は構築後に走るため、View も注入済みの依存もここで安全に使える。
        LoadSettings();
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

        // UserControl は既にデザイナーで配置済み。
        _settingsPresenter = new SettingsPanelPresenter();
        _settingsPresenter.Connect(_settingsPanel);   // アタッチ + 初期化 + 破棄配線
        // _settingsPanel.Dispose() と一緒に Presenter も自動 Dispose
    }
}
```

**Form 系との違い**:

- View は既にデザイナーまたはコードで生成済み → `Connect(view)` で渡す (コンストラクタは Presenter 自身の依存のみ)
- `WindowNavigator` も `IViewMappingRegister` も使わない
- `Connect` は構築後に走るため、`OnViewAttached` / `OnInitialize` で注入済みの依存を安全に使える (コンストラクタ順序の罠が無い)
- UserControl が Dispose されると Presenter も自動 Dispose (`Connect` がライフサイクルを橋渡し)
- Presenter 基底は `System.Windows.Forms` に依存しない — 唯一の `view is Control` 境界は内部の `ControlLifecycleController` に隔離 (Window 側の `WindowLifecycleController` と対称)

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
    // コンストラクタは Presenter 自身の依存のみ。View とパラメータは Connect で渡す。

    protected override void OnInitialize(SearchParameters parameters)
    {
        View.Keyword = parameters.DefaultKeyword;
        View.DateFrom = parameters.DateFrom;
    }
}

// 使用
var parameters = new SearchParameters { DefaultKeyword = "test" };
var presenter = new SearchPanelPresenter();
presenter.Connect(_searchPanel, parameters);   // アタッチ + パラメータ初期化
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
2. Presenter インスタンスを生成 (`new SettingsPresenter(deps?)` — Presenter 自身の依存のみ)
3. `presenter.Connect(view[, parameters])` を呼ぶ → 構築後に **View アタッチ** (`OnViewAttached`) → **アクション登録** (`RegisterViewActions`、続けてフレームワークが `ActionBinder` を自動 Bind) → **初期化** (`OnInitialize`) が走る
4. UserControl が `Dispose` されると Presenter も自動 `Dispose` (`Connect` が `ControlLifecycleController` 経由で橋渡し)

---

## 主なフック (Override 可能なメソッド)

| メソッド | 呼ばれるタイミング | 主な用途 |
|---------|-----------------|---------|
| `OnViewAttached()` | View が注入された直後 | View イベントの購読 |
| `OnInitialize()` / `OnInitialize(TParam)` | View アタッチ後・表示前 | View プロパティの初期化、データロード |
| `RegisterViewActions()` | `OnInitialize` の前 (Initialize 内) | `Dispatcher.Register` でアクション登録 (この直後にフレームワークが ActionBinder を自動 Bind) |
| `Cleanup()` | Presenter が破棄されるとき | イベント購読解除、リソース解放 |

すべて `protected override`。`Cleanup` 内で View イベントを `-=` で解除する習慣にすると、メモリリークを防げます。

---

## 公開 API は最小限に保つ

> **Presenter は "サービス" ではありません** — 外部から命令的に突けるサービスにしない、という 1 つの不変量で考えます。

ここが [MVP 設計ルール](Design-Rules) の **Rule 16・17 の正準の落とし所**です (Design-Rules 側はルール文と本ページへの参照のみ)。ルールは「公開メンバーの白名単」ではなく、*Presenter のユースケース行動は正当な通路 — ViewAction (視点の意図) + ライフサイクル (構築 / `Initialize` / `Dispose`) — からのみ駆動され、任意の外部メソッド呼び出しからは駆動されない* という不変量です。これを **入站 (他者が呼んで Presenter を動かすメソッド) と 出站 (Presenter が出すイベント/通知) で非対称に**適用します:

- **入站 = 命令面 → 厳しく圧縮する。** 硬い約束: ViewAction の handler / helper は必ず `private`。`public Save()` を生やすと Dispatcher の `CanExecute`・ミドルウェアを迂回する経路ができ、Presenter がサービス化します。これが非協商部分です。
- **出站 = 通知面 → 圧縮対象ではない。** 上向きの出力ポート / 通知イベントは公開してよい (後述)。公開イベントは Presenter を命令的に突けるようにはしないからです。

### メソッドの可視性ガイドライン

| メソッドの種類 | 可視性 |
|--------------|------|
| コンストラクタ / `Initialize` / `Dispose` | `public` (既定の公開面) |
| 親→子の狭いコマンド | 既定 `internal` (同一アセンブリ)。縫い目が要るときだけ公開コマンドインターフェイス — 接口は強制しない |
| ライフサイクルフック (`OnInitialize`、`OnViewAttached`、`RegisterViewActions`、`Cleanup`) | `protected override` |
| クローズゲート (`CanClose(CloseReason)` / `CanClose(CloseReason, Action<bool>)`) | `protected override` |
| ViewAction ハンドラ (`OnSave`、`OnCancel`、...) | `private` (硬い約束) |
| View イベントハンドラ (`OnSelectionChanged`、...) | `private` |
| ヘルパー (検証、フォーマッタ等) | `private` |

### 出站イベント: 公開してよいもの / 禁じるもの

圧縮するのは**入站の命令面**であって、出站の通知ではありません。**公開してはいけない**のは、内部状態を載せたイベント (`IsDirtyChanged`) や View イベントを再発行するイベント (`SelectionChanged`) — それらは本来 Model / Service / Store が持つべき観測点を Presenter に漏らします。下の NG / OK 例がちょうどこの境界を描きます。

通知の性質ごとに手段を選びます:

- **ウィンドウの結果通知 (上向き・単発)** → 基底 `RequestClose(result, status)` を呼ぶ (✅ 出力ポート。インターフェイス実装不要。フレームワークが `InteractionResult<TResult>` に変換して呼び出し元に返す)
- **親子の連携 (親 → 子)** → **まず共有 Model / イベントを検討する**。直接命令は**窄い例外**で、本物の一回的コマンド (`Reset()` 等、他に関係者がなく子の状態問い合わせも不要) のときだけ使います。その場合も **handler は `private` のまま (硬線)**、露出形式は既定 `internal`、差し替え / テストの縫い目が要るときだけコマンドインターフェイス (**接口は強制しない**)。[Rule 10](Design-Rules#rule-10-view-にアクセスするのは-presenter-だけ) ・ [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) 参照
- **共有・観測されるステート (注文・認証等)** → ステートは Service / Store に持たせ、その変更通知イベントを発火
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
// ✅ 公開面はコンストラクタだけ。結果はフレームワークが InteractionResult<T> に変換して返す。
public class GoodPresenter : WindowPresenterBase<IMyView>
{
    public GoodPresenter(IMyRepository repo) { ... }

    protected override void RegisterViewActions() { ... }
    private void OnSave()
    {
        var result = BuildResult();
        RequestClose(result, InteractionStatus.Ok);   // Push close — TResult inferred from result
    }
}
```

---

## テスト時のルール

> **テストのために可視性を緩めない**。

Presenter を **本物のエントリーポイントから** 駆動してください。これによってテストが production と同じ経路を通ります。

```csharp
// ❌ Wrong:   presenter.OnSave();
// ✅ Right:   presenter.Dispatcher.Dispatch(StandardActions.Save);

// Pull ゲート (CanClose) のテスト — ICloseParticipant は internal、InternalsVisibleTo で到達
bool? allow = null;
((ICloseParticipant)presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);
Assert.False(allow);

// Push (RequestClose) のテスト — recording sink を注入
var sink = new RecordingSink();   // ICloseSink を実装する小さなテスト用クラス
((ICloseParticipant)presenter).BindCloseSink(sink);
presenter.Dispatcher.Dispatch(StandardActions.Save);
Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
```

`OnSave` を `private` から `internal` / `public` に格上げしてテストすると、production が通らない経路がテストで通ってしまい、CanExecute 判定をスキップする等の不具合を見逃します。

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) と [HowTo: ウィンドウクローズを扱う § テストパターン](HowTo-Handle-Window-Closing#test-patterns) を参照してください。

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
これらは `ServiceLocator.Current` (`IServiceProvider`) から遅延解決されます。
DI コンテナと統合する場合の構成方法は [Dependency Injection](Reference-DependencyInjection) を参照してください。

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — Presenter の責務範囲
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — `RequestClose` / `CanClose` の詳細
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Register` の全形式
- [WindowNavigator](Reference-WindowNavigator) — Form 系 Presenter の表示方法
- [Platform Services](Reference-Platform-Services) — 内部ヘルパーで使えるサービス群
- [MVP 設計ルール](Design-Rules) — ルール 16 (メソッド可視性) ・ルール 17 (イベント最小化) の詳細
