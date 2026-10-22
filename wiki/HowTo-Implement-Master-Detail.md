# HowTo: マスター/詳細パターンを実装する

このページでは、親 (マスター) - 子 (詳細) のデータ連動を持つ画面の典型パターンを示します。
例:**顧客リスト ↔ 注文明細**、**フォルダツリー ↔ ファイル一覧**、**カテゴリ ↔ 商品**。

---

## 目次

- [全体構成](#全体構成)
- [シナリオ 1: 単一 Presenter で両方を扱う](#シナリオ-1-単一-presenter-で両方を扱う)
- [シナリオ 2: 親子 Presenter に分割する](#シナリオ-2-親子-presenter-に分割する)
  - [組み立て (親 Presenter が子を所有する)](#組み立て-親-presenter-が子を所有する)
  - [誰が子 Presenter を所有するか](#誰が子-presenter-を所有するか)
- [シナリオ 3: 階層 N 段の構成](#シナリオ-3-階層-n-段の構成)
- [動的なパネル (行の追加・削除、タブ)](#動的なパネル-行の追加削除タブ)
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

    // ActionBinder は IWindowView (→ IActionableView) から継承するので再宣言しない。
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
両者の連携は [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) の **「共有 Model + イベント」** パターン（状態の単一所有者を 1 か所に置く方式）を使います。ここでは選択状態を持つ **Service** として実装します（状態保持者は Model でも Service でもよい）。

### 共有 Service (選択状態の保持者)

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
        ICustomerRepository repo,
        ICustomerSelectionService selection)
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
        IOrderRepository repo,
        ICustomerSelectionService selection)
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

### 組み立て (親 Presenter が子を所有する)

子 Presenter の生成とアタッチは **親 Presenter** が `AttachChild` で行います。親 View インターフェイスは子の **View インターフェイス** を公開し (コントロールは公開しない — Rule 2 のまま)、子の依存は親のコンストラクタから渡します。`WindowNavigator` が生成する親 Form はパラメータなしのコンストラクタのままで済み、ファクトリ登録は要りません。

```csharp
public interface ICustomerOrderShellView : IWindowView
{
    ICustomerListView CustomerList { get; }   // 子の View インターフェイス (UserControl 型ではない)
    IOrderListView OrderList { get; }
}

public class CustomerOrderShellPresenter : WindowPresenterBase<ICustomerOrderShellView>
{
    private readonly ICustomerRepository _customerRepo;
    private readonly IOrderRepository _orderRepo;

    public CustomerOrderShellPresenter(ICustomerRepository customerRepo, IOrderRepository orderRepo)
    {
        _customerRepo = customerRepo;
        _orderRepo    = orderRepo;
    }

    protected override void OnViewAttached() { }

    protected override void OnInitialize()
    {
        // この画面だけの選択状態。両方の子に同じインスタンスを渡す
        var selection = new CustomerSelectionService();
        AttachChild(new CustomerListPresenter(_customerRepo, selection), View.CustomerList);
        AttachChild(new OrderListPresenter(_orderRepo, selection), View.OrderList);
        // 固定パネルなので返り値のハンドルは無視: 親の破棄時に逆順で解放される
    }
}
```

```csharp
public partial class CustomerOrderShellForm : Form, ICustomerOrderShellView
{
    public CustomerOrderShellForm() => InitializeComponent();   // 依存なし: Navigator がそのまま生成できる

    public IViewActionBinder ActionBinder => null;   // この Form 自身はアクションを持たない

    // デザイナー上の UserControl を子の View インターフェイスとして返す (明示的実装なので WFO1000 も出ない)
    ICustomerListView ICustomerOrderShellView.CustomerList => _customerListControl;
    IOrderListView ICustomerOrderShellView.OrderList => _orderListControl;
}
```

- **呼ぶ場所は `OnInitialize`** (パラメータが使える。`OnViewAttached` では早すぎる)。子は `AttachChild` から戻る前に初期化され、ウィンドウ表示前に動いています。
- 子は親のフレームワークサービス (`Messages` 等) を引き継ぎます。`IPresenterFactory` で生成した子は、ファクトリが結び付けたサービスを保ちます。
- **M.E.DI では Presenter ごとにスコープが別** です。親子で共有されるのは **渡したもの** (上の `selection`、入力 Model) だけで、スコープ登録したサービスを子それぞれに注入すると別インスタンスになります。共有したい状態は親から明示的に渡してください。
- 子は **ウィンドウのクローズ判定に参加しません**。未保存チェックは親が `CanClose` / `CanCloseAsync` で共有入力に対して行います。検証も各 Presenter が自分の `Validation` を持ち、保存に必要な規則は Model か親に置きます。
- 破棄の順序とすべての契約は [Presenter 基底クラス § 子 Presenter を所有する](Reference-Presenter-Base-Classes#子-presenter-を所有する-attachchild) を参照。

### 誰が子 Presenter を所有するか

所有者は **1 つ** です。同じ Presenter を `AttachChild` と `Connect` の両方で扱うことはできません (2 回目は例外)。

| 子をホストするもの | 使う API | 典型例 |
|---|---|---|
| **MVP の親 Presenter** | 親 Presenter の `AttachChild(new ChildPresenter(deps), View.Child[, input])` | Navigator が開く画面のパネル、依存 (Repository 等) を持つパネル、明細行・タブ |
| **WinForms 側** | `childPresenter.Connect(control[, param])` | レガシー Form (親 Presenter がない)、依存を持たない単体の再利用コントロール |

`Connect` の例 (親 Presenter のない Form):

```csharp
public partial class LegacySettingsForm : Form
{
    public LegacySettingsForm()
    {
        InitializeComponent();
        new SettingsPanelPresenter().Connect(_settingsPanel);   // コントロールの破棄と一緒に Presenter も破棄
    }
}
```

> **移行メモ (ファクトリ登録)**: 以前は「親 Form が子 Presenter を作って `Connect` する」構成のために、View マッピングをファクトリ付きで登録し直して Form に依存 (`Func<ChildPresenter>` 等) を渡していました (`register.Register<IOrderEditView>(() => new OrderEditForm(...), allowOverride: true)`)。依存の手配線・二重登録・組み立てが親 Presenter のテストに乗らない、という欠点があるので、`AttachChild` に置き換えてください: Form のコンストラクタ引数と `Connect` 呼び出しを削除 → 親 View インターフェイスに子 View を追加 → 親 Presenter のコンストラクタで依存を受け取り `OnInitialize` で `AttachChild` → ファクトリ登録を削除 (自動スキャン / 通常の `Register` に戻す)。

### メリット

✅ マスターと詳細が独立してテスト可能
✅ 別画面で再利用しやすい
✅ Service がステートを所有 → 単一情報源
✅ 組み立て (どの子に何を渡すか) が親 Presenter にあり、親のテストでカバーできる

詳しくは [HowTo: Presenter 間の通信方法 § 共有 Model + イベント](HowTo-Communicate-Between-Presenters#共有-model--イベント) を参照。

---

## シナリオ 3: 階層 N 段の構成

3 段以上 (カテゴリ → サブカテゴリ → 商品、国 → 都道府県 → 市区町村 等) の連鎖選択は、専用ページ **[連鎖選択 (カスケード) を扱う](HowTo-Handle-Cascading-Selection)** を参照してください。

各レベルに手書きの選択 Service を N 個用意する素朴な実装は、(1) 同型コードの重複と (2) 上位変更時の「下位選択クリア忘れ」という 2 つの罠を抱えます。汎用の `ISelection<T>` + `Cascade.Bind` を使えば、N 個の Service を 1 つのジェネリックに、各レベルの「上位購読 → 自分をクリア → 再読込」を 1 行に畳み、下位クリアを通知連鎖で自動化できます (多親依存は `Cascade.Combine`)。

---

## 動的なパネル (行の追加・削除、タブ)

注文明細の行パネルやドキュメントのタブのように、ウィンドウを開いたまま **子パネルを追加・削除する** 画面も同じ `AttachChild` で組み立てます。違いは、返り値のハンドルを保持して削除時に `Dispose` することだけです。キー・削除ポリシー・Model との同期は画面ごとに異なるので、フレームワークのコレクションは提供せず、アプリのコードで書きます。

### Model と子 Presenter

```csharp
public sealed class OrderLine
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class OrderEditInput
{
    public List<OrderLine> Lines { get; } = new List<OrderLine>();
}

public static class OrderLineActions
{
    private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("OrderLine");
    public static readonly ViewAction<int> ChangeQuantity = Factory.Create<int>(nameof(ChangeQuantity));
}

public interface IOrderLineView : IActionableView
{
    IReadOnlyList<Product> Products { set; }
    int Quantity { set; }
}

public class OrderLinePresenter : ControlPresenterBase<IOrderLineView, OrderLine>
{
    private readonly IProductRepository _products;
    private OrderLine _line;

    public OrderLinePresenter(IProductRepository products) => _products = products;

    protected override void OnViewAttached() { }

    protected override void RegisterViewActions()
        => Dispatcher.Register(OrderLineActions.ChangeQuantity, (int quantity) => _line.Quantity = quantity);

    protected override void OnInitialize(OrderLine line)
    {
        _line = line;   // 親と共有する入力 Model の 1 行
        View.Products = _products.GetAll();
        View.Quantity = line.Quantity;
    }
}
```

### 親 View インターフェイス

親 View は「子 View を作って返す」「取り除く」メソッドと、要求イベントを公開します。**View は自分の判断で子を取り除きません** — タブの × や行の削除ボタンはイベントを上げるだけで、取り除くかどうかは Presenter が決めます。

```csharp
public interface IOrderEditView : IWindowView
{
    IOrderLineView AddLine();                   // 行パネルを作って配置し、その View を返す
    void RemoveLine(IOrderLineView line);       // 行パネルを取り除く (コントロールの破棄も View 側)

    event EventHandler AddLineRequested;
    event EventHandler<IOrderLineView> RemoveLineRequested;   // 行の × ボタン
}
```

### 親 Presenter

```csharp
public class OrderEditPresenter : WindowPresenterBase<IOrderEditView, OrderEditInput>
{
    private readonly IProductRepository _products;
    // キーは子 View (推奨: 常に別インスタンス)。値は Model の行と解放用ハンドル
    private readonly Dictionary<IOrderLineView, (OrderLine Line, IDisposable Handle)> _lines
        = new Dictionary<IOrderLineView, (OrderLine, IDisposable)>();
    private OrderEditInput _input;

    public OrderEditPresenter(IProductRepository products) => _products = products;

    protected override void OnViewAttached()
    {
        SubscribeViewEvent(h => View.AddLineRequested += h, h => View.AddLineRequested -= h, OnAddLineRequested);
        SubscribeViewEvent<IOrderLineView>(h => View.RemoveLineRequested += h, h => View.RemoveLineRequested -= h,
                                           OnRemoveLineRequested);
    }

    protected override void OnInitialize(OrderEditInput input)
    {
        _input = input;
        foreach (var line in input.Lines) AttachLine(line);   // 既存の行
    }

    private void OnAddLineRequested()
    {
        var line = new OrderLine();
        _input.Lines.Add(line);
        try { AttachLine(line); }
        catch { _input.Lines.Remove(line); throw; }   // アタッチ失敗: Model の変更を戻す
    }

    private void AttachLine(OrderLine line)
    {
        var view = View.AddLine();
        try { _lines[view] = (line, AttachChild(new OrderLinePresenter(_products), view, line)); }
        catch { View.RemoveLine(view); throw; }       // アタッチ失敗: 追加した View を取り除く
    }

    private void OnRemoveLineRequested(IOrderLineView view)
    {
        if (!_lines.TryGetValue(view, out var entry)) return;

        // 1. 失敗し得ること (確認・保存・削除) を先に。ここで中断しても何も変わっていない
        if (!Messages.ConfirmYesNo("この行を削除しますか?")) return;

        // 2. Model の変更を確定
        _input.Lines.Remove(entry.Line);
        _lines.Remove(view);

        // 3. 子を解放してから View を取り除く (Cleanup が例外を投げても View は必ず取り除く)
        try { entry.Handle.Dispose(); }
        finally { View.RemoveLine(view); }
    }
}
```

ポイント:

- **キー**: 子 View (推奨)、または Model の行オブジェクト (参照で比較 — 新規行には ID がない)、安定した ID。**インデックスは使わない** (削除でずれる)。内容の等価比較も使わない。
- **削除の順序**: 失敗し得ること (確認・保存・DB 削除) → Model の変更を確定 → `handle.Dispose()` を `try`、`View.RemoveLine(view)` を `finally`。
- **アタッチ失敗の補償**: `AttachChild` が例外を投げると子は破棄済み (View には触れない) なので、呼び出し側が追加した View を取り除き、Model の変更を戻します。
- ハンドルの `Dispose` は 2 回目以降何もしません。ウィンドウを閉じたときに残っている行は、親の破棄時に逆順で解放されるので、`Cleanup` で全行を手で解放する必要はありません。
- 解放された子の Dispatcher は停止するので、取り除く途中のコントロールが後からクリックされてもハンドラは走りません。
- **安全網**: Form が自分で行のコントロールを破棄してしまった場合も、その子 Presenter は同じ経路で解放されます (確認も Model 変更も `RemoveLine` もなし)。正規の経路は上のイベント → Presenter です。

### Form 側の実装例

```csharp
public partial class OrderEditForm : Form, IOrderEditView
{
    public event EventHandler AddLineRequested;
    public event EventHandler<IOrderLineView> RemoveLineRequested;

    public OrderEditForm()
    {
        InitializeComponent();
        _addLineButton.Click += (s, e) => AddLineRequested?.Invoke(this, EventArgs.Empty);
    }

    public IViewActionBinder ActionBinder => null;

    IOrderLineView IOrderEditView.AddLine()
    {
        var control = new OrderLineControl();   // IOrderLineView を実装する UserControl
        control.RemoveClicked += (s, e) => RemoveLineRequested?.Invoke(this, control);   // 取り除かず、要求するだけ
        _linesPanel.Controls.Add(control);
        return control;
    }

    void IOrderEditView.RemoveLine(IOrderLineView line)
    {
        var control = (OrderLineControl)line;
        _linesPanel.Controls.Remove(control);
        control.Dispose();
    }
}
```

テストは親だけを `PresenterTestHost` で起動し、フェイクの親 View が返すフェイクの行 View で子を操作します — [HowTo: Presenter をテストする § 子 Presenter を含む画面のテスト](HowTo-Test-A-Presenter#子-presenter-を含む画面のテスト)。

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

「親を削除するときに、紐付く子をどう扱うか」の問題。削除コマンドは他の操作と同様 `RegisterViewActions` で登録しておきます:

```csharp
Dispatcher.Register(CustomerActions.Delete, OnDeleteCustomer,
    canExecute: () => View.HasSelectedCustomer);
```

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

View イベントのハンドラは `async void` にせず、`async Task` メソッドを開始するだけにします。例外はすべてそのメソッド内で処理し、トークンには Presenter の `Lifetime` (ウィンドウを閉じるとキャンセル) を渡します。

```csharp
private void OnSelectedCustomerChanged(object sender, EventArgs e)
    => _ = LoadOrdersAsync(View.SelectedCustomer);   // 例外は LoadOrdersAsync 内で処理済み

private async Task LoadOrdersAsync(Customer customer)
{
    if (customer == null)
    {
        View.Orders = Array.Empty<Order>();
        return;
    }

    View.IsLoadingDetail = true;
    try
    {
        var orders = await _orderRepo.GetByCustomerAsync(customer.Id, Lifetime);
        if (View.SelectedCustomer != customer) return;   // 待っている間に選択が変わった: 古い結果は捨てる
        View.Orders = orders;
        View.TotalAmount = orders.Sum(o => o.Amount);
    }
    catch (OperationCanceledException) when (Lifetime.IsCancellationRequested)
    {
        // ウィンドウが閉じられた: 何もしない
    }
    catch (Exception ex)   // 実コードでは具体的な例外型 (DbException 等) を先に捕捉する
    {
        Messages.ShowError($"Failed to load orders: {ex.Message}", "Error");
    }
    finally
    {
        if (!Lifetime.IsCancellationRequested) View.IsLoadingDetail = false;
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

    Assert.False(_view.Binder.CanDispatch(OrderActions.DeleteOrder));
}

[Fact]
public void DeletingCustomer_WithOrders_ConfirmsBeforeDelete()
{
    _view.SelectedCustomer = new Customer { Id = 1 };
    _platform.OrderRepository.SetupCountByCustomer(1, 5);
    _platform.MessageService.ConfirmYesNoResult = false;   // Cancel

    _view.Binder.Dispatch(CustomerActions.Delete);

    Assert.True(_platform.MessageService.ConfirmDialogShown);
    Assert.False(_platform.CustomerRepository.DeleteCalled);
}
```

---

## 関連ページ

- [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — 共有 Model + イベント パターン
- [Presenter 基底クラス § 子 Presenter を所有する](Reference-Presenter-Base-Classes#子-presenter-を所有する-attachchild) — `AttachChild` の契約・破棄順序
- [HowTo: Presenter をテストする § 子 Presenter を含む画面のテスト](HowTo-Test-A-Presenter#子-presenter-を含む画面のテスト)
- [連鎖選択 (カスケード) を扱う](HowTo-Handle-Cascading-Selection) — 3 段以上の N 段連鎖 (`ISelection<T>` + `Cascade`)
- [ViewAction システム](Reference-ViewAction-System) — State-driven CanExecute
- [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) — 詳細読み込みの async 化
- サンプル:
  - `samples/WinformsMVP.Samples/MasterDetailDemo/` — 顧客 → 注文の完全例
