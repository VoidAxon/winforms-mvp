# HowTo: Presenter 間の通信方法

このページでは、複数の Presenter が連携する必要があるときの **4 つの選択肢** とその使い分けを示します。
よくある誤りは「全部を EventAggregator でやろうとする」ですが、状況に応じた最適解があります。

---

## 目次

- [4 つの選択肢](#4-つの選択肢)
- [選び方の決定木](#選び方の決定木)
- [選択肢 1: 親が子のメソッドを直接呼ぶ (最も限定的に使う)](#選択肢-1-親が子のメソッドを直接呼ぶ-最も限定的に使う)
- [選択肢 2: 共有 Model + イベント](#選択肢-2-共有-model--イベント)
- [選択肢 3: EventAggregator (pub/sub)](#選択肢-3-eventaggregator-pubsub)
- [選択肢 4: コールバック (`onClosed`)](#選択肢-4-コールバック-onclosed)
- [比較表](#比較表)
- [アンチパターン](#アンチパターン)
- [関連ページ](#関連ページ)

---

## 4 つの選択肢

| 選択肢 | 結合度 | 用途 |
|--------|------|----|
| **直接メソッド呼び出し** | 強 (親→子) | 親 Presenter が明確に子を制御するとき |
| **共有 Model + イベント** | 中 (Model 経由) | 状態を所有する責任が明確に存在するとき |
| **EventAggregator** | 弱 (結合なし) | 状態の所有者がない・複数 Presenter が反応する通知 |
| **コールバック (`onClosed`)** | 弱 (一方向) | 子ウィンドウの結果を 1 回だけ親に返す |

---

## 選び方の決定木

```
質問 1: ステート (データ) を持つ?
├── はい
│    ├── 質問 2: 単一の責任主体がいる?
│    │    ├── はい → 選択肢 2: 共有 Model
│    │    └── いいえ → 設計を見直す (どこかが状態を持つべき)
│    └── (省略)
└── いいえ (通知のみ)
     ├── 質問 3: 1 回だけ親に返す結果?
     │    ├── はい → 選択肢 4: コールバック または IRequestClose<TResult>
     │    └── いいえ
     │         ├── 質問 4: 以下を「すべて」満たす?
     │         │    ・親が子をコンポジションで所有
     │         │    ・操作は 1〜2 個の狭いコマンドで完結
     │         │    ・第三の Presenter は関心を持たない
     │         │    ・子の状態を問い合わせる必要がない
     │         │    ├── はい → 選択肢 1: 直接メソッド呼び出し (internal)
     │         │    └── いいえ → 選択肢 2 (Model) または 選択肢 3 (EventAggregator)
```

---

## 選択肢 1: 親が子のメソッドを直接呼ぶ (最も限定的に使う)

最も単純で結合度の強い方法。親 Presenter が、自分が所有する子 Presenter のメソッドを直接呼びます。

> ⚠️ **使う前に必ず確認** — Presenter に `public` メソッドを追加すると、ViewAction の Dispatcher と CanExecute をバイパスする経路が作られ、`Presenter` がサービス化してしまいます ([Rule 16・17](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ) 参照)。
> 「親子関係が明確だから」という理由だけでは不十分です。下記の **4 条件をすべて満たす場合のみ** この選択肢を使ってください。それ以外は **選択肢 2 (共有 Model)** を選びます。

### こんなときに (厳密な 4 条件、すべて満たすこと)

- ✅ 親 Form/UserControl が子 UserControl を **コンポジション関係で所有** している
- ✅ 子に対する操作が **1〜2 個の狭く特定的なコマンド** で完結する (`Reset()`, `Refresh()`, `ClearSelection()` のような局所的なもの)
- ✅ **第三の Presenter が同じ操作・状態に関心を持つ可能性がない**
- ✅ 子の **内部状態を問い合わせる必要がない** (コマンド一方向のみ。クエリが必要なら Model の責務)

### 例: データソース切替時に検索パネルをリセットする

主 Form がデータソース選択を変えたら、検索パネルの条件をリセットする — 「父→子の一回的・狭い・他に関係者がいない」操作。

```csharp
public class MainPresenter : WindowPresenterBase<IMainView>
{
    private SearchPanelPresenter _searchPresenter;

    protected override void OnViewAttached()
    {
        // 親が子をコンポジションで所有
        _searchPresenter = new SearchPanelPresenter(View.SearchPanel);
    }

    private void OnDataSourceChanged()
    {
        // 親が子に対して狭く・一回的な指示を出す
        _searchPresenter.Reset();
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

### なぜ `public` ではなく `internal` か

`public` メソッドはアセンブリ外の任意の呼び出し元に「Presenter をサービスとして使う」道を開いてしまいます。`internal` にしておけば、同一アセンブリ内の親 Presenter からのみ呼ばれることが型レベルで保証されます。これは Rule 16 (handler は private) と Rule 17 (public 表面は最小限) の精神を、どうしてもメソッドが必要な狭いケースに適用する妥協案です。

### 「もう Model にすべき」サイン

以下のいずれかに該当したら、**選択肢 2 (共有 Model) へリファクタすべき** サインです。`public`/`internal` メソッドを増やして対応してはいけません。

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

// ✅ Good — 共有 Model (選択肢 2) に移譲
_themeModel.ChangeTheme(themeName);   // Model が状態を所有・通知
```

### メリット / デメリット

- ✅ シンプル、デバッグしやすい (F12 ナビ可能)
- ✅ Model レイヤー不要で構造が最も浅い
- ❌ 親が子のクラスを知っている (結合あり)
- ❌ 子から親への通知は別の手段が必要 (親→子の一方向限定)
- ❌ 拡張に弱い — メソッドを増やすと急速にアンチパターン化する

---

## 選択肢 2: 共有 Model + イベント

ステートを所有する **共有 Model** を用意し、Presenter は Model を介して連携します。**最も推奨される方法**。

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

    public ProductSelectorPresenter(IProductSelectorView view, IOrderModel orderModel)
        : base(view)
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

    public OrderSummaryPresenter(IOrderSummaryView view, IOrderModel orderModel)
        : base(view)
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

完全なサンプルは `src/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` 参照。

---

## 選択肢 3: EventAggregator (pub/sub)

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
        _subscription = _eventAggregator.Subscribe<UserLoggedOutNotification>(_ =>
        {
            View.ClearList();
            View.ShowLoginPrompt();
        });
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

詳細は [EventAggregator](Reference-EventAggregator) を参照。完全サンプルは `src/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/`。

---

## 選択肢 4: コールバック (`onClosed`)

子ウィンドウから親へ **1 回だけ** 結果を返したい場合は、Navigator のコールバックを使います。
Modal で済むなら `ShowAsModal<TResult>()` の戻り値 (`InteractionResult<TResult>`) を使う方が直接的。

### Modal: 戻り値で受け取る

```csharp
var result = Navigator.For(presenter).ShowAsModal<UserResult>();
if (result.IsSuccess)
    ReloadUser(result.Value.Id);
```

### Non-Modal: コールバックで受け取る

```csharp
Navigator.ShowWindow<DocumentPresenter, EditResult>(
    presenter,
    onClosed: result =>
    {
        if (result.IsSuccess)
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

## 比較表

| 観点 | 直接呼び出し | 共有 Model | EventAggregator | コールバック |
|------|----------|-----------|---------------|----------|
| **結合度** | 強 | 中 | 弱 | 中 |
| **ステート管理** | ❌ | ✅ 推奨 | ❌ 不向き | ❌ |
| **通知 1 対 1** | ✅ | ✅ | ⚠️ 可能だが過剰 | ✅ |
| **通知 1 対 N** | ❌ | ✅ | ✅ 推奨 | ❌ |
| **クエリ可能** | ⚠️ 必要なら Model へ | ✅ | ⚠️ Request パターン要 | ❌ |
| **デバッグしやすさ** | ✅ | ✅ | ❌ | ✅ |
| **テストしやすさ** | ✅ | ✅ | ✅ | ✅ |
| **モジュール横断** | ❌ | ⚠️ | ✅ | ❌ |

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

「親子関係」で選択肢 1 を正当化できるのは **狭く・一回的・拡張しない** 操作のみ。テーマ・ユーザー・カート等のアプリ概念は必ず Model へ。

### ❌ Presenter が他の Presenter の参照を直接持つ

```csharp
// ❌ Bad — Presenter 間の直接結合
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

### ❌ Presenter が public イベントを公開

```csharp
// ❌ Bad — Presenter がイベントを公開してステート通知にしている
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    public event EventHandler ProductAdded;   // ← 内部状態の露出
}
```

通知は共有 Model 側に持たせる ([Presenter 基底クラス#公開-api-は最小限に保つ](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ) 参照)。

### ❌ EventAggregator でステートを管理しようとする

```csharp
// ❌ Bad
_eventAggregator.Publish(new AddProductToCartMessage(product));   // どこに保存される?
_eventAggregator.Publish(new GetCartSnapshotRequest(snapshot));   // Request/Response 不自然

// ✅ Good — ステートは Model、通知だけ EventAggregator
_cartModel.AddProduct(product);                                    // Model が保存
_eventAggregator.Publish(new CartUpdatedNotification());           // 通知だけ
```

### ❌ View が Presenter を知る

```csharp
// ❌ Bad — View が Presenter の参照を持つ (鉄則 3 違反)
public class MyForm : Form, IMyView
{
    private MyPresenter _presenter;

    public void SetPresenter(MyPresenter presenter) => _presenter = presenter;
}
```

View はイベント・ViewAction 経由でだけ外と通信する ([MVP の鉄則 3](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) 参照)。

---

## 関連ページ

- [EventAggregator](Reference-EventAggregator) — pub/sub の API 詳細
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — 公開 API ガイドライン (ルール 16・17)
- [WindowNavigator](Reference-WindowNavigator) — Modal / Non-Modal の戻り値・コールバック
- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 3 (依存方向)
- サンプル:
  - `src/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` — Model ベース
  - `src/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/` — EventAggregator ベース
