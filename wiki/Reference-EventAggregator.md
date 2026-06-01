# EventAggregator

`EventAggregator` (`WinformsMVP.Common.EventAggregator.EventAggregator`) は、**Presenter 間の疎結合な pub/sub** のための高性能・スレッドセーフなメッセージング機構です。
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
- [ベストプラクティス](#ベストプラクティス)
- [パフォーマンス特性](#パフォーマンス特性)
- [テスト](#テスト)
- [関連ページ](#関連ページ)

---

## 主な特徴

| 特徴 | 説明 |
|------|----|
| **弱参照購読** | 購読者が GC されたら自動的にクリーンアップ。メモリリーク防止 |
| **UI スレッド整列** | バックグラウンドスレッドからの発行を自動的に UI スレッドへマーシャル (WinForms で重要) |
| **高性能** | Expression Tree でコンパイルしたデリゲートを使用 (リフレクションの 10〜100 倍) |
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

### Step 2: 共有 `EventAggregator` インスタンスを作る

```csharp
// UI スレッド上で作成する必要がある (SynchronizationContext をキャプチャするため)
// 通常は Program.Main や Composition Root で 1 つだけ作る
var eventAggregator = new EventAggregator();
```

### Step 3: Presenter A — メッセージを購読

```csharp
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IEventAggregator _eventAggregator;
    private IDisposable _productAddedSubscription;

    public OrderSummaryPresenter(IOrderSummaryView view, IEventAggregator eventAggregator)
        : base(view)
    {
        _eventAggregator = eventAggregator;
    }

    protected override void OnViewAttached()
    {
        _productAddedSubscription =
            _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
    }

    private void OnProductAdded(ProductAddedMessage msg)
    {
        // UI スレッド上で自動的に実行される
        View.AddItem(msg.Product, msg.Quantity);
    }

    protected override void Cleanup()
    {
        _productAddedSubscription?.Dispose();
    }
}
```

### Step 4: Presenter B — メッセージを発行

```csharp
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IEventAggregator _eventAggregator;

    public ProductSelectorPresenter(IProductSelectorView view, IEventAggregator eventAggregator)
        : base(view)
    {
        _eventAggregator = eventAggregator;
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

```csharp
// 高優先度の商品だけ受信
_eventAggregator.Subscribe<ProductAddedMessage>(
    msg => View.HighlightProduct(msg.Product),
    filter: msg => msg.Product.Priority > 5);

// 特定の顧客のメッセージだけ受信
_eventAggregator.Subscribe<OrderPlacedMessage>(
    msg => UpdateCustomerOrders(msg.OrderId),
    filter: msg => msg.CustomerId == _currentCustomerId);
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
_eventAggregator.Subscribe<DataLoadedMessage>(msg =>
{
    View.Data = msg.Data;   // ✅ UI コントロールに安全にアクセス可能
});
```

> **重要**: `EventAggregator` は構築時に `SynchronizationContext` をキャプチャするため、**UI スレッド上で構築する** 必要があります。

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
_eventAggregator.Subscribe<GetOrderSnapshotRequest>(request =>
{
    request.Snapshot = _orderItems.ToList();
});
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

## ベストプラクティス

### 1. EventAggregator を UI スレッドで作る

```csharp
// Program.Main または MainForm のコンストラクタ内で
var eventAggregator = new EventAggregator();   // ✅ UI スレッド
```

### 2. 必ず購読を Dispose する

```csharp
protected override void Cleanup()
{
    _subscription?.Dispose();   // ✅ 明示的にクリーンアップ
}
```

弱参照によって最終的には自動でクリーンアップされますが、`Dispose` で明示的に外す方が即座に効果があります。

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
| メモリ管理 | 弱参照でリーク防止、GC 時に自動クリーンアップ |

---

## テスト

```csharp
[Fact]
public void ProductSelector_PublishesMessage_OrderSummaryReceives()
{
    // Arrange
    var eventAggregator = new EventAggregator();
    var productSelectorView = new MockProductSelectorView();
    var orderSummaryView = new MockOrderSummaryView();

    var productSelectorPresenter = new ProductSelectorPresenter(
        productSelectorView, eventAggregator);
    var orderSummaryPresenter = new OrderSummaryPresenter(
        orderSummaryView, eventAggregator);

    productSelectorPresenter.AttachView(productSelectorView);
    productSelectorPresenter.Initialize();
    orderSummaryPresenter.AttachView(orderSummaryView);
    orderSummaryPresenter.Initialize();

    // Act
    productSelectorPresenter.AddToOrder();   // ProductAddedMessage を発行

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
  - `src/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/` — EventAggregator の完全例
  - `src/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` — Service ベース比較
  - `src/WinformsMVP.Samples.Tests/Common/EventAggregatorTests.cs` — ストレステスト含む網羅テスト
