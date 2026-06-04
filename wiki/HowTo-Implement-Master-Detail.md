# HowTo: マスター/詳細パターンを実装する

このページでは、親 (マスター) - 子 (詳細) のデータ連動を持つ画面の典型パターンを示します。
例:**顧客リスト ↔ 注文明細**、**フォルダツリー ↔ ファイル一覧**、**カテゴリ ↔ 商品**。

---

## 目次

- [全体構成](#全体構成)
- [シナリオ 1: 単一 Presenter で両方を扱う](#シナリオ-1-単一-presenter-で両方を扱う)
- [シナリオ 2: 親子 Presenter に分割する](#シナリオ-2-親子-presenter-に分割する)
- [シナリオ 3: 階層 N 段の構成](#シナリオ-3-階層-n-段の構成)
- [State-driven CanExecute](#state-driven-canexecute)
- [カスケード削除](#カスケード削除)
- [パフォーマンス上の注意](#パフォーマンス上の注意)
- [テストパターン](#テストパターン)
- [関連ページ](#関連ページ)

---

## 全体構成

「マスター側で選択 → 詳細側に対応するデータが表示される」が基本動作。
構成の選び方は次の質問で決めます。

| 質問 | はい | いいえ |
|------|----|----|
| Q1: マスターと詳細は **同じウィンドウ内** にある? | → シナリオ 1 か 2 | → 別ウィンドウ + Navigator |
| Q2: マスターと詳細は **独立して再利用される** ことがある? | → シナリオ 2 (分割) | → シナリオ 1 (単一) |
| Q3: 階層が **3 段以上** ある (例: カテゴリ → サブカテゴリ → 商品)? | → シナリオ 3 | → 1 か 2 |

---

## シナリオ 1: 単一 Presenter で両方を扱う

シンプルな構成。マスターと詳細が同じ View 上にあり、別画面で再利用しないなら、**1 つの Presenter で十分**。

### View インターフェイス

```csharp
public interface ICustomerOrderView : IWindowView
{
    IReadOnlyList<Customer> Customers { get; set; }
    Customer SelectedCustomer { get; set; }
    IReadOnlyList<Order> Orders { get; set; }      // 詳細 — 選択された顧客の注文
    decimal TotalAmount { get; set; }

    bool HasSelectedCustomer { get; }

    ViewActionBinder ActionBinder { get; }
    event EventHandler SelectedCustomerChanged;
}
```

### Presenter

```csharp
public class CustomerOrderPresenter : WindowPresenterBase<ICustomerOrderView>
{
    private readonly ICustomerRepository _customerRepo;
    private readonly IOrderRepository _orderRepo;

    public CustomerOrderPresenter(ICustomerRepository customerRepo, IOrderRepository orderRepo)
    {
        _customerRepo = customerRepo;
        _orderRepo    = orderRepo;
    }

    protected override void OnViewAttached()
    {
        View.SelectedCustomerChanged += OnSelectedCustomerChanged;
    }

    protected override void OnInitialize()
    {
        View.Customers = _customerRepo.GetAll();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(OrderActions.AddOrder, OnAddOrder,
            canExecute: () => View.HasSelectedCustomer);
        Dispatcher.Register(OrderActions.DeleteOrder, OnDeleteOrder,
            canExecute: () => View.HasSelectedCustomer && View.Orders?.Count > 0);
    }

    // ── マスター選択変更 → 詳細を更新 ──────────────────────────
    private void OnSelectedCustomerChanged(object sender, EventArgs e)
    {
        var customer = View.SelectedCustomer;
        if (customer == null)
        {
            View.Orders = Array.Empty<Order>();
            View.TotalAmount = 0;
        }
        else
        {
            View.Orders = _orderRepo.GetByCustomer(customer.Id);
            View.TotalAmount = View.Orders.Sum(o => o.Amount);
        }

        Dispatcher.RaiseCanExecuteChanged();   // ← マスターの選択変化で CanExecute も再評価
    }

    // ── 詳細側の操作 ─────────────────────────────────────────
    private void OnAddOrder()
    {
        // ... 新規注文を作成して View.Orders を更新 ...
        RefreshDetail();
    }

    private void OnDeleteOrder()
    {
        // ... 選択された注文を削除 ...
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        var customer = View.SelectedCustomer;
        if (customer == null) return;

        View.Orders = _orderRepo.GetByCustomer(customer.Id);
        View.TotalAmount = View.Orders.Sum(o => o.Amount);
    }
}
```

---

## シナリオ 2: 親子 Presenter に分割する

マスターと詳細を **独立した UserControl** に分けて、それぞれに Presenter を持たせる構成。再利用性が高い。
両者の連携は [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) の **共有 Service** パターンを使います。

### 共有 Service

```csharp
public interface ICustomerSelectionService
{
    Customer SelectedCustomer { get; }
    void SetSelectedCustomer(Customer customer);
    event EventHandler SelectedCustomerChanged;
}

public class CustomerSelectionService : ICustomerSelectionService
{
    private Customer _selected;

    public Customer SelectedCustomer => _selected;

    public void SetSelectedCustomer(Customer customer)
    {
        if (Equals(_selected, customer)) return;
        _selected = customer;
        SelectedCustomerChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler SelectedCustomerChanged;
}
```

### マスター Presenter (顧客リスト側)

```csharp
public class CustomerListPresenter : ControlPresenterBase<ICustomerListView>
{
    private readonly ICustomerRepository _repo;
    private readonly ICustomerSelectionService _selection;

    public CustomerListPresenter(
        ICustomerListView view,
        ICustomerRepository repo,
        ICustomerSelectionService selection) : base(view)
    {
        _repo = repo;
        _selection = selection;
    }

    protected override void OnViewAttached()
    {
        View.SelectionChanged += (s, e) =>
            _selection.SetSelectedCustomer(View.SelectedCustomer);
    }

    protected override void OnInitialize()
    {
        View.Customers = _repo.GetAll();
    }
}
```

### 詳細 Presenter (注文一覧側)

```csharp
public class OrderListPresenter : ControlPresenterBase<IOrderListView>
{
    private readonly IOrderRepository _repo;
    private readonly ICustomerSelectionService _selection;

    public OrderListPresenter(
        IOrderListView view,
        IOrderRepository repo,
        ICustomerSelectionService selection) : base(view)
    {
        _repo = repo;
        _selection = selection;
    }

    protected override void OnViewAttached()
    {
        _selection.SelectedCustomerChanged += OnSelectedCustomerChanged;
    }

    private void OnSelectedCustomerChanged(object sender, EventArgs e)
    {
        var customer = _selection.SelectedCustomer;
        View.Orders = customer != null
            ? _repo.GetByCustomer(customer.Id)
            : Array.Empty<Order>();
        View.Total = View.Orders.Sum(o => o.Amount);
    }

    protected override void Cleanup()
    {
        _selection.SelectedCustomerChanged -= OnSelectedCustomerChanged;
    }
}
```

### メリット

✅ マスターと詳細が独立してテスト可能
✅ 別画面で再利用しやすい
✅ Service がステートを所有 → 単一情報源

詳しくは [HowTo: Presenter 間の通信方法 § 共有 Service](HowTo-Communicate-Between-Presenters#選択肢-2-共有-service--イベント) を参照。

---

## シナリオ 3: 階層 N 段の構成

3 段以上 (カテゴリ → サブカテゴリ → 商品) は、選択 Service を **各レベルごと** に用意します。

```csharp
public interface ICategorySelectionService    { /* ... */ }
public interface ISubCategorySelectionService { /* ... */ }
public interface IProductSelectionService     { /* ... */ }
```

各レベルの Presenter は、**1 段上の選択変更を購読** → 自分のデータを更新 → 自分の選択 Service に通知、というチェーンになります。

```
CategoryListPresenter
   │ 選択変更
   ▼
ICategorySelectionService.SelectedCategoryChanged
   │ subscribed by
   ▼
SubCategoryListPresenter
   │ 新リスト読み込み + 自分の選択をクリア
   ▼
ISubCategorySelectionService.SelectedSubCategoryChanged
   │ subscribed by
   ▼
ProductListPresenter
   │ 新リスト読み込み
   ▼
View.Products = ...
```

---

## State-driven CanExecute

マスター/詳細では「マスターを選んでいなければ詳細の操作は無効」のパターンが頻出。`CanExecute` で表現します。

```csharp
Dispatcher.Register(OrderActions.AddOrder, OnAddOrder,
    canExecute: () => View.HasSelectedCustomer);

Dispatcher.Register(OrderActions.DeleteOrder, OnDeleteOrder,
    canExecute: () => View.HasSelectedCustomer && View.Orders?.Count > 0);
```

選択が変わったら `Dispatcher.RaiseCanExecuteChanged()` を必ず呼ぶ:

```csharp
private void OnSelectedCustomerChanged(object sender, EventArgs e)
{
    // ... 詳細更新 ...
    Dispatcher.RaiseCanExecuteChanged();
}
```

---

## カスケード削除

「親を削除するときに、紐付く子をどう扱うか」の問題。

### パターン A: 確認してから親子両方を削除

```csharp
private void OnDeleteCustomer()
{
    var customer = View.SelectedCustomer;
    var orderCount = _orderRepo.CountByCustomer(customer.Id);

    if (orderCount > 0)
    {
        var msg = $"This will also delete {orderCount} orders. Continue?";
        if (!Messages.ConfirmYesNo(msg, "Confirm Delete"))
            return;
    }
    else
    {
        if (!Messages.ConfirmYesNo("Delete this customer?", "Confirm Delete"))
            return;
    }

    _orderRepo.DeleteByCustomer(customer.Id);
    _customerRepo.Delete(customer.Id);

    View.Customers = _customerRepo.GetAll();
    Messages.ShowInfo("Customer deleted.", "Success");
}
```

### パターン B: 子があるなら親の削除を禁止

```csharp
private void OnDeleteCustomer()
{
    var customer = View.SelectedCustomer;
    if (_orderRepo.CountByCustomer(customer.Id) > 0)
    {
        Messages.ShowWarning(
            "Cannot delete customer with existing orders. Delete orders first.",
            "Delete Blocked");
        return;
    }

    if (!Messages.ConfirmYesNo("Delete this customer?", "Confirm Delete"))
        return;

    _customerRepo.Delete(customer.Id);
    View.Customers = _customerRepo.GetAll();
}
```

業務要件に応じてどちらか選んでください。

---

## パフォーマンス上の注意

マスター選択ごとに DB クエリが走るので、大量データには注意。

| 課題 | 対策 |
|------|----|
| 詳細データが大量 | ページング / 仮想スクロール |
| マスター切り替えが頻繁 | キャッシュ (LRU など) |
| マスター選択時の DB レイテンシが見える | async + ローディングインジケータ |

### async 化の例

```csharp
private async void OnSelectedCustomerChanged(object sender, EventArgs e)
{
    var customer = View.SelectedCustomer;
    if (customer == null)
    {
        View.Orders = Array.Empty<Order>();
        return;
    }

    View.IsLoadingDetail = true;
    try
    {
        View.Orders = await _orderRepo.GetByCustomerAsync(customer.Id);
        View.TotalAmount = View.Orders.Sum(o => o.Amount);
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to load orders: {ex.Message}", "Error");
    }
    finally
    {
        View.IsLoadingDetail = false;
    }
}
```

詳細は [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) 参照。

---

## テストパターン

```csharp
[Fact]
public void SelectingCustomer_LoadsOrders()
{
    var customer = new Customer { Id = 1, Name = "Alice" };
    _platform.OrderRepository.SetupGetByCustomer(1,
        new[] { new Order { Amount = 100 }, new Order { Amount = 200 } });

    _view.SelectedCustomer = customer;
    _view.RaiseSelectedCustomerChanged();

    Assert.Equal(2, _view.Orders.Count);
    Assert.Equal(300m, _view.TotalAmount);
}

[Fact]
public void CanDelete_WhenNoCustomerSelected_IsFalse()
{
    _view.SelectedCustomer = null;
    _view.RaiseSelectedCustomerChanged();

    Assert.False(_presenter.Dispatcher.CanDispatch(OrderActions.DeleteOrder));
}

[Fact]
public void DeletingCustomer_WithOrders_ConfirmsBeforeDelete()
{
    _view.SelectedCustomer = new Customer { Id = 1 };
    _platform.OrderRepository.SetupCountByCustomer(1, 5);
    _platform.MessageService.ConfirmYesNoResult = false;   // Cancel

    _presenter.Dispatcher.Dispatch(CustomerActions.Delete);

    Assert.True(_platform.MessageService.ConfirmDialogShown);
    Assert.False(_platform.CustomerRepository.DeleteCalled);
}
```

---

## 関連ページ

- [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — 共有 Service パターン
- [ViewAction システム](Reference-ViewAction-System) — State-driven CanExecute
- [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) — 詳細読み込みの async 化
- サンプル:
  - `samples/WinformsMVP.Samples/MasterDetailDemo/` — 顧客 → 注文の完全例
