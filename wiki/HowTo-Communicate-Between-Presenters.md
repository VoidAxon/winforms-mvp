# HowTo: Presenter 間の通信方法

このページでは、複数の Presenter が連携する必要があるときの **4 つの選択肢** とその使い分けを示します。
よくある誤りは「全部を EventAggregator でやろうとする」ですが、状況に応じた最適解があります。**迷ったら、まず「共有 Model + イベント」を検討してください**（このページは推奨度の高い順に並べています）。

---

## 目次

- [4 つの選択肢](#4-つの選択肢)
- [選び方の決定木](#選び方の決定木)
- [共有 Model + イベント (推奨)](#共有-model--イベント)
- [EventAggregator (pub/sub)](#eventaggregator-pubsub)
- [コールバック (`onClosed`)](#コールバック-onclosed)
- [親が子のメソッドを直接呼ぶ (最終手段)](#親が子のメソッドを直接呼ぶ-最終手段)
- [比較表](#比較表)
- [アンチパターン](#アンチパターン)
- [関連ページ](#関連ページ)

---

## 4 つの選択肢

推奨度の高い順:

| 選択肢 | 結合度 | 用途 |
|--------|------|----|
| **共有 Model + イベント** (推奨) | 中 (Model 経由) | 状態を所有する責任が明確に存在するとき |
| **EventAggregator** | 弱 (結合なし) | 状態の所有者がない・複数 Presenter が反応する通知 |
| **コールバック (`onClosed`)** | 弱 (一方向) | 子ウィンドウの結果を 1 回だけ親に返す |
| **直接メソッド呼び出し** (最終手段) | 強 (親→子) | 親が子をコンポジション所有し、狭い一回的コマンドだけのとき |

---

## 選び方の決定木

```
質問 1: ステート (データ) を持つ?
├── はい
│    ├── 質問 2: 単一の責任主体がいる?
│    │    ├── はい → 共有 Model + イベント (推奨)
│    │    └── いいえ → 設計を見直す (どこかが状態を持つべき)
│    └── (省略)
└── いいえ (通知のみ)
     ├── 質問 3: 1 回だけ親に返す結果?
     │    ├── はい → コールバック または RequestClose (Push 方向)
     │    └── いいえ
     │         ├── 質問 4: 以下を「すべて」満たす?
     │         │    ・親が子をコンポジションで所有
     │         │    ・操作は 1〜2 個の狭いコマンドで完結
     │         │    ・第三の Presenter は関心を持たない
     │         │    ・子の状態を問い合わせる必要がない
     │         │    ├── はい → 直接メソッド呼び出し (最終手段)
     │         │    └── いいえ → 共有 Model + イベント または EventAggregator
```

---

## 共有 Model + イベント

ステートを所有する **共有 Model** を用意し、Presenter は Model を介して連携します。**最も推奨される方法**。Presenter 同士は互いを知りません。

### こんなときに

- ステート (注文・カート・ユーザーセッション等) が明確に存在
- 複数 Presenter が同じ状態を読み書きする
- ビジネスルールを 1 か所に集約したい

### 例: 注文管理

```csharp
// Model がステートを所有
public interface IOrderModel
{
    IReadOnlyList<OrderItem> OrderItems { get; }
    decimal TotalAmount { get; }

    void AddProduct(Product product, int quantity);
    void RemoveItem(OrderItem item);
    void ClearOrder();

    event EventHandler<ProductAddedEventArgs> ProductAdded;
    event EventHandler<TotalChangedEventArgs> TotalChanged;
    event EventHandler OrderCleared;
}

// Presenter A: 状態を変更する
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IOrderModel _orderModel;

    public ProductSelectorPresenter(IOrderModel orderModel)
    {
        _orderModel = orderModel;
    }

    private void OnAddToOrder()
    {
        _orderModel.AddProduct(View.SelectedProduct, View.Quantity);
    }
}

// Presenter B: 変化を購読する
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IOrderModel _orderModel;

    public OrderSummaryPresenter(IOrderModel orderModel)
    {
        _orderModel = orderModel;
    }

    protected override void OnViewAttached()
    {
        _orderModel.ProductAdded += OnProductAdded;
        _orderModel.OrderCleared += OnOrderCleared;
    }

    private void OnProductAdded(object sender, ProductAddedEventArgs e)
    {
        View.OrderItems = _orderModel.OrderItems;     // ← クエリも可能
        View.Total = _orderModel.TotalAmount;
    }

    protected override void Cleanup()
    {
        _orderModel.ProductAdded -= OnProductAdded;
        _orderModel.OrderCleared -= OnOrderCleared;
    }
}
```

### メリット / デメリット

- ✅ Presenter 間の結合がゼロ
- ✅ ステートの所有者が明確 (Model)
- ✅ クエリも可能 (`var items = _orderModel.OrderItems`)
- ✅ ビジネスルールを Model に集約できる
- ✅ テストが楽 (Model をモックすればよい)
- ❌ Model の抽象化が必要
- ❌ Model インスタンスを DI コンテナで共有する必要がある

完全なサンプルは `samples/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` 参照。

---

## EventAggregator (pub/sub)

ステートの所有者がいない・複数 Presenter が同じ通知に反応する場合、**EventAggregator** で疎結合に通知します。

### こんなときに

- ステートを所有する責任が特定の Model にない
- モジュール横断の通知 (例: 「ユーザーがログアウトしたら全 Presenter で表示をリセットしたい」)
- 親子関係も Model 関係もない Presenter 間の通信

### 例: モジュール横断のログアウト通知

```csharp
// メッセージ
public class UserLoggedOutNotification { }

// 発行 (Auth モジュール)
public class AuthPresenter : WindowPresenterBase<IAuthView>
{
    private readonly IEventAggregator _eventAggregator;

    private void OnLogout()
    {
        _authService.Logout();
        _eventAggregator.Publish(new UserLoggedOutNotification());
    }
}

// 購読 (各モジュールの Presenter が自分の責任で対応)
public class UserListPresenter : WindowPresenterBase<IUserListView>
{
    private IDisposable _subscription;

    protected override void OnViewAttached()
    {
        // ハンドラはインスタンスメソッドで渡す（this をキャプチャするラムダは弱参照下で静かに失効する）
        _subscription = _eventAggregator.Subscribe<UserLoggedOutNotification>(OnUserLoggedOut);
    }

    private void OnUserLoggedOut(UserLoggedOutNotification _)
    {
        View.ClearList();
        View.ShowLoginPrompt();
    }

    protected override void Cleanup()
    {
        _subscription?.Dispose();
    }
}
```

### メリット / デメリット

- ✅ Presenter 間の結合ゼロ
- ✅ UI スレッドへの自動マーシャリング (バックグラウンドから発行可能)
- ✅ 弱参照で自動クリーンアップ
- ❌ 「誰がそれに反応するか」が見えにくい
- ❌ デバッグでフロー追跡が大変
- ❌ ステート管理には不向き

### EventAggregator の誤用に注意

```csharp
// ❌ ステート管理に EventAggregator を使う (悪い)
_eventAggregator.Publish(new AddProductMessage(product));   // 誰が「現在の注文」を持つ?
var snapshot = GetOrderSnapshot();                          // Request/Response... 不自然

// ✅ ステート管理は共有 Model、通知だけ EventAggregator (良い)
_orderModel.AddProduct(product);                          // Model がステート所有
_eventAggregator.Publish(new ProductAddedNotification(...)); // 他モジュールへの通知だけ pub/sub
```

詳細は [EventAggregator](Reference-EventAggregator) を参照。完全サンプルは `samples/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/`。

---

## コールバック (`onClosed`)

子ウィンドウから親へ **1 回だけ** 結果を返したい場合は、Navigator のコールバックを使います。
Modal で済むなら `ShowAsModal<TResult>()` の戻り値 (`InteractionResult<TResult>`) を使う方が直接的。

### Modal: 戻り値で受け取る

```csharp
var result = Navigator.For(presenter).ShowAsModal<UserResult>();
if (result.IsOk)
    ReloadUser(result.Value.Id);
```

### Non-Modal: コールバックで受け取る

```csharp
Navigator.ShowWindow<DocumentPresenter, EditResult>(
    presenter,
    onClosed: result =>
    {
        if (result.IsOk)
            ReloadList();
    });
```

### メリット / デメリット

- ✅ 結果伝達が明示的・一方向
- ✅ 1 対 1 の関係に最適
- ❌ 1 回しか送れない (継続的な通知には不向き)
- ❌ 親と子のクラスが結合する (親が子の型を知る)

詳細は [WindowNavigator](Reference-WindowNavigator) と [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照。

---

## 親が子のメソッドを直接呼ぶ (最終手段)

最も結合度が強く、**通常は避けます**。親 Presenter が、自分が所有する子 Presenter のメソッドを直接呼ぶ方法です。下の 4 条件を **すべて** 満たす狭いケースに限ってのみ正当化されます。

> ⚠️ **使う前に必ず確認** — Presenter に外部から呼べるメソッドを生やすと、ViewAction の Dispatcher と CanExecute をバイパスする経路が生まれ、`Presenter` がサービス化しかねません ([Rule 16](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ) 参照。だから露出は `public` でなく `internal` に絞る — 後述)。
> 下記の **4 条件は「これは本物の一回的コマンドか」を判定する基準**です。4 条件をすべて満たすなら、直接コマンドが正しい道具です。逆にどれかが崩れたら (特に第三者が関心を持つ / 子の状態を問い合わせたい)、それはもう一回的コマンドではなく**共有・観測される状態**なので、命令ではなく **共有 Model** に載せます — **性質で選ぶ**のであって、Model が常に優先なのではありません。
>
> 📌 末尾の [アンチパターン「Presenter が他の Presenter の参照を直接持つ」](#アンチパターン) と矛盾するように見えますが、境界は明確です: あのアンチパターンは「**状態や通知のために** 他 Presenter を保持する」ことを禁じます。ここで許されるのは「親が composition で所有する子に、**状態でも通知でもない一回的コマンドを 1 つだけ** 送る」狭い例外です。1 つでも条件が崩れたら共有 Model に移します。

### こんなときに (厳密な 4 条件、すべて満たすこと)

- ✅ 親 Form/UserControl が子 UserControl を **コンポジション関係で所有** している
- ✅ 子に対する操作が **1〜2 個の狭く特定的なコマンド** で完結する (`Reset()`, `Refresh()`, `ClearSelection()` のような局所的なもの)
- ✅ **第三の Presenter が同じ操作・状態に関心を持つ可能性がない**
- ✅ 子の **内部状態を問い合わせる必要がない** (コマンド一方向のみ。クエリが必要なら Model の責務)

### 例: データソース切替時に検索パネルをリセットする

「データソース切替で検索条件をリセット」— 一回的・狭い・他に関係者がいないコマンドの例。**子の生成と所有は composition root (親 Form) が行い**、親 Presenter は子を `internal` の seam 経由で受け取ります（親 View に子コントロール/子 View を生やさない）。

```csharp
// composition root: 親 Form が両方を生成して結線する
public partial class MainForm : Form, IMainView
{
    private readonly MainPresenter _main;

    public MainForm()
    {
        InitializeComponent();

        var search = new SearchPanelPresenter();
        search.Connect(_searchPanelControl);   // 子はデザイナー上のコントロール。Form が所有

        _main = new MainPresenter(search);      // 親へは子 Presenter を内部 seam で渡す
        _main.Connect(this);
    }
}

public class MainPresenter : WindowPresenterBase<IMainView>
{
    // 親が子を保持する = 結合あり。だから「最終手段」かつ狭い 4 条件付き。
    private readonly SearchPanelPresenter _search;

    public MainPresenter(SearchPanelPresenter search) => _search = search;

    private void OnDataSourceChanged()
    {
        _search.Reset();   // 狭く・一回的なコマンド
    }
}

public class SearchPanelPresenter : ControlPresenterBase<ISearchPanelView>
{
    // ⚠️ public ではなく internal — DI コンテナや無関係なクラスからは呼べないようにする
    internal void Reset()
    {
        View.ClearKeyword();
        View.ResetFilters();
    }
}
```

> 💡 多くの場合、この「リセット」すら共有 Model にした方が素直です（例: `IDataSourceModel.DataSourceChanged` を SearchPanel 側が購読して自分でリセット → 親子結合が消える）。直接呼び出しは本当に上の 4 条件を満たすときだけにしてください。

### なぜ `public` ではなく `internal` か

`public` メソッドはアセンブリ外の任意の呼び出し元に「Presenter をサービスとして使う」道を開いてしまいます。`internal` にしておけば、同一アセンブリ内の親 Presenter からのみ呼ばれることが型レベルで保証されます。これは Rule 16 (入站の命令面を最小化し、handler は private) の精神を、どうしてもメソッドが必要な狭いケースに適用したものです。差し替えやテストで親を mock したい「縫い目」が要るなら、`internal` メソッドをコマンドインターフェイスへ引き上げます (接口は必須ではなく、その縫い目が要るときの選択肢)。

### 「もう共有 Model にすべき」サイン

以下のいずれかに該当したら、**共有 Model へリファクタすべき** サインです。`public`/`internal` メソッドを増やして対応してはいけません。

| サイン | 理由 |
|------|------|
| 子に対する公開メソッドが 3 つ以上になりそう | Presenter がサービスのように肥大化している |
| 子の状態を問い合わせたい (`bool IsDirty` 等) | コマンドだけでなくクエリが必要 → 状態の所有者が必要 |
| 第三の Presenter も同じ操作・通知に反応したい | もはや「親子関係」ではない。アプリ規模の関心事 |
| 扱う概念が「テーマ・ユーザー・カート・セッション」などアプリ全体の状態 | ドメインステート → Model が所有すべき |

### 典型的な誤用: アプリ全体の状態をこの方法で扱う

```csharp
// ❌ Bad — テーマはアプリ全体の状態。複数の Presenter が関心を持つ可能性がある
_settingsPresenter.UpdateTheme(themeName);

// ✅ Good — 共有 Model に移譲
_themeModel.ChangeTheme(themeName);   // Model が状態を所有・通知
```

### メリット / デメリット

- ✅ シンプル、デバッグしやすい (F12 ナビ可能)
- ✅ Model レイヤー不要で構造が最も浅い
- ❌ 親が子のクラスを知っている (結合あり)
- ❌ 子から親への通知は別の手段が必要 (親→子の一方向限定)
- ❌ 拡張に弱い — メソッドを増やすと急速にアンチパターン化する

---

## 比較表

| 観点 | 共有 Model | EventAggregator | コールバック | 直接呼び出し |
|------|-----------|---------------|----------|----------|
| **結合度** | 中 | 弱 | 中 | 強 |
| **ステート管理** | ✅ 推奨 | ❌ 不向き | ❌ | ❌ |
| **通知 1 対 1** | ✅ | ⚠️ 可能だが過剰 | ✅ | ✅ |
| **通知 1 対 N** | ✅ | ✅ 推奨 | ❌ | ❌ |
| **クエリ可能** | ✅ | ⚠️ Request パターン要 | ❌ | ⚠️ 必要なら Model へ |
| **デバッグしやすさ** | ✅ | ❌ | ✅ | ✅ |
| **テストしやすさ** | ✅ | ✅ | ✅ | ✅ |
| **モジュール横断** | ⚠️ | ✅ | ❌ | ❌ |

---

## アンチパターン

### ❌ アプリ全体の状態を「直接メソッド呼び出し」で扱う

```csharp
// ❌ Bad — テーマはアプリ全体の状態。public メソッドが次々と増えていく
public class MainPresenter
{
    private SettingsPanelPresenter _settings;

    private void OnApplyTheme(string theme)   => _settings.UpdateTheme(theme);
    private void OnApplyLanguage(string lang) => _settings.UpdateLanguage(lang);
    private void OnApplyFont(string font)     => _settings.UpdateFont(font);
    // ↑ Presenter がサービス化、Dispatcher と CanExecute がバイパスされる
}

// ✅ Good — 状態は Model が所有、各 Presenter は購読
public class MainPresenter
{
    private readonly IThemeModel _themeModel;
    private void OnApplyTheme(string theme) => _themeModel.ChangeTheme(theme);
}

public class SettingsPanelPresenter : ControlPresenterBase<ISettingsPanelView>
{
    private readonly IThemeModel _themeModel;

    protected override void OnViewAttached()
    {
        _themeModel.ThemeChanged += (s, e) => View.ApplyTheme(e.Theme);
    }
}
```

「親子関係」で直接メソッド呼び出しを正当化できるのは **狭く・一回的・拡張しない** 操作のみ。テーマ・ユーザー・カート等のアプリ概念は必ず Model へ。

### ❌ Presenter が他の Presenter の参照を直接持つ (状態・通知のために)

```csharp
// ❌ Bad — 状態や通知のために他 Presenter を保持する直接結合
public class MainPresenter
{
    private readonly OrderSummaryPresenter _orderSummary;   // ← これはやらない

    public MainPresenter(OrderSummaryPresenter orderSummary)
    {
        _orderSummary = orderSummary;
    }
}
```

代わりに共有 Model か EventAggregator を使う。

> ⚠️ **例外は「直接メソッド呼び出し (最終手段)」だけ** — composition で所有する子に、状態でも通知でもない一回的コマンドを 1 つ送るケースに限り、親が子参照を保持してよい (4 条件を満たすこと)。それ以外で他 Presenter を保持するのは本アンチパターンです。

### ❌ Presenter のイベントで共有ステートを通知する

```csharp
// ❌ Bad — Presenter がイベントを公開してステート通知にしている
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    public event EventHandler ProductAdded;   // ← 内部状態の露出
}
```

禁じられるのは **共有ステートの通知を Presenter のイベントで担わせること** です (それは Model の責務)。共有ステートの変更通知は共有 Model 側に持たせます。

一方、**ウィンドウのクローズ結果を返す** (基底 `RequestClose(result, status)` を呼ぶ) ことは最小化の精神と矛盾せず、許容されます — 規則が本当に対象とするのは「外部から命令的に突ける入口」であって、フレームワークが `InteractionResult<TResult>` として返す出口側ではありません。境界の根拠は [Presenter の責務](Concept-Presenter-Responsibilities) を参照。

### ❌ EventAggregator でステートを管理しようとする

```csharp
// ❌ Bad
_eventAggregator.Publish(new AddProductToCartMessage(product));   // どこに保存される?
_eventAggregator.Publish(new GetCartSnapshotRequest(snapshot));   // Request/Response 不自然

// ✅ Good — ステートは Model、通知だけ EventAggregator
_cartModel.AddProduct(product);                                    // Model が保存
_eventAggregator.Publish(new CartUpdatedNotification());           // 通知だけ
```

### ❌ View が Presenter の業務メソッドを呼ぶ

```csharp
// ❌ Bad — View が Presenter を能動的に駆動する (鉄則 3 違反)
public class MyForm : Form, IMyView
{
    private MyPresenter _presenter;

    private void btnSave_Click(object sender, EventArgs e)
    {
        _presenter.Save();   // ← この「業務メソッド呼び出し」が依存方向を逆転させる
    }
}
```

✅ View はイベント (`SaveRequested` 等) を公開し、Presenter にそれを購読させる。または ViewAction で `_binder.Add(SaveAction, btnSave)` と宣言する。

> 💡 **3 つを区別する: 持つ / ライフサイクル呼び出し / 業務メソッド呼び出し**
>
> 以下は **MVP 違反ではありません** (Form が composition root を兼ねているだけ):
>
> | 例 | 種別 | 可否 |
> |---|---|---|
> | `private MyPresenter _presenter;` | フィールド保持 | ✅ |
> | `_presenter.AttachView(this);` | ライフサイクル | ✅ |
> | `_presenter.Initialize();` | ライフサイクル | ✅ |
> | `_presenter.Dispose();` | ライフサイクル | ✅ |
>
> 規則が真に禁じているのは **View から Presenter の業務メソッド (use-case メソッド) を能動的に呼ぶ** ことです:
>
> | 例 | 種別 | 可否 |
> |---|---|---|
> | `btnSave_Click → _presenter.Save();` | 業務メソッド | ❌ |
> | `txtName_TextChanged → _presenter.OnNameChanged();` | 業務メソッド | ❌ |
>
> これらが依存方向を逆転させる本質的な違反です。理想は `Program.Main()` で wire-up しますが、レガシー Form 内に書く折衷は許容されます — **ライフサイクル呼び出しと業務メソッド呼び出しを混同しない** ことが重要です。

([MVP の鉄則 3](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) 参照)

---

## 関連ページ

- [EventAggregator](Reference-EventAggregator) — pub/sub の API 詳細
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — 公開 API ガイドライン (ルール 16・17)
- [WindowNavigator](Reference-WindowNavigator) — Modal / Non-Modal の戻り値・コールバック
- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 3 (依存方向)
- [連鎖選択 (カスケード) を扱う](HowTo-Handle-Cascading-Selection) — 主従/N 段連鎖を `ISelectionStore<T>` + `Cascade` で
- サンプル:
  - `samples/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` — Model ベース
  - `samples/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/` — EventAggregator ベース
