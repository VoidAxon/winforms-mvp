# EventAggregator

`EventAggregator` (`WinformsMVP.Common.EventAggregator.EventAggregator`) は、**Presenter 間の疎結合な pub/sub** のためのスレッドセーフなメッセージング機構です。
複数の Presenter が同じイベントに反応する必要があるが、互いに直接参照を持たせたくない場面で使います。

> **位置づけ**: 親子の Presenter 間や、同一機能内の通信には **使うべきではありません**。それらは直接メソッド呼び出しや共有 Service で扱います。EventAggregator は「直接の関係を持たない複数 Presenter が共通の出来事に反応する」場合の専用ツールです。

---

## 目次

- [主な特徴](#主な特徴)
- [いつ使うか・使わないか](#いつ使うか-使わないか)
- [基本的な使い方](#基本的な使い方)
- [フィルタ付き購読](#フィルタ付き購読)
- [バックグラウンドスレッドからの発行](#バックグラウンドスレッドからの発行)
- [リクエスト/レスポンスパターン](#リクエストレスポンスパターン)
- [EventAggregator と他の選択肢](#eventaggregator-と他の選択肢)
- [購読の生存期間](#購読の生存期間)
- [ベストプラクティス](#ベストプラクティス)
- [パフォーマンス特性](#パフォーマンス特性)
- [テスト](#テスト)
- [関連ページ](#関連ページ)

---

## 主な特徴

| 特徴 | 説明 |
|------|----|
| **明示的な生存期間** | 購読は返された `IDisposable` トークンを Dispose するまで有効 (強参照)。ハンドラの形 (メソッドグループ・キャプチャするラムダ) を問わない |
| **UI スレッド整列** | バックグラウンドスレッドからの発行を自動的に UI スレッドへマーシャル (WinForms で重要) |
| **例外の分離** | 1 つの購読者の例外が他の購読者に波及しない |
| **スレッドセーフ** | 並行発行・並行購読をサポート |
| **フィルタ付き購読** | 条件にマッチするメッセージだけ受信できる |

---

## いつ使うか・使わないか

### 使うべき場面

- 複数の Presenter が同じイベントに反応する必要がある
- Presenter が UI 階層の別々の場所にある
- 親 Presenter に子 Presenter の参照を持たせたくない
- モジュール横断の通知が必要

### 使うべきでない場面

| 状況 | 代わりに使うべき手段 |
|------|----------------|
| 親子間の単純な調整 | 親が子のメソッドを直接呼ぶ |
| View → Presenter の通信 | View のイベント または ViewAction |
| 同一 Presenter 内 | 単なる private メソッド呼び出し |
| 共有ステート (注文・認証等) | 共有 Service が状態を持ち、Service からイベント発火 |

---

## 基本的な使い方

### Step 1: メッセージ型を定義する

```csharp
public class ProductAddedMessage
{
    public Product Product { get; init; }
    public int Quantity { get; init; }
}

public class OrderClearedMessage { }
```

### Step 2: 共有 `IEventAggregator` を受け取る

`IEventAggregator` はフレームワークが**シングルトンとして既定登録**します (`ServiceLocator` /
`MvpApplication.Configure` の経路でも、M.E.DI の `AddWinformsMVP` でも同じ)。自分で `new` する必要はありません。
コンストラクタ注入で受け取るか、サービスプロバイダから解決します。

```csharp
// M.E.DI / IPresenterFactory 経由ならコンストラクタ注入で自動的に渡される。
// DI を使わない場合はプロバイダから解決して渡す:
var events = ServiceLocator.Current.ResolveRequired<IEventAggregator>();
var presenter = new OrderSummaryPresenter(events);
```

- 各購読は **`Subscribe` を呼んだ時点のスレッドの `SynchronizationContext`** をキャプチャします
  (通常は UI スレッド上の Presenter)。アグリゲーター自体はどのスレッドで生成されても構いません。
- `EventAggregator` は `IDisposable` で、生成したサービスプロバイダが破棄時に Dispose します (アプリ終了時は `MvpApplication.Run(mainForm)` / `MvpApplication.Shutdown()`、M.E.DI ではコンテナの `Dispose`)
  (全購読を解除し、以後の `Publish` は何も配送せず、`Subscribe` は `ObjectDisposedException`)。
- 独自の実装に差し替える場合は起動時に `mvp.Events.Use(...)` を指定します
  ([フレームワークをカスタマイズする](HowTo-Customize-Framework))。

### Step 3: Presenter A — メッセージを購読

```csharp
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IEventAggregator _eventAggregator;

    public OrderSummaryPresenter(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    protected override void OnViewAttached()
    {
        // 生存期間を作成行で宣言 — Presenter の Dispose 時にフレームワークが自動解除する
        _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded)
            .DisposeWith(Disposables);
    }

    private void OnProductAdded(ProductAddedMessage msg)
    {
        // UI スレッド上で自動的に実行される
        View.AddItem(msg.Product, msg.Quantity);
    }

    // Cleanup の override は不要 — Disposables バッグが解除を肩代わりする
}
```

### Step 4: Presenter B — メッセージを発行

```csharp
public static class ProductActions
{
    public static readonly ViewAction AddToOrder = ViewAction.Create("Product.AddToOrder");
}

public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IEventAggregator _eventAggregator;

    public ProductSelectorPresenter(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(ProductActions.AddToOrder, OnAddToOrder);
    }

    private void OnAddToOrder()
    {
        var product = View.SelectedProduct;
        var quantity = View.Quantity;

        // 発行 — すべての購読者に通知される
        _eventAggregator.Publish(new ProductAddedMessage
        {
            Product = product,
            Quantity = quantity,
        });
    }
}
```

---

## フィルタ付き購読

条件にマッチするメッセージだけを受信できます。

ハンドラ・`filter` ともにメソッドグループでもキャプチャするラムダでも構いません。

```csharp
// 高優先度の商品だけ受信
_eventAggregator.Subscribe<ProductAddedMessage>(
        OnHighPriorityProductAdded,
        filter: msg => msg.Product.Priority > 5)
    .DisposeWith(Disposables);

// 特定の顧客のメッセージだけ受信
_eventAggregator.Subscribe<OrderPlacedMessage>(
        OnOrderPlaced,
        filter: msg => msg.CustomerId == _currentCustomerId)
    .DisposeWith(Disposables);
```

---

## バックグラウンドスレッドからの発行

バックグラウンドスレッドから発行されたメッセージは、**購読者ハンドラを UI スレッド上で実行** します。手動で `Invoke` を呼ぶ必要はありません。

```csharp
// バックグラウンドスレッド
Task.Run(() =>
{
    var data = LoadDataFromDatabase();
    _eventAggregator.Publish(new DataLoadedMessage { Data = data });
});

// 購読者 — UI スレッド上で実行される
_eventAggregator.Subscribe<DataLoadedMessage>(OnDataLoaded).DisposeWith(Disposables);

private void OnDataLoaded(DataLoadedMessage msg)
{
    View.Data = msg.Data;   // ✅ UI コントロールに安全にアクセス可能
}
```

> **重要**: マーシャリング先は、**購読 (`Subscribe`) した時点のスレッドの `SynchronizationContext`** です (アグリゲーターの構築時のものではありません)。Presenter が UI スレッドで購読していれば、どのスレッドから発行しても UI スレッドで実行されます。購読時にコンテキストが無かった場合のみ、ハンドラは発行スレッドで同期実行されます。
>
> UI スレッドへポスト済みのメッセージは、その間に Dispose された購読 (例: ウィンドウが閉じた) には配送されません。

---

## リクエスト/レスポンスパターン

問い合わせ系のメッセージにも使えます。

```csharp
// レスポンス用プロパティを持つリクエストメッセージ
public class GetOrderSnapshotRequest
{
    public IList<OrderItem> Snapshot { get; set; }
}

// 発行側 (要求者)
private List<OrderItem> GetCurrentOrderSnapshot()
{
    var request = new GetOrderSnapshotRequest();
    _eventAggregator.Publish(request);
    return request.Snapshot?.ToList() ?? new List<OrderItem>();
}

// 購読側 (応答者)
_eventAggregator.Subscribe<GetOrderSnapshotRequest>(OnGetOrderSnapshot).DisposeWith(Disposables);

private void OnGetOrderSnapshot(GetOrderSnapshotRequest request)
{
    request.Snapshot = _orderItems.ToList();
}
```

---

## EventAggregator と他の選択肢

| パターン | 結合度 | 用途 | 例 |
|---------|------|----|----|
| **public メソッド** | 直接参照 | 親子の調整 | `childPresenter.AddProduct()` |
| **共有 Service** | Service 参照 | 共有ステート | `orderService.AddProduct()` |
| **EventAggregator** | 結合なし | Presenter 横断のイベント | `eventAggregator.Publish(...)` |

### EventAggregator vs 共有 Service

```csharp
// ❌ ステート管理に EventAggregator を使う (誤用)
_eventAggregator.Publish(new AddProductMessage(product));   // 誰がステートを持ってる?
var snapshot = GetOrderSnapshot();                          // Request/Response で取り回す? 不自然

// ✅ ステート管理は共有 Service
_orderService.AddProduct(product);                          // Service がステートを所有
var items = _orderService.OrderItems;                       // 直接クエリ

// ✅ 通知には EventAggregator か Service のイベント
_orderService.ProductAdded += (s, e) => { };                // または:
_eventAggregator.Publish(new ProductAddedNotification(...));
```

**判断基準**: 「**ステートを所有する場所**」と「**通知の宛先**」を分けて考えます。
- ステートは Service が持つ
- 通知は Service のイベント (直接結合) または EventAggregator (疎結合)

---

## 購読の生存期間

`Subscribe` は購読トークン (`IDisposable`) を返します。EventAggregator は購読を **強参照** で保持し、**トークンを Dispose するまで** ハンドラ (とそれがキャプチャするオブジェクト) を生かし続けます。GC による自動解除はありません。

- ハンドラの形は自由 — インスタンスメソッド、`this` やローカル変数をキャプチャするラムダ、キャプチャする `filter`、いずれも GC で失効しない
- Dispose すると以後は配送されない (UI スレッドへポスト済みで未実行のメッセージも配送されない)
- **トークンを Dispose しなければ**、ハンドラとそれがキャプチャする Presenter は EventAggregator の生存期間中ずっと残る (= 閉じたウィンドウの Presenter がメッセージを受け続けるリーク)

推奨パターンは、作成行でトークンを Presenter の `Disposables` バッグに登録することです。Presenter の Dispose 時 (`Cleanup()` の後) にフレームワークが自動解除します。

```csharp
protected override void OnViewAttached()
{
    var customerId = View.CustomerId;

    // ✅ キャプチャするラムダでも問題ない — 生存期間はトークンで決まる
    _eventAggregator.Subscribe<OrderPlacedMessage>(
            msg => View.ShowOrder(msg.OrderId),
            filter: msg => msg.CustomerId == customerId)
        .DisposeWith(Disposables);
}

// ❌ トークンを捨てている — 二度と解除できない (MVP017 警告)
_eventAggregator.Subscribe<OrderPlacedMessage>(OnOrderPlaced);
```

### アナライザ MVP017

`IEventAggregator.Subscribe(...)` の戻り値を単独の式文として捨てると、アナライザ **`MVP017`** が警告します。次の書き方は受理されます:

| 書き方 | 用途 |
|------|----|
| `.DisposeWith(Disposables)` を連結 | 推奨 |
| フィールド/変数に格納 (`_subscription = ...`) | 手動管理 (`Cleanup()` で `Dispose`) |
| `using (events.Subscribe(...)) { ... }` / `using var` | スコープ限定の購読 |
| `_ = events.Subscribe(...)` | アプリ全体の生存期間で意図的に解除しない場合 (明示的な破棄) |

詳細は [Design Rules — 自動検証](Design-Rules#自動検証-roslyn-analyzer) を参照。

---

## ベストプラクティス

### 1. 既定登録の 1 つを共有し、UI スレッドで購読する

```csharp
// ✅ フレームワークが登録したシングルトンを使う (コンストラクタ注入 or 解決)
public OrderSummaryPresenter(IEventAggregator events) { _events = events; }
```

Presenter ごとに `new` すると発行側と購読側が別インスタンスになり、メッセージが届きません。
購読は UI スレッド (Presenter の `OnViewAttached` / `OnInitialize` など) で行ってください。マーシャリング先は購読時のコンテキストで決まります。

### 2. 必ず購読を Dispose する

```csharp
// Subscribe の戻り値を作成行で Disposables バッグに登録する — これだけで
// Presenter の Dispose 時にフレームワークが自動解除する
_eventAggregator.Subscribe<OrderShipped>(OnShipped).DisposeWith(Disposables);
```

購読は Dispose されるまで解除されません ([購読の生存期間](#購読の生存期間))。
バッグを使わず手で管理する場合は `Cleanup()` で `_subscription?.Dispose()` しても同じです
(詳細は [購読ライフサイクルの管理](HowTo-Manage-Presenter-Subscriptions))。

### 3. 強い型のメッセージクラスを使う

```csharp
// ✅ Good — 専用のメッセージ型
public class ProductAddedMessage { ... }

// ❌ Bad — 汎用辞書
_eventAggregator.Publish(new Dictionary<string, object> { ... });
```

### 4. メッセージの命名規約

| 種類 | サフィックス | 例 |
|------|-----------|----|
| アクション通知 | `Message` | `ProductAddedMessage` |
| イベント通知 | `Notification` | `OrderClearedNotification` |
| データ通知 | `Message` または `Loaded` | `DataLoadedMessage` |
| 要求/問い合わせ | `Request` | `GetOrderSnapshotRequest` |

### 5. メッセージは不変にする

```csharp
public class ProductAddedMessage
{
    public Product Product { get; init; }   // ✅ init only
    public int Quantity { get; init; }
}
```

C# 8.0 以下なら `get; private set;` か `readonly` フィールドを使います。

### 6. 過度に使わない

EventAggregator は間接層を追加するため、必要な場面でだけ使います。

- 親子間 → public メソッド
- 共有ステート → 共有 Service
- 横断的関心事 → EventAggregator

---

## パフォーマンス特性

ストレステスト (`EventAggregatorTests.cs`) に基づく実測値:

| 観点 | 数値 |
|------|------|
| 10,000 購読者で 1 メッセージ発行 | 約 1 秒 |
| 100,000 メッセージ発行 (購読者 1 つ) | 約 16 ms |
| スループット | 50,000 メッセージ/秒以上 |
| スレッドセーフ性 | 並行 Subscribe/Publish サポート |
| メモリ管理 | 購読はトークンの Dispose まで強参照で保持 (Dispose 忘れはリーク) |

---

## テスト

```csharp
[Fact]
public void ProductSelector_PublishesMessage_OrderSummaryReceives()
{
    // Arrange — テストではテストごとに新しいインスタンスを使う
    var eventAggregator = new EventAggregator();
    var productSelectorView = new MockProductSelectorView();
    var orderSummaryView = new MockOrderSummaryView();

    var productSelectorPresenter = new ProductSelectorPresenter(eventAggregator);
    var orderSummaryPresenter = new OrderSummaryPresenter(eventAggregator);

    // PresenterTestHost (WinformsMVP.Testing) が View のアタッチと初期化を行う
    PresenterTestHost.Start(productSelectorPresenter, productSelectorView);
    PresenterTestHost.Start(orderSummaryPresenter, orderSummaryView);

    // Act — モック View の CapturingActionBinder (WinformsMVP.Testing) 経由で実際のアクションを Dispatch
    productSelectorView.SelectedProduct = new Product { Name = "Laptop" };
    productSelectorView.Quantity = 1;
    productSelectorView.Binder.Dispatch(ProductActions.AddToOrder);   // ProductAddedMessage を発行

    // Assert
    Assert.Equal(1, orderSummaryView.Items.Count);
    Assert.Equal("Laptop", orderSummaryView.Items[0].Product.Name);
}
```

---

## 関連ページ

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — Presenter のライフサイクルと `Cleanup` フック
- [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — 4 つの選択肢の使い分け
- サンプル:
  - `samples/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/` — EventAggregator の完全例
  - `samples/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` — Service ベース比較
  - `tests/WinformsMVP.Samples.Tests/Common/EventAggregatorTests.cs` — ストレステスト含む網羅テスト
