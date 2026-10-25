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

これらの基底クラスはすべて `WinformsMVP.Core` パッケージ (`netstandard2.0`、WinForms 非参照) にあります。`Connect` 拡張メソッドや `WindowNavigator` など WinForms に触れる部分は `WinformsMVP` パッケージ側です (名前空間は共通)。

`WindowPresenterBaseCore<TView>` にはクローズゲート (`CanClose` / `CanCloseAsync` オーバーライド)、Push helper (`RequestClose`)、および `Connect` で使われるクローズシンクバインディングが集約されています。直接継承せず、`WindowPresenterBase<TView>` または `WindowPresenterBase<TView, TParam>` を使ってください。

業務結果を返す Presenter は、`RequestClose(result, status)` 基底メソッドを呼ぶだけです — インターフェイスの実装も拡張メソッドも不要。`TResult` は引数から推論されます。開いているウィンドウへの `RequestClose` は X と同じく `CanClose` / `CanCloseAsync` を通り (`CloseReason.Normal`)、渡した結果はゲートが許可したときに呼び出し元へ届く候補です。ウィンドウ表示前 (`OnInitialize` など) の `RequestClose` はゲートを通らず、表示をキャンセルします (詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model))。

---

## どれを選ぶか

| シナリオ | 使う基底クラス |
|---------|--------------|
| 通常の Form (ダイアログ・メインウィンドウ) | `WindowPresenterBase<TView>` |
| 入力データを受け取って開く Form | `WindowPresenterBase<TView, TParam>` |
| 親 Form / 親 UserControl 内に配置する UserControl | `ControlPresenterBase<TView>` |
| 構成情報を持つ UserControl | `ControlPresenterBase<TView, TParam>` |
| 親 Presenter が組み立てる画面内のパネル (固定・動的) | `ControlPresenterBase<TView[, TParam]>` + 親の `AttachChild` ([後述](#子-presenter-を所有する-attachchild)) |

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

    private void OnClose() => RequestClose(InteractionStatus.Cancel);   // goes through CanClose, like X
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

    protected override void OnViewAttached() { }

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
- UserControl が Dispose されると Presenter も自動 Dispose (`Connect` がライフサイクルを橋渡し。破棄の監視は初期化の**前**に始まるので、初期化中にコントロールが破棄されても Presenter は破棄され、`Connect` は `ObjectDisposedException` を投げる)
- `Connect` は **WinForms 側がホストする** Presenter 用 (レガシー Form、依存を持たない単体の再利用コントロール)。MVP の親 Presenter が子パネルを持つ画面では、親 Presenter が [`AttachChild`](#子-presenter-を所有する-attachchild) で組み立てる
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

    protected override void OnViewAttached() { }

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

## 子 Presenter を所有する (`AttachChild`)

親 Presenter が画面内のパネル (UserControl) の Presenter を **自分で生成して所有する** ための `protected` メソッドです。子 Presenter の依存 (Repository 等) は親のコンストラクタから渡せるので、`WindowNavigator` が生成する Form にファクトリ登録は要りません。親 View インターフェイスは子の **View インターフェイス** を公開します (コントロールは公開しない — Rule 2 のまま)。

```csharp
protected IDisposable AttachChild<TChildView>(ControlPresenterBase<TChildView> child, TChildView view)
    where TChildView : IViewBase;

protected IDisposable AttachChild<TChildView, TParam>(
    ControlPresenterBase<TChildView, TParam> child, TChildView view, TParam parameters)
    where TChildView : IViewBase;
```

- 子にできるのは **コントロール Presenter だけ**。Window Presenter (クローズゲート・結果を持つ) は型制約でコンパイルエラー。
- `TChildView` / `TParam` は `child` から推論されます。View やパラメータの型違い、パラメータが必要な Presenter にパラメータなし版を使うことはコンパイルできません。

```csharp
public interface IOrderEditView : IWindowView
{
    ICustomerPanelView CustomerPanel { get; }   // 子の View インターフェイス (コントロールではない)
}

public class OrderEditPresenter : WindowPresenterBase<IOrderEditView, OrderEditInput>
{
    private readonly ICustomerRepository _customers;

    public OrderEditPresenter(ICustomerRepository customers) => _customers = customers;

    protected override void OnViewAttached() { }

    protected override void OnInitialize(OrderEditInput input)
    {
        // 固定パネル: 返り値のハンドルは無視してよい (親の破棄時に解放される)
        AttachChild(new CustomerPanelPresenter(_customers), View.CustomerPanel, input);
    }
}
```

**いつ呼ぶか**: `OnInitialize` (パラメータが使える) かそれ以降 (行の追加時など)。`OnViewAttached` では早すぎます。子は `AttachChild` から戻る前にアタッチ・初期化されます (子の `OnViewAttached` → `RegisterViewActions` → 子 View の `ActionBinder` を Bind → `OnInitialize`。子の `OnInitializeAsync` は開始のみで待たない)。親の `OnInitialize` で付けた子は、ウィンドウ表示前に動いています。親と子の非同期初期化は並行し得ます (`PresenterTestHost.Initialized` は親のみ)。

**返り値のハンドル**: 子を永久に解放 (Dispose) する `IDisposable`。固定パネルでは無視、ウィンドウを開いたまま外すパネル (明細行・タブ) では保持し、View がコントロールを取り除く前に `Dispose` します。2 回目以降の `Dispose` や、親が既に解放した後の `Dispose` は何もしません。手順は [HowTo: マスター/詳細 § 動的なパネル](HowTo-Implement-Master-Detail#動的なパネル-行の追加削除タブ) を参照。

**サービス・クローズ・検証**:

- 子は親のフレームワークサービス (`Messages` 等) を引き継ぎます (`PresenterTestHost.Start(services, …)` で起動した親の子も同じフェイクを使い、`ServiceLocator.Current` には落ちない)。`IPresenterFactory` で生成した子は、ファクトリが結び付けたサービス (とスコープ) を保ちます。M.E.DI では Presenter ごとにスコープが別なので、親子で共有されるのは渡したもの (入力 Model) だけです。
- 子はウィンドウのクローズ判定に参加しません。親が `CanClose` / `CanCloseAsync` で共有入力をチェックします。
- 各 Presenter は自分の `Validation` を持ちます。保存に必要な規則は Model か親に置き、親が入力全体を検証します。

**契約 (要約)**:

| 状況 | 挙動 |
|---|---|
| `child` / `view` が null | `ArgumentNullException` (何も移譲しない) |
| 親に View がまだ無い | `InvalidOperationException` |
| 親が破棄中 / 破棄済み、子が破棄済み、子のコントロールが破棄済み | `ObjectDisposedException` |
| 子がアタッチ・初期化・所有・`Connect` 済み、同じ親の別の子が同じ View を使用中 | `InvalidOperationException` (現在の所有者には影響なし) |
| 子がコンストラクタで別のフレームワークサービスを解決済み | `InvalidOperationException` (その処理は `OnViewAttached` / `OnInitialize` へ) |
| 子のアタッチ / 初期化 (同期部分) が例外 | 所有を取り消し、子を破棄して元の例外を再スロー。View には触れない (呼び出し側の所有) |
| 子の `OnInitializeAsync` が失敗 | 従来どおりログのみ (アタッチ失敗ではない) |
| 子を直接 `child.Dispose()` | 所有記録も消える |
| 子のコントロールが (Form 側で) 破棄された | 同じ経路で子を解放 (確認・Model 変更・View 操作はしない)。`Control.Disposed` だけが対象 |
| 解放済みの子 | 再アタッチ不可 |

所有者は 1 つ: 子として付けた Presenter を `Connect` すること、その逆も拒否されます。View は決して破棄されません。

---

## ライフサイクル

### Form 系 (WindowNavigator パターン)

1. Presenter インスタンスを生成
2. `WindowNavigator` が `IViewMappingRegister` から View 型を解決して View を生成
3. View を Presenter にアタッチ (`IViewAttacher<TView>.AttachView()`)
4. **`OnViewAttached()` 呼び出し**
5. **`RegisterViewActions()` 呼び出し** (フレームワークが続けて `View.ActionBinder?.Bind(Dispatcher)` を実行)
6. **`OnInitialize()` または `OnInitialize(parameters)` 呼び出し**
7. **`OnInitializeAsync(Lifetime)` 開始** (完了を待たない)
8. Form 表示 (Modal / Non-Modal) — `OnInitializeAsync` の `await` 中も表示・操作できる
9. (Form が閉じられたとき) Presenter を破棄 — 順序は [破棄の順序](#破棄の順序) を参照

### UserControl 系

1. UserControl は既にデザイナーまたはコードで生成済み
2. Presenter インスタンスを生成 (`new SettingsPresenter(deps?)` — Presenter 自身の依存のみ)
3. `presenter.Connect(view[, parameters])` を呼ぶ → 構築後に **View アタッチ** (`OnViewAttached`) → **アクション登録** (`RegisterViewActions`、続けてフレームワークが `ActionBinder` を自動 Bind) → **初期化** (`OnInitialize`、続けて `OnInitializeAsync` を開始) が走る
4. UserControl が `Dispose` されると Presenter も自動 `Dispose` (`Connect` が `ControlLifecycleController` 経由で橋渡し。監視は手順 3 の前から有効)

親 Presenter が `AttachChild` で付けた子も手順 3 と同じ順序で初期化されます。子が終わるのは、ハンドルの `Dispose`、親の破棄、子コントロールの破棄のいずれかです。

### 破棄の順序

`Dispose` は次の順で進みます。どの段階が例外を投げても後続の段階は必ず実行され、失敗はまとめて最後に再スローされます (1 件ならそのまま、複数なら `AggregateException`)。

1. **ディスパッチ停止** — この Presenter と子孫すべての `Dispatcher` が停止。以後、残っているコントロールのクリックは何もしない (`CanDispatch` は `false`、`Dispatch` / `DispatchAsync` はハンドラもミドルウェアも実行しない)。実行中の非同期ハンドラが完了しても `CanExecuteChanged` は発火しない
2. **`Lifetime` をキャンセル**
3. **バインダーを解除** — フレームワークが Bind した `ActionBinder` の `Unbind()` を呼ぶ
4. **`Cleanup()`**
5. **子 Presenter を付けた逆順に破棄** (子も同じ順序で破棄される。親のリソース — DI スコープ等 — がまだ使える)
6. **`Disposables` を解放**

View は破棄されません (Form / UserControl の破棄はそれぞれのホスト側)。

### 誤用時の挙動 (フェイルファスト)

- View をアタッチせずに `Initialize` すると `InvalidOperationException`。
- アタッチ済みの Presenter に 2 つ目の View をアタッチすると例外。
- UserControl 用 `Connect` が途中 (アタッチ/初期化) で失敗した場合、Presenter は破棄されます。初期化中にコントロールが破棄された場合も同様です (`ObjectDisposedException`)。
- `AttachChild` の誤用 (アタッチ済みの子、破棄済みの親など) は [契約表](#子-presenter-を所有する-attachchild) を参照。
- Form 用 `Connect` (Adopted) の挙動は [ウィンドウクローズモデル § Adopted ホスティング](Concept-Window-Closing-Model) を参照 (アタッチ済み・未初期化なら初期化、異なる View や 2 回目の `Connect` は例外、失敗時は破棄)。

---

## 主なフック (Override 可能なメソッド)

| メソッド | 呼ばれるタイミング | 主な用途 |
|---------|-----------------|---------|
| `OnViewAttached()` | View が注入された直後 | View イベントの購読 |
| `OnInitialize()` / `OnInitialize(TParam)` | View アタッチ後・表示前 | View プロパティの初期化、データロード |
| `OnInitializeAsync(CancellationToken)` | `OnInitialize` の直後に開始 (完了を待たずにウィンドウ表示) | 非同期のデータロード (DB・ネットワーク) |
| `RegisterViewActions()` | `OnInitialize` の前 (Initialize 内) | `Dispatcher.Register` でアクション登録 (この直後にフレームワークが ActionBinder を自動 Bind) |
| `Cleanup()` | Presenter が破棄されるとき | `Disposables` で賄えない特殊なリソース解放 |

すべて `protected override`。

### `OnInitializeAsync` と `Lifetime`

`protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)` は同期の `OnInitialize` の直後に開始され、ウィンドウはその完了を待たずに表示されます。重いデータロードはここに置くと UI がフリーズしません。

```csharp
protected override void OnInitialize()
{
    View.StatusMessage = "読み込み中...";   // 同期部分: すぐに表示したい初期状態
}

protected override async Task OnInitializeAsync(CancellationToken ct)
{
    try
    {
        View.Customers = await _repository.GetAllAsync(ct);
        View.StatusMessage = "完了";
    }
    catch (IOException ex)
    {
        Messages.ShowError($"読み込みに失敗しました: {ex.Message}");   // 想定内のエラーは中で処理
    }
}
```

- 未処理の例外はログ出力されます (ウィンドウは開いたまま)。ユーザーに伝えるべきエラーはオーバーライド内で捕まえてください。
- 渡されるトークンは `Lifetime` です。`protected CancellationToken Lifetime` は Presenter の `Dispose` の冒頭 (ディスパッチ停止の直後、`Cleanup` より前) にキャンセルされるので、読み込み中にウィンドウを閉じても処理が止まり、そのキャンセルはエラー扱いされません。
- `Lifetime` は非同期アクションハンドラにも自動で渡されます ([ViewAction § 非同期アクション](Reference-ViewAction-System#非同期アクション))。それ以外の、Presenter と一緒に止めたい非同期処理 (タイマー、ポーリング等) にも渡してください。
- テストでは `await host.Initialized;` で完了を待ってから検証します ([HowTo: Presenter をテストする](HowTo-Test-A-Presenter))。

### `Disposables` — 購読の自動解放バッグ

購読 (`IEventAggregator.Subscribe` のトークン、`Cascade.Bind` の戻り値、`+=`/`-=` のイベント) は
`Cleanup` で手動解除する代わりに、**作成行で `.DisposeWith(Disposables)` を付けるだけ**です。
バッグは遅延生成され (購読のない Presenter はコストゼロ)、Presenter の `Dispose` 時に
`Cleanup()` (と子 Presenter の破棄) の**後**にフレームワークが逆順で自動解放します — `Cleanup` を override して
base を呼び忘れても漏れません。

```csharp
protected override void OnViewAttached()
{
    Events.Subscribe<OrderShipped>(OnShipped).DisposeWith(Disposables);

    // View のイベントは SubscribeViewEvent で(解除は自動、破棄が始まった後のイベントは無視)
    SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h,
                       () => Dispatcher.RaiseCanExecuteChanged());
}
// Cleanup の override は不要
```

| メンバー | 説明 |
|---|---|
| `protected IDisposable SubscribeViewEvent(Action<EventHandler> add, Action<EventHandler> remove, Action handler)` | View の `EventHandler` イベントを購読。Presenter の破棄時に自動で解除し、破棄が始まった後に届いたイベントは無視する。返り値で早めに解除できる |
| `protected IDisposable SubscribeViewEvent<TEventArgs>(Action<EventHandler<TEventArgs>> add, Action<EventHandler<TEventArgs>> remove, Action<TEventArgs> handler)` | `EventHandler<TEventArgs>` 版。ハンドラーはイベント引数を受け取る |

詳細は [購読ライフサイクルの管理](HowTo-Manage-Presenter-Subscriptions) を参照してください。

---

## 公開 API は最小限に保つ

> **Presenter は "サービス" ではありません** — 外部から命令的に突けるサービスにしない、という 1 つの不変量で考えます。

ここが [MVP 設計ルール](Design-Rules) の **Rule 16・17 の正準の落とし所**です (Design-Rules 側はルール文と本ページへの参照のみ)。ルールは「公開メンバーの白名単」ではなく、*Presenter のユースケース行動は正当な通路 — ViewAction (視点の意図) + ライフサイクル (構築 / `Initialize` / `Dispose`) — からのみ駆動され、任意の外部メソッド呼び出しからは駆動されない* という不変量です。これを **入站 (他者が呼んで Presenter を動かすメソッド) と 出站 (Presenter が出すイベント/通知) で非対称に**適用します:

- **入站 = 命令面 → 厳しく圧縮する。** 硬い約束: ViewAction の handler / helper は必ず `private`。`public Save()` を生やすと Dispatcher の `CanExecute`・ミドルウェアを迂回する経路ができ、Presenter がサービス化します。これが非協商部分です。
- **出站 = 通知面 → 圧縮対象ではない。** 上向きの出力ポート / 通知イベントは公開してよい (後述)。公開イベントは Presenter を命令的に突けるようにはしないからです。

### メソッドの可視性ガイドライン

| メソッドの種類 | 可視性 |
|--------------|------|
| コンストラクタ / `Dispose` | `public` (既定の公開面)。`AttachView` / `Initialize` は `IViewAttacher<TView>` / `IInitializable` の明示的インターフェイス実装で、`presenter.Initialize()` とは書けない — 本番は `WindowNavigator` / `Connect`、テストは `PresenterTestHost` 経由 |
| 親→子の狭いコマンド | 既定 `internal` (同一アセンブリ)。縫い目が要るときだけ公開コマンドインターフェイス — 接口は強制しない |
| ライフサイクルフック (`OnInitialize`、`OnInitializeAsync`、`OnViewAttached`、`RegisterViewActions`、`Cleanup`) | `protected override` |
| クローズゲート (`CanClose(CloseReason)` / `CanCloseAsync(CloseReason)`) | `protected override` |
| ViewAction ハンドラ (`OnSave`、`OnCancel`、...) | `private` (硬い約束) |
| View イベントハンドラ (`OnSelectionChanged`、...) | `private` |
| ヘルパー (検証、フォーマッタ等) | `private` |

### 出站イベント: 公開してよいもの / 禁じるもの

圧縮するのは**入站の命令面**であって、出站の通知ではありません。**公開してはいけない**のは、内部状態を載せたイベント (`IsDirtyChanged`) や View イベントを再発行するイベント (`SelectionChanged`) — それらは本来 Model / Service / 共有状態オブジェクトが持つべき観測点を Presenter に漏らします。下の NG / OK 例がちょうどこの境界を描きます。

通知の性質ごとに手段を選びます:

- **ウィンドウの結果通知 (上向き・単発)** → 基底 `RequestClose(result, status)` を呼ぶ (✅ 出力ポート。インターフェイス実装不要。フレームワークが `InteractionResult<TResult>` に変換して呼び出し元に返す)
- **親子の連携 (親 → 子)** → **まず共有 Model / イベントを検討する**。直接命令は**窄い例外**で、本物の一回的コマンド (`Reset()` 等、他に関係者がなく子の状態問い合わせも不要) のときだけ使います。その場合も **handler は `private` のまま (硬線)**、露出形式は既定 `internal`、差し替え / テストの縫い目が要るときだけコマンドインターフェイス (**接口は強制しない**)。[Rule 10](Design-Rules#rule-10-view-にアクセスするのは-presenter-だけ) ・ [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) 参照
- **共有・観測されるステート (注文・認証等)** → ステートは Service / 共有状態オブジェクトに持たせ、その変更通知イベントを発火
- **モジュール横断の通知** → `IEventAggregator` で publish (購読トークンは `.DisposeWith(Disposables)`、UI スレッド整列)

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
        RequestClose(result, InteractionStatus.Ok);   // Push close through CanClose — TResult inferred from result
    }
}
```

---

## テスト時のルール

> **テストのために可視性を緩めない**。

Presenter を **本物のエントリーポイントから** 駆動してください。これによってテストが production と同じ経路を通ります。

```csharp
// ❌ Wrong:   presenter.OnSave();
// ✅ Right:   view.Binder.Dispatch(StandardActions.Save);

// Presenter のホスト — AttachView / Initialize は明示的インターフェイス実装なので直接は呼べない。
// PresenterTestHost (WinformsMVP.Testing) がモック View をアタッチして実際の初期化を走らせる
using (var host = PresenterTestHost.Start(presenter, view))
{
    // Push (RequestClose) のテスト — ゲートを通って受理されたクローズをホストが記録する
    view.Binder.Dispatch(StandardActions.Save);
    Assert.Equal(InteractionStatus.Ok, host.CloseStatus);   // CanCloseAsync なら先に await host.WaitForCloseDecisionAsync()

    // ゲート (CanClose) のテスト — X / Alt+F4 と同じゲート (CanCloseAsync が未回答なら null)
    Assert.False(host.CanClose(CloseReason.Normal));

    // 非同期: ハンドラ / OnInitializeAsync / CanCloseAsync の完了を await する
    await view.Binder.DispatchAsync(StandardActions.Refresh);
    await host.Initialized;
    Assert.True(await host.CanCloseAsync(CloseReason.Normal));
}
```

`OnSave` を `private` から `internal` / `public` に格上げしてテストすると、production が通らない経路がテストで通ってしまい、CanExecute 判定をスキップする等の不具合を見逃します。

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) と [HowTo: ウィンドウクローズを扱う § テストパターン](HowTo-Handle-Window-Closing#テストパターン) を参照してください。

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
| `Lifetime` | `CancellationToken` | Presenter の破棄 (ウィンドウを閉じる / コントロール破棄) でキャンセルされるトークン |
| `AttachChild(child, view[, parameters])` | `IDisposable` (メソッド) | 子パネルの Presenter を所有・初期化 ([上記](#子-presenter-を所有する-attachchild)) |
| `Validation` | `ValidationState` | 検証エラー (フィールド名ごと)。`Validate(model)` / `Check(model)` / `SetError` / `Reset`。View が `IValidatableView` なら `ValidationErrors` に自動接続され、変更のたびに `canExecute` を再評価 ([フォーム入力を検証する](HowTo-Validate-Form-Input)) |

`Messages` / `Dialogs` / `Files` / `Logger` / `Navigator` は `ServiceLocator.Current` (`IServiceProvider`) から遅延解決されます。
DI コンテナと統合する場合の構成方法は [Dependency Injection](Reference-DependencyInjection) を参照してください。

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — Presenter の責務範囲
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — `RequestClose` / `CanClose` / `CanCloseAsync` の詳細
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Register` の全形式
- [WindowNavigator](Reference-WindowNavigator) — Form 系 Presenter の表示方法
- [Platform Services](Reference-Platform-Services) — 内部ヘルパーで使えるサービス群
- [MVP 設計ルール](Design-Rules) — ルール 16 (メソッド可視性) ・ルール 17 (イベント最小化) の詳細
