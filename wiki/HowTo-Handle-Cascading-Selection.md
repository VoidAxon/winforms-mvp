# HowTo: 連鎖選択 (カスケード) を扱う

「カテゴリ → サブカテゴリ → 商品」「国 → 都道府県 → 市区町村」「組織 → 部門 → 担当者」——
**上の階層の選択が下の階層のリストを決める** 連鎖選択 (カスケード) は、業務アプリで頻出します。

単純そうに見えて、素朴に実装すると必ず 2 か所で詰まります。

- 階層が N 段あると、**同じ形のコードを N 回書く**
- 上を切り替えたとき、**下の選択をクリアし忘れて** 古い選択が残る

このページは、汎用の `ISelectionStore<T>` と小さな `Cascade` ヘルパーだけで、この 2 つを構造的に潰します。状態の持ち方は [共有 Model](HowTo-Communicate-Between-Presenters#共有-model--イベント) の原則そのままで、新しい思想は持ち込みません。

---

## 目次

- [難しさはどこか](#難しさはどこか)
- [素朴な実装が破綻する理由](#素朴な実装が破綻する理由)
- [解法: `ISelectionStore<T>` + `Cascade`](#解法-iselectionstoret--cascade)
- [フレームワークプリミティブ](#フレームワークプリミティブ)
- [選択の同一性と値型](#選択の同一性と値型)
- [例: カテゴリ → サブカテゴリ → 商品 (3 段)](#例-カテゴリ--サブカテゴリ--商品-3-段)
- [なぜ「Presenter が Presenter を持つ」にならないのか](#なぜpresenter-が-presenter-を持つにならないのか)
- [リスト再読込が選択を再発火しない契約](#リスト再読込が選択を再発火しない契約)
- [多親依存 (`Cascade.Combine`)](#多親依存-cascadecombine)
- [設計上の注意](#設計上の注意)
- [動的深度のとき](#動的深度のとき)
- [メリット / デメリット](#メリット--デメリット)
- [アンチパターン](#アンチパターン)
- [関連ページ](#関連ページ)

---

## 難しさはどこか

連鎖選択の各レベルがやることは、実はどのレベルでも同じです。

1. **1 つ上のレベルの選択変更** を受け取る
2. その選択に応じて **自分のリストを再読込** する
3. **自分の選択をクリア** する (上が変わったら下の選択は無効)
4. 自分の選択クリアによって、**さらに下も連鎖的にクリア・再読込** される

問題はロジックではなく、この「同じこと」を階層の数だけ繰り返す **重複** と、3 番を 1 か所でも忘れると古い選択が残る **抜け** です。

---

## 素朴な実装が破綻する理由

### ❌ レベルごとに手書きの選択 Service を N 個

```csharp
public interface ICategorySelectionService    { Category Selected { get; } event EventHandler Changed; }
public interface ISubCategorySelectionService { SubCategory Selected { get; } event EventHandler Changed; }
public interface IProductSelectionService     { Product Selected { get; } event EventHandler Changed; }
// ↑ 型引数が違うだけの同型を、インターフェイス・実装・DI 登録まで N 份手書き
```

ジェネリックで一発のはずのものを手で展開しているだけ。純粋なボイラープレートです。

### ❌ 下位クリアを各 Presenter が手書き

各レベルの Presenter が「上が変わったら自分の `Select(null)` を呼ぶ」を毎回書く必要があり、1 つ抜けると stale 選択が残ります。レビューでも見つけにくい類のバグです。

### ❌ 全レベルを束ねる「コーディネーター Presenter」

親 Presenter が子 Presenter を全部抱えて手で `reload` を呼んで回る形は、[Presenter が他の Presenter を持つ](HowTo-Communicate-Between-Presenters#-presenter-が他の-presenter-の参照を直接持つ) アンチパターンに直行します。

---

## 解法: `ISelectionStore<T>` + `Cascade`

要点は 2 つだけ。

- **N 個の同型インターフェイスを 1 つのジェネリック** `ISelectionStore<T>` に畳む
- **「上を購読 → 自分をクリア → 再読込」の 3 段を 1 行の宣言** に畳む (`Cascade.Bind`)

そして「自分をクリアすると、その変更通知で **さらに下が自動的にクリアされる**」——この連鎖が自動で起きるので、クリアの書き忘れが構造的に発生しません。

---

## フレームワークプリミティブ

汎用の選択ストア (`WinformsMVP.Common`)。判定の既定は「`T` が `ISelectable` を実装していれば Key 同値 (`SelectableKeyComparer<T>` を自動採用)、そうでなければ `EqualityComparer<T>.Default`」で、`IEqualityComparer<T>` を明示注入すればそれが最優先です ([選択の同一性と値型](#選択の同一性と値型) 参照)。

```csharp
public interface ISelectionStore<T> where T : class
{
    T Current { get; }
    void Select(T item);              // Select(null) = clear
    event EventHandler CurrentChanged;
}

public sealed class SelectionStore<T> : ISelectionStore<T> where T : class
{
    private readonly IEqualityComparer<T> _comparer;

    public SelectionStore(IEqualityComparer<T> comparer = null)
    {
        // No comparer: ISelectable -> compare by Key; otherwise EqualityComparer<T>.Default.
        _comparer = comparer
            ?? (typeof(ISelectable).IsAssignableFrom(typeof(T))
                    ? (IEqualityComparer<T>)SelectableKeyComparer<T>.Instance
                    : EqualityComparer<T>.Default);
    }

    public T Current { get; private set; }

    public void Select(T item)
    {
        // Short-circuit when unchanged (clearing an already-empty level fans out nothing).
        bool same = (Current == null && item == null)
                 || (Current != null && item != null && _comparer.Equals(Current, item));
        if (same) return;

        Current = item;
        var handler = CurrentChanged;
        if (handler != null) handler(this, EventArgs.Empty);
    }

    public event EventHandler CurrentChanged;
}
```

> ⚠️ **判定は Id で、参照に頼らない。** 親が変わると下位リストは再クエリされ、**全く新しいインスタンス** に入れ替わります。参照同値で選択を比較/復元すると、論理的に「同じ」レコードが参照違いで一致しません。**エンティティに `ISelectable` を実装** すれば、`SelectionStore<T>` は comparer 未指定でも Key で判定するので、この罠は既定で塞がります (次節)。`ISelectable` を実装せず `Equals` も未実装の型では `EqualityComparer<T>.Default` が参照同値に退化するため、その場合は Id 比較の `IEqualityComparer<T>` を注入してください。

連鎖を 1 行にするヘルパー。**同期版**で、`initialSync` (既定 `true`) は束縛時に一度だけ現在値で同期します。

```csharp
public static class Cascade
{
    // from が変わったら: target をクリア (→ 下位が連鎖クリア) し、新しい親値で再読込する。
    // initialSync=true: 束縛時にも一度 reload する (束縛時に親が既に値を持っていても下位が空のままにならない)。
    public static IDisposable Bind<TParent, TChild>(
        ISelectionStore<TParent> from,
        ISelectionStore<TChild> target,
        Action<TParent> reload,
        bool initialSync = true)
        where TParent : class where TChild : class
    {
        EventHandler handler = delegate
        {
            target.Select(null);     // clear self -> target.CurrentChanged -> downstream Bind clears next
            reload(from.Current);     // from.Current may be null (parent cleared)
        };
        from.CurrentChanged += handler;
        if (initialSync) reload(from.Current);   // each level self-syncs; order-independent
        return /* IDisposable that unsubscribes */;
    }

    // 多親版: target が a と b の両方に依存する場合 (下記「多親依存」参照)。
    public static IDisposable Combine<TA, TB, TChild>(
        ISelectionStore<TA> a, ISelectionStore<TB> b, ISelectionStore<TChild> target,
        Action<TA, TB> reload, bool initialSync = true)
        where TA : class where TB : class where TChild : class { /* ... */ }
}
```

`Cascade` は純粋なヘルパーで外部依存ゼロ、テストも容易です。

---

## 選択の同一性と値型

`ISelectionStore<T>` には `where T : class` 制約があります。これは「**未選択 = `null`**」を `Current` で表現するための制約で、値型 (`int` / `enum` / `Guid` / `DateTime` / `bool`) は `null` を持てないため直接は載りません (`string` は参照型なのでそのまま使えます)。そこで `WinformsMVP.Common` は同一性まわりの小さな補助を 3 つ用意します。

### エンティティ — `ISelectable` を実装する

エンティティが Key (Id 等) を 1 か所宣言すれば、`SelectionStore<T>` は comparer を渡さなくても `SelectableKeyComparer<T>` を **自動採用** し、Key で判定します。再読込で別インスタンスになった「同じ行」も一致します。

```csharp
public sealed class Category : ISelectable
{
    public int Id { get; set; }
    public string Name { get; set; }
    public object Key { get { return Id; } }   // identity = Id
}

// comparer 未指定でも Key (=Id) 判定。手で comparer を渡す必要なし。
var categoryStore = new SelectionStore<Category>();
```

> 💡 `ISelectable` 方式は判定を **選択ストアの中だけに局所化** します。エンティティの `Equals`/`GetHashCode` を Id ベースに override するとアプリ全体 (Dictionary・HashSet・LINQ `Distinct` …) の同値性が変わってしまいますが、`ISelectable` + `SelectableKeyComparer<T>` ならエンティティ本来の同値性はそのままです。

### 値型 — `SelectableItem<T>` で包む

値型や enum を選択単位にするときは `SelectableItem<T>` で包みます。Value を identity とし、`Text`/`ToString()` を持つのでリスト/コンボのデータソース項目としてもそのまま使えます。`SelectableItem<T>` 自身が `ISelectable` なので、ストアは Value で判定します。

```csharp
var yearStore = new SelectionStore<SelectableItem<int>>();
yearStore.Select(SelectableItem.Of(2025));
yearStore.Select(SelectableItem.Of(2025, "2025 年度"));   // 表示文字列付き

// 生成ヘルパー (型推論が効く)
SelectableItem.Of(2025);                          // 単一
SelectableItem.From(new[] { 1, 2, 3 }, n => "#" + n);  // 列を一括ラップ
SelectableItem.FromEnum<OrderStatus>();                // enum 全値
```

`Cascade.Bind` の `reload` も包装型を受けます (`year => ... year.Value ...`)。

### まとめ

| 選びたいもの | ストアの型 | 既定の判定 |
|---|---|---|
| 領域エンティティ (`Category` …) | `ISelectionStore<Category>` (`ISelectable` 実装) | Key (Id) |
| `string` | `ISelectionStore<string>` | `EqualityComparer.Default` (値) |
| 値型 `int`/`enum`/`Guid`/`DateTime`/`bool` | `ISelectionStore<SelectableItem<int>>` | Value |
| 上記以外で独自判定したい | いずれも、`IEqualityComparer<T>` を注入 | 注入した comparer (最優先) |

---

## 例: カテゴリ → サブカテゴリ → 商品 (3 段)

各レベルの View は、選択リストを表す汎用インターフェイス `ISelectListView<T>` です (サンプルの `SelectListControl<T>` が実装)。

```csharp
public interface ISelectListView<T> : IViewBase where T : class
{
    IList<T> Items { set; }                 // 再読込。後述の契約により SelectionChanged を発火しない
    T Selected { get; }
    event EventHandler SelectionChanged;     // ユーザー操作のときだけ発火
}
```

### 各レベルの Presenter

各 Presenter が握るのは **Store (共有 Model)** と **Repository** だけ。他の Presenter は一切参照しません。各レベルは「入 (`Cascade.Bind` で上位に応じて自分のリストを再読込)」と「出 (自分の View の選択を自分の Store に書き戻す)」の二役です。

```csharp
// 最上位 — 親はいない。自分のリストを読み、ユーザー選択を Store へ公開する
public sealed class CategoryListPresenter : ControlPresenterBase<ISelectListView<Category>>
{
    private readonly ISelectionStore<Category> _store;
    private readonly ICategoryRepository _repo;

    public CategoryListPresenter(ISelectionStore<Category> store, ICategoryRepository repo)
    {
        _store = store;
        _repo = repo;
    }

    protected override void OnViewAttached()
    {
        // 出: View 選択 → Store。購読の解除は Disposables バッグが自動で行う
        EventHandler handler = OnUserSelected;
        View.SelectionChanged += handler;
        Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);
    }

    protected override void OnInitialize()
        => View.Items = _repo.GetAll();                  // 初期リスト読み込みは OnInitialize

    private void OnUserSelected(object sender, EventArgs e) => _store.Select(View.Selected);

    // Cleanup の override は不要 — Disposables は Dispose 時にフレームワークが解放する
}

// 中間 — 入: Category を購読してリスト再読込; 出: サブカテゴリ選択を Store へ
public sealed class SubCategoryListPresenter : ControlPresenterBase<ISelectListView<SubCategory>>
{
    private readonly ISelectionStore<Category> _parent;       // 上位選択 (共有 Model)
    private readonly ISelectionStore<SubCategory> _self;      // 自分の選択 (共有 Model)
    private readonly ISubCategoryRepository _repo;

    public SubCategoryListPresenter(
        ISelectionStore<Category> parent,
        ISelectionStore<SubCategory> self,
        ISubCategoryRepository repo)
    {
        _parent = parent; _self = self; _repo = repo;
    }

    protected override void OnViewAttached()
    {
        EventHandler handler = OnUserSelected;
        View.SelectionChanged += handler;
        Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);

        Cascade.Bind(_parent, _self, category =>
        {
            try
            {
                View.Items = category == null
                    ? new SubCategory[0]                 // net40 には Array.Empty<T>() がない
                    : _repo.GetByCategory(category.Id);  // 同期リポジトリ
            }
            catch (Exception ex)
            {
                View.Items = new SubCategory[0];         // reload は自己回復する。Cascade は巻き戻さない
                Messages.ShowError("Failed to load subcategories: " + ex.Message, "Error");
            }
        }).DisposeWith(Disposables);                     // 生存期間を作成行で宣言

    }

    private void OnUserSelected(object sender, EventArgs e) => _self.Select(View.Selected);

    // Cleanup の override は不要 — フレームワークが Disposables を自動解放する
}

// 末端 — SubCategory を購読する。中間とまったく同じ形
public sealed class ProductListPresenter : ControlPresenterBase<ISelectListView<Product>>
{
    private readonly ISelectionStore<SubCategory> _parent;
    private readonly ISelectionStore<Product> _self;
    private readonly IProductRepository _repo;

    public ProductListPresenter(
        ISelectionStore<SubCategory> parent,
        ISelectionStore<Product> self,
        IProductRepository repo)
    {
        _parent = parent; _self = self; _repo = repo;
    }

    protected override void OnViewAttached()
    {
        EventHandler handler = OnUserSelected;
        View.SelectionChanged += handler;
        Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);

        Cascade.Bind(_parent, _self, sub =>
        {
            try { View.Items = sub == null ? new Product[0] : _repo.GetBySubCategory(sub.Id); }
            catch (Exception ex)
            {
                View.Items = new Product[0];
                Messages.ShowError("Failed to load products: " + ex.Message, "Error");
            }
        }).DisposeWith(Disposables);
    }

    private void OnUserSelected(object sender, EventArgs e) => _self.Select(View.Selected);
}
```

中間と末端の形が完全に同じであることに注目してください。階層が 4 段・5 段になっても、増えるのは同型の Presenter と `Cascade.Bind` 1 行だけです。

### composition root (親 Form)

Store と各 Presenter を生成し配線するのは composition root の責務です。**Store は画面ごとに生成** します (アプリ全体の singleton にしない)。

```csharp
var categoryStore    = new SelectionStore<Category>();
var subCategoryStore = new SelectionStore<SubCategory>();
var productStore     = new SelectionStore<Product>();
var catalog          = new InMemoryCatalog();

var categoryP    = new CategoryListPresenter(categoryStore, catalog);
var subCategoryP = new SubCategoryListPresenter(categoryStore, subCategoryStore, catalog);
var productP     = new ProductListPresenter(subCategoryStore, productStore, catalog);

categoryP.Connect(categoryListControl);
subCategoryP.Connect(subCategoryListControl);
productP.Connect(productListControl);
```

`subCategoryP` が受け取るのは `categoryStore` (上位 Model) と `subCategoryStore` (自分の Model) で、Presenter 参照はゼロです。

### 動作の流れ

ユーザーがカテゴリ `C` を選ぶと、すべてが **同期・深さ優先・1 パス** で処理されます。

```
ユーザーが Category C を選択
  → CategoryListPresenter: _categoryStore.Select(C)
  → categoryStore.CurrentChanged
      → SubCategory の Bind:
          ① _subCategoryStore.Select(null)            // サブカテゴリ選択をクリア
              → subCategoryStore.CurrentChanged
                  → Product の Bind:
                      ① _productStore.Select(null)     // 商品選択をクリア (既に null なら短絡)
                      ② reload(sub = null) → 商品リスト = 空
          ② reload(category = C) → サブカテゴリリスト = C の一覧
```

「サブカテゴリ再読込・選択クリア・商品リスト空・商品選択クリア」が一回で漏れなく揃います。各 Presenter はクリアを 1 行も書いていません。束縛時 (`initialSync = true`) は各層が現在値で自己同期するので、既定選択があっても・attach 順が前後しても、下位が空のまま取り残されません。

完全に動くサンプル: `samples/WinformsMVP.Samples/CascadeDemo/`。

---

## なぜ「Presenter が Presenter を持つ」にならないのか

「連鎖」と聞くと「親が子を駆動」しているように見えますが、実装は次のとおりです。

- 各 Presenter が握るのは `ISelectionStore<T>` (共有 Model) と Repository **だけ**。他 Presenter への参照は存在しない。
- `CategoryListPresenter` と `SubCategoryListPresenter` は **互いの存在を知らない**。やり取りは Store の `CurrentChanged` 経由の間接通信のみ。
- 全レベルを束ねる **コーディネーター Presenter も不要**。連鎖は Store の通知が運ぶ。

これは [共有 Model](HowTo-Communicate-Between-Presenters#共有-model--イベント) で `OrderSummaryPresenter` が `_orderModel.ProductAdded` を購読するのと同じ構図です。Model 変更を購読して反応するのは Presenter の正常な責務で、[Presenter が他の Presenter を持つ](HowTo-Communicate-Between-Presenters#-presenter-が他の-presenter-の参照を直接持つ) アンチパターンには該当しません。

> 💡 変数名は `_categoryStore` のように **Store と分かる名前** に。`_categorySel` のような略称は「CategorySelector (= Presenter/コンポーネント)」と誤読され、持っているのが Model か Presenter か分からなくなります。

---

## リスト再読込が選択を再発火しない契約

再読込で `View.Items` を入れ替えると、WinForms の `ListBox`/`DataGridView` は **先頭行を自動選択して選択イベントを発火** しがちです。これが `View.SelectionChanged → store.Select(自動項)` と回り込むと、ユーザー意図でない二次連鎖が起きます (クリア後 `Current=null` なので、自動選択された非 null は短絡されず発火してしまう)。

そこで **View 側の契約**: 「`Items` の再設定は `SelectionChanged` を発火しない」。サンプルの `SelectListControl<T>` は再設定中だけ抑制フラグを立てます。

```csharp
public IList<T> Items
{
    set
    {
        _suppress = true;
        try
        {
            _list.Items.Clear();
            if (value != null) foreach (var item in value) _list.Items.Add(item);
            _list.ClearSelected();
        }
        finally { _suppress = false; }
    }
}

// SelectedIndexChanged ハンドラ内: if (_suppress) return; ...
```

> この抑制は **View 側の責務** であって `Cascade` には入れません。回灌の経路は View を通るため、`Cascade` からは見えないからです。原語はあくまで純粋に保ちます。

---

## 多親依存 (`Cascade.Combine`)

1 つのレベルが **複数の親に同時に依存** することもあります (例:「商品リストは サブカテゴリ *と* 倉庫選択の両方で決まる」)。これは線形チェーン (`Bind`) では表せないので、`Bind` の入れ子で無理に組まないでください。

両方の親を購読し、どちらが変わっても両方の現在値で再読込する `Combine` を使います。

```csharp
Cascade.Combine(_subCategoryStore, _warehouseStore, _productStore,
    (sub, warehouse) =>
    {
        View.Items = (sub == null || warehouse == null)
            ? new Product[0]
            : _repo.GetBySubCategoryAndWarehouse(sub.Id, warehouse.Id);
    }).DisposeWith(Disposables);
// sub か warehouse のどちらが変わっても: productStore をクリア → 両方の現在値で reload
```

3 つ以上の親が必要なら、`Combine` のオーバーロードを足すか、複数の親の `CurrentChanged` を手で購読し、同一ハンドラで `target.Select(null)` 後にすべての現在値で再読込します (論理は等価、`Bind` ほど一行ではないだけ)。

---

## 設計上の注意

- **判定は Id・参照ではない** (上の ⚠️)。再読込は実例を入れ替えるので、参照同値はこの場面で必ず咬みます。
- **`initialSync` は既定 ON**。「束縛時に親が既に値を持っているのに下位が空のまま」という隠れた罠を消します。各層が独立に初期同期し、束縛順に依存しません。初期同期は **再読込のみでクリアはしない** ので、Store を事前に詰めてから順に束縛すれば状態復元にも使えます。
- **`reload` は自己回復する**。`Cascade` は巻き戻しません。同期 `reload` が途中で例外を投げると、最初に発火した Presenter まで呼び出しスタックを遡って「半端な連鎖」を残します。同期リポジトリでも例外は起き得る (タイムアウト・接続断) ので、`reload` 内で try/catch し、失敗時はリストを空にして通知 (上の例)。
- **解放は `Disposables` バッグに任せる**: `Cascade.Bind` / `Combine` の戻り値と View イベント購読は、作成行で `.DisposeWith(Disposables)` を付ければ Presenter の Dispose 時にフレームワークが自動解放します。`_bind` フィールドや `Cleanup` override は不要です ([購読ライフサイクルの管理](HowTo-Manage-Presenter-Subscriptions) 参照)。
- **初期リスト読み込みは `OnInitialize`**、View イベント購読は `OnViewAttached` (フレームワークのライフサイクル。[Presenter 基底クラス](Reference-Presenter-Base-Classes) 参照)。
- **DI は開放ジェネリック登録が前提**。`services.AddSingleton(typeof(ISelectionStore<>), typeof(SelectionStore<>));` 1 行で全レベルを賄えます。ただし **singleton はアプリ全体共有** になるため、独立した複数の連鎖画面があるなら scoped/transient か手書き組み立てで **画面ごとに隔離** してください。
- **将来の非同期化**は `Action<TParent>` 版とは別に `Func<TParent, Task>` 版の `Cascade.Bind` を追加して並存させれば、既存 API を壊さず移行できます ([非同期処理を扱う](HowTo-Handle-Async-Operations) 参照)。
- **net40 の制約**。`Array.Empty<T>()` (4.6+) と `IReadOnlyList<T>` (4.5+) は net40 にありません。空表現は `new T[0]`、戻り値は `T[]` / `IList<T>`。`EqualityComparer<T>.Default` / `IEqualityComparer<T>` は net40 可用。

---

## 動的深度のとき

階層の **深さがコンパイル時に分からない** 場合 (自己再帰的なファイルツリー等) は、型ごとに `ISelectionStore<T>` を分ける方式は合いません。レベル番号で選択を持つ単一の `DrillDownPath` を用意し、`SelectAt(level, item)` がそのレベルより下をすべて切り捨てる設計に切り替えます。より重い抽象なので、**深さが固定で各レベルの型が異なる** 通常の連鎖選択では本ページの方式を既定とし、`DrillDownPath` は逃げ道として温存します。

---

## メリット / デメリット

- ✅ N 段でも増えるのは同型 Presenter と `Cascade.Bind` 1 行だけ — 重複が消える
- ✅ 下位クリアが連鎖通知で自動 — 書き忘れによる stale 選択が構造的に起きない
- ✅ `initialSync` 内蔵で「束縛時に値があるのに空」の罠がない
- ✅ Presenter 同士は無結合、コーディネーター Presenter 不要
- ✅ Store と `Cascade` はモック・単体テストが容易
- ✅ 状態の所有者が明確 (各 `SelectionStore<T>`)
- ❌ `SelectionStore<T>` と `Cascade` をフレームワーク側に用意する必要がある
- ❌ DI に開放ジェネリック登録があると最も楽 (なくても可)
- ❌ 多親は `Combine`、動的深度は別設計 (`DrillDownPath`) が必要

---

## アンチパターン

### ❌ レベルごとに手書きの選択 Service を N 個

型引数違いの同型を手で展開するのは純粋なボイラープレート。`ISelectionStore<T>` 1 つに畳む。

### ❌ コーディネーター Presenter が全子 Presenter を抱えて手で reload

```csharp
// ❌ Bad — Presenter が Presenter を持ち、手で連鎖を回す
public class MasterDetailPresenter
{
    private readonly SubCategoryListPresenter _sub;   // ← これはやらない
    private readonly ProductListPresenter _product;   // ← これも

    private void OnCategoryChanged(Category c) { _sub.Reload(c); _product.Clear(); }
}
```

各レベルを `ISelectionStore<T>` の購読で自律させ、コーディネーターを消す。

### ❌ 下位選択のクリアを各 Presenter で手書き

クリア漏れの温床。`Cascade.Bind` に任せ、`target.Select(null)` の連鎖で自動化する。

### ❌ 選択状態を EventAggregator で管理

選択は **所有・観測される状態** なので Store (共有 Model) の責務。EventAggregator は所有者のない横断通知のためのもの ([EventAggregator の誤用に注意](HowTo-Communicate-Between-Presenters#eventaggregator-の誤用に注意) 参照)。

---

## 関連ページ

- [Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — 共有 Model / EventAggregator / コールバック / 直接呼び出しの使い分け
- [マスター/詳細パターンを実装する](HowTo-Implement-Master-Detail) — 1〜2 段の master-detail。本ページはその N 段版
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — ライフサイクル (`OnViewAttached` / `OnInitialize` / `Cleanup`)
- [Dependency Injection](Reference-DependencyInjection) — 開放ジェネリック登録
- [非同期処理を扱う](HowTo-Handle-Async-Operations) — 遅いリポジトリで連鎖を非同期化する場合
