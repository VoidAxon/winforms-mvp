# ChangeTracker

`ChangeTracker<T>` は、データが読み込み時 (または最後の保存時) に撮った **スナップショット** (写真) から変わったかどうかを **検出するだけ** のコンポーネントです。
データの持ち主は呼び出し側 (Presenter) で、tracker がデータを預かることはありません。`IChangeTracking` だけを実装します (元に戻す機能はありません)。

> **位置づけ**: 編集画面の `CanExecute` 判定 (「未保存の変更があるか」) と、`CanClose` の保存確認を支える基幹コンポーネントです。

---

## 目次

- [型制約](#型制約)
- [スナップショット (複製) の解決順序](#スナップショット-複製-の解決順序)
- [比較の解決順序](#比較の解決順序)
- [第三者ライブラリの差し込み](#第三者ライブラリの差し込み)
- [項目を比較から外す (画面ごと)](#項目を比較から外す-画面ごと)
- [2 つの使い方](#2-つの使い方)
- [なぜ深いコピーが必要か](#なぜ深いコピーが必要か)
- [主要 API](#主要-api)
- [変更の通知](#変更の通知)
- [高度な機能](#高度な機能)
- [関連ページ](#関連ページ)

---

## 型制約

```csharp
public sealed class ChangeTracker<T> : IChangeTracking where T : class
```

- **`T : class`** — 参照型のみ対応 (値型には追跡する「参照」がないため。必要なら編集状態クラスに包む)
- **渡したオブジェクトはコピーせず参照を保持** し、スナップショットだけを深いコピーで取る。Presenter がそのオブジェクトをその場で編集すれば `IsChanged` にすぐ反映される
- **`ICloneable` は使われない** — .NET の `Clone()` は浅いコピーでもよい契約のため、実装していても参照しない。検証済みの深い `Clone()` を使いたい場合は `ChangeTrackingPolicy<T>` で明示する
- **`IEquatable<T>` / `Equals` は比較に使われない** — 既定は内容比較 (`ObjectComparer.ContentEquals`)。独自の等価性を使いたい場合は `comparer` を明示的に渡す

### コンストラクタと `TrackChanges`

複製・比較の戦略 (`ChangeTrackingPolicy<T>`) は**生成時に固定**され、以後グローバルな設定は読みません。

```csharp
new ChangeTracker<T>(value);                               // 組み込みエンジン (リフレクション深いコピー + 内容比較)
new ChangeTracker<T>(value, Func<T, T, bool> comparer);    // 比較だけ指定 (複製は組み込み)
new ChangeTracker<T>(value, IEqualityComparer<T> comparer);
new ChangeTracker<T>(value, ChangeTrackingPolicy<T> policy); // 複製と比較を明示
```

Presenter の中では `TrackChanges(value)` を優先してください。起動時の `mvp.ChangeTracking` の設定
(型ごとのポリシー、差し替えたエンジン) を反映した tracker を返します。`new ChangeTracker<T>(...)` は
コンストラクタに渡したもの (または組み込みエンジン) しか使いません。

```csharp
_tracker = TrackChanges(order);                      // 設定を反映 (推奨)
_tracker = TrackChanges(order, policy);              // ポリシーを明示 (設定は見ない)
_tracker = TrackChanges(order, (a, b) => a.SameEditableContent(b)); // 比較だけ指定。複製は設定に従う
```

---

## スナップショット (複製) の解決順序

スナップショットを保存するために `T` のコピーを作る必要があります。`TrackChanges(value)` は以下の順で複製戦略を決定します。

1. **`T` ちょうどに登録された `ChangeTrackingPolicy<T>`** (`mvp.ChangeTracking.For<T>(policy)`) → その `Clone`
2. **登録された `IObjectCloner`** (`mvp.ChangeTracking.UseCloner(...)`) → その `DeepClone`
3. **どちらも無い** → 組み込みのリフレクション深いコピー (`BuiltInObjectCloner`)

`new ChangeTracker<T>(value)` は常に 3 (またはコンストラクタで渡したポリシー) を使います。
リフレクション深いコピーは正確ですが速度面では手書きのコピーに劣ります。性能が重要なら検証済みの深いコピーをポリシーで登録してください。

> 組み込みの `ObjectCloner` / 深い比較は、`HashSet` / `SortedSet` / `Queue` / `Stack` / `LinkedList` / `ReadOnlyCollection` を要素単位で扱います (セットの比較子やスタックの順序は維持)。`Type` / `MemberInfo` / `Uri` / `Version` は不変として扱いコピーせずに共有します。コレクションを通る循環参照はコピー側に解決されます (コンストラクタでしか要素を受け取れないコレクションを通る循環のみ `NotSupportedException`)。参照同一性の要素を持つセットは要素の深い等価性で対応付けて比較するため、未変更のディープコピーと等しく判定され、構築直後に dirty になることはありません。

---

## 比較の解決順序

`IsChanged` / `IsChangedWith` 判定 (追跡中のオブジェクトまたは渡した値と、スナップショットの比較) は以下の順で戦略を決定します。

1. **コンストラクタ / `TrackChanges` に `comparer` かポリシーを渡した** → それを使用
2. **(`TrackChanges` のみ) `T` ちょうどに登録された `ChangeTrackingPolicy<T>`** → その `ContentEquals`
3. **(`TrackChanges` のみ) 登録された `IObjectContentComparer`** (`mvp.ChangeTracking.UseComparer(...)`) → それを使用
4. **いずれも無い** → 組み込みの内容比較 (`BuiltInObjectContentComparer` = `ObjectComparer.ContentEquals`)

`T` 自身の `IEquatable<T>` / `Equals` は **意図的に使いません**。エンティティは「Id が同じなら等しい」と `Equals` を定義することが多く、それを使うと内容を編集しても `IsChanged == false` となり、`CanClose` の保存確認が出ずにデータが失われるためです。

`ContentEquals` は、値型とフレームワーク型 (`System.*` / `Microsoft.*`) の `Equals` だけを信頼し、アプリケーションの参照型はどの深さでもフィールド単位で比較します (`Order.Customer.Name` の変更も、`Customer.Equals` が Id 比較でも検出される)。参照型の要素を持つセットも同様に要素の内容で対応付けます。一方 `ObjectComparer.DeepEquals` は従来どおりすべての型の `Equals` を尊重します。

```csharp
new ChangeTracker<Order>(order);                         // 内容比較 (既定)
new ChangeTracker<Order>(order, (a, b) => a.Equals(b));  // T の Equals を使いたい場合は明示
```

> **動作変更の注意**: 以前は `T` が `IEquatable<T>` / `IComparable<T>` / `Equals` オーバーライドを持つ場合それを使っていました。現在は `comparer` を渡さない限り内容比較です。`Equals` で一部フィールドを意図的に無視していたモデルは、同じ意味の `comparer` を渡してください。

---

## 第三者ライブラリの差し込み

アプリ起動時の `mvp.ChangeTracking` で、複製・比較エンジンと型ごとのポリシーを設定できます
(`TrackChanges` で作った tracker に効きます)。第三者ライブラリは `IObjectCloner` /
`IObjectContentComparer` を実装する数行のアダプタで差し込みます。

```csharp
// 例: Force.DeepCloner を使うアダプタ
public sealed class DeepClonerAdapter : IObjectCloner
{
    public object DeepClone(object value) => value?.DeepClone();
}

// 例: CompareNETObjects を使うアダプタ (画面ごとの Ignore をライブラリの設定に対応付ける)
public sealed class CompareNetObjectsComparer : IObjectContentComparer
{
    public bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores)
    {
        var config = new ComparisonConfig();
        foreach (var m in ignores.Members)     config.MembersToIgnore.Add(m.Owner.Name + "." + m.Name);
        foreach (var n in ignores.MemberNames) config.MembersToIgnore.Add(n);
        foreach (var t in ignores.Types)       config.TypesToIgnore.Add(t);
        return new CompareLogic(config).Compare(left, right).AreEqual;
    }
}

MvpApplication.Configure(framework: mvp =>
{
    mvp.ChangeTracking.UseCloner(new DeepClonerAdapter());
    mvp.ChangeTracking.UseComparer(new CompareNetObjectsComparer());

    // 1 つの型だけ: 検証済みの深い Clone() と、監査用タイムスタンプを無視する比較
    mvp.ChangeTracking.For(new ChangeTrackingPolicy<Order>(
        o => (Order)o.Clone(),
        (a, b) => a.SameEditableContent(b)));
});
```

- エンジンはアプリ全体で 1 インスタンスを共有するため、スレッドセーフに実装してください。
  `IObjectCloner` のコピーは元と状態を共有せず内容的に等しいこと、未対応のグラフは部分コピーではなく例外で失敗することが契約です。
- `For<T>` は `T` **ちょうど**の型にだけ効きます (基底型・派生型には効きません)。
- M.E.DI では `AddWinformsMVP(viewRegistry, mvp => ...)` に同じ設定を書きます。

通常はデフォルト (リフレクション深い処理) で十分ですが、ホットパスでパフォーマンスが問題になる場合に検討してください。
設定入口の全体は [フレームワークをカスタマイズする](HowTo-Customize-Framework) を参照してください。

---

## 項目を比較から外す (画面ごと)

画面によっては、変更として扱いたくない項目があります (更新日時、最終閲覧者、キャッシュ値など)。tracker を作るときに指定します。指定はその tracker だけに効きます。

```csharp
_tracker = TrackChanges(order)
    .Ignore(x => x.UpdatedAt)                  // Order の UpdatedAt
    .Ignore<OrderLine>(l => l.CachedTotal)     // OrderLine の CachedTotal (コレクション内の要素も含め、どこに出てきても)
    .IgnoreMember("LastViewedBy")              // どの型でも、この名前のメンバー
    .IgnoreType<AuditInfo>();                  // この型の値は比較しない
```

- 式は **その型のメンバー 1 段だけ** (`x => x.A`)。`x => x.Customer.Name` のようなパスはエラーになり、`Ignore<Customer>(c => c.Name)` と書くよう案内されます。メソッド呼び出し・インデクサー・計算プロパティ (値を持つフィールドがない) もエラーです。
- 自動プロパティのほか、`_name` / `m_name` のような手書きのバッキングフィールドを持つプロパティ (`NotifyingObject.SetProperty(ref _name, value)` など) も対象にできます。
- 新しく追加したプロパティは何もしなくても比較されます (除外漏れで変更を見逃すことがない)。
- メンバーの除外はアプリケーションの型だけに効きます (`System.*` / `Microsoft.*` の型の内部には及びません。例えば `IgnoreMember("Value")` が `int?` の比較を壊すことはありません)。独自の `Equals` を持つ型でも、除外があればフィールド単位で比較します。
- `IgnoreType<T>()` は、`object` 型やインターフェイス型のメンバー・コレクションの要素に入っている値にも効きます。その型の値どうし (別の派生型を含む) や `null` との違いは変更として扱いませんが、まったく別の値 (例えば文字列) に置き換えた場合は変更です。
- `Ignore` の指定は、次に `IsChanged` / `IsChangedWith` を読んだときから効きます。
- この 3 種類 (型のメンバー・メンバー名・型) は、CompareNETObjects や ObjectsComparer などの比較ライブラリが共通して持つ除外方法です。比較エンジンを差し替えた場合、tracker の除外設定は `ChangeTrackingIgnores` として `IObjectContentComparer.ContentEquals` に渡されるので、アダプタでライブラリの設定に対応付けます (上の例)。
- 比較関数を自分で渡した tracker (`new ChangeTracker<T>(value, comparer)`、デリゲートで作った `ChangeTrackingPolicy<T>`) では `Ignore` は `InvalidOperationException` になります。その比較関数の中で除外してください。

## 2 つの使い方

tracker の使い方は 2 通りあり、それぞれ「判定」と「確定 (写真の撮り直し)」の組が決まっています。

| | どんなとき | 判定 | 確定 (保存後) | 保証 |
|---|---|---|---|---|
| **書き方 1** | Presenter が 1 つのモデルを編集し続ける (`TrackChanges(_order)` → `_order.Name = ...`) | `IsChanged` | `AcceptChanges()` | 以後 `IsChanged == false` |
| **書き方 2** | 必要なたびに View から新しいモデルを作る (`new Order(View.Name, ...)`) | `IsChangedWith(x)` | `AcceptChanges(x)` | 以後 `IsChangedWith(x) == false` |

比較は内容で行うため、値を元に戻せば「変更なし」に戻ります。

### 書き方 1: Presenter が 1 つのモデルを編集し続ける

tracker は渡されたオブジェクトそのもの (参照) を覚え、そのスナップショットを撮ります。編集は `_order` に入れ、`IsChanged` で判定し、保存後に `AcceptChanges()` で撮り直します。

```csharp
public class EditOrderPresenter : WindowPresenterBase<IEditOrderView, int>
{
    private readonly IOrderRepository _repository;
    private Order _order;                    // データの持ち主は Presenter
    private ChangeTracker<Order> _tracker;   // tracker は変更の検出だけ

    public EditOrderPresenter(IOrderRepository repository) => _repository = repository;

    protected override void OnViewAttached()
    {
        View.DataChanged += OnDataChanged;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave, canExecute: () => HasUnsavedChanges);
    }

    protected override void OnInitialize(int orderId)
    {
        _order = _repository.Load(orderId);
        _tracker = TrackChanges(_order);     // _order 自体を覚え、スナップショットを撮る
        View.ShowOrder(_order);
        View.IsDirty = false;
    }

    private bool HasUnsavedChanges => _tracker != null && _tracker.IsChanged;

    private void OnDataChanged(object sender, EventArgs e)
    {
        _order.Name = View.OrderName;        // 編集は _order に入れる
        _order.Quantity = View.Quantity;
        View.IsDirty = HasUnsavedChanges;    // 通知はないので、ここで読む
        Dispatcher.RaiseCanExecuteChanged();
    }

    private void OnSave()
    {
        _repository.Save(_order);
        _tracker.AcceptChanges();            // 撮り直す → IsChanged == false
        View.IsDirty = false;
        Dispatcher.RaiseCanExecuteChanged();
    }
}
```

- `_order = _repository.Load(...)` のように **変数を別のオブジェクトに差し替えたら、tracker も作り直します** (tracker は古いオブジェクトを追跡し続けるため)。
- **非同期保存**: 保存するデータの独立したコピーを先に作り、成功後に `AcceptChanges(saved)` を呼びます。保存中にユーザーが編集していれば `IsChanged` は `true` のまま残ります (元の値に戻した場合も、保存先にはもう `saved` があるので `true`)。

```csharp
private async Task OnSaveAsync(CancellationToken ct)
{
    var saved = ObjectCloner.DeepCopy(_order);   // 保存するデータを固定する
    await _repository.SaveAsync(saved, ct);
    _tracker.AcceptChanges(saved);               // 確定するのは saved。_order には触れない
    View.IsDirty = _tracker.IsChanged;           // 保存中の編集があれば true のまま
    Dispatcher.RaiseCanExecuteChanged();
}
```

### 書き方 2: 毎回 View から新しいモデルを作る

最初のオブジェクトは最初のスナップショットを撮るためだけに使います。判定は `IsChangedWith(ReadModel())`、保存後は保存した値で `AcceptChanges(saved)` を呼びます。`record` や不変クラスはこちらの書き方です。

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, int>
{
    private readonly IUserRepository _repository;
    private ChangeTracker<UserModel> _tracker;

    public EditUserPresenter(IUserRepository repository) => _repository = repository;

    protected override void OnViewAttached()
    {
        View.DataChanged += OnDataChanged;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave, canExecute: () => HasUnsavedChanges);
    }

    protected override void OnInitialize(int userId)
    {
        var user = _repository.Load(userId);
        View.ShowUser(user);
        _tracker = TrackChanges(ReadModel());   // 最初のスナップショットを撮るためだけに使う
        View.IsDirty = false;
    }

    // 編集中の内容を View から新しいモデルとして読み出す
    private UserModel ReadModel() => new UserModel { Name = View.UserName, Email = View.Email };

    private bool HasUnsavedChanges => _tracker != null && _tracker.IsChangedWith(ReadModel());

    private void OnDataChanged(object sender, EventArgs e)
    {
        View.IsDirty = HasUnsavedChanges;
        Dispatcher.RaiseCanExecuteChanged();
    }

    private void OnSave()
    {
        var saved = ReadModel();
        _repository.Save(saved);
        _tracker.AcceptChanges(saved);          // 保存した内容を新しいスナップショットに
        View.IsDirty = HasUnsavedChanges;
        Dispatcher.RaiseCanExecuteChanged();
    }
}
```

> ⚠️ **書き方 2 で引数なしの `AcceptChanges()` を呼ばないでください。** 引数なし版は tracker を作ったときのオブジェクト (書き方 2 では古いまま) を撮り直すため、次の `IsChangedWith` が変更ありと報告します。

実例: `samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs` (書き方 2。編集する 3 項目だけを `EmailEditState` に読み出して比較し、非同期の下書き保存後に `AcceptChanges(submitted)`)。

### tracker の役割ではないこと

- **元に戻す**: tracker は検出だけを行い、データを復元しません (`RejectChanges` / `GetOriginalValue` は削除されました)。画面に留まったまま編集を取り消したい場合は、リポジトリから読み直して表示し、tracker を新しく作ります。
- **「破棄して閉じる」**: `RequestClose` の前に `AcceptChanges()` (書き方 1) / `AcceptChanges(ReadModel())` (書き方 2) を呼び、`RequestClose` が通る `CanClose` が確認しないようにします ([ウィンドウクローズとの連携](#3-ウィンドウクローズとの連携))。これは破棄した編集の追跡を終えるという意味で、保存したという意味ではありません。
- **データの保持**: 現在値を取り出すプロパティはありません (`CurrentValue` / `UpdateCurrentValue` は削除されました)。データは Presenter (書き方 1) か View (書き方 2) が持ちます。

---

## なぜ深いコピーが必要か

浅いコピー (MemberwiseClone) は、参照型プロパティが元のオブジェクトと **同じインスタンスを共有** してしまいます。すると tracker が取るスナップショットが追跡中のオブジェクトから独立せず、変更検出が壊れます。

### 浅いコピーの問題

```csharp
var user = new UserModel { Address = new Address { City = "Tokyo" } };
var tracker = new ChangeTracker<UserModel>(user);   // user を追跡し、スナップショットを撮る

// 浅いコピーだと、スナップショットは user と同じ Address インスタンスを共有してしまう。
// その状態で追跡中のオブジェクトを編集すると…
user.Address.City = "Osaka";

// スナップショットまで一緒に書き換わるため、変更を検出できない
bool changed = tracker.IsChanged;   // 浅いコピーだと false(期待: true)
```

### 自前の深いコピーを使う場合

既定のリフレクション深いコピーではこの罠は**自動的に回避されます**。手書きの `Clone()` を使う場合は、
それが深いコピーであることを確かめたうえで `ChangeTrackingPolicy<T>` として渡します
(`ICloneable` を実装しているだけでは使われません)。

```csharp
mvp.ChangeTracking.For(new ChangeTrackingPolicy<UserModel>(
    u => (UserModel)u.Clone(),
    (a, b) => ObjectComparer.ContentEquals(a, b)));
```

```csharp
public class UserModel : ICloneable
{
    public string Name { get; set; }
    public int Age { get; set; }
    public Address Address { get; set; }
    public List<string> Tags { get; set; }

    public object Clone()
    {
        return new UserModel
        {
            Name = this.Name,                              // 値型・string は OK
            Age  = this.Age,
            Address = this.Address?.Clone() as Address,    // ネストされたオブジェクトも Clone
            Tags = this.Tags != null
                ? new List<string>(this.Tags)              // コレクションは新規生成
                : null,
        };
    }
}
```

### ベストプラクティス

1. **すべての参照型プロパティを深くコピー** — ネストオブジェクト、コレクション
2. **`MemberwiseClone()` を使わない** — 浅いコピーになるため
3. **`Clone()` の独立性をテストする**

```csharp
[Fact]
public void Clone_CreatesIndependentCopy()
{
    var original = new UserModel { Name = "John" };
    var clone = (UserModel)original.Clone();

    clone.Name = "Jane";

    Assert.Equal("John", original.Name);   // 元は変更されない
    Assert.Equal("Jane", clone.Name);
}
```

既定のリフレクション深いコピーは上記の罠を **自動的に回避します** が、パフォーマンスは手書きのコピーに劣ります。

---

## 主要 API

公開メンバーは以下だけです (除外設定の `Ignores` / `Ignore` / `Ignore<TOwner>` / `IgnoreMember` / `IgnoreType<T>` は [項目を比較から外す](#項目を比較から外す-画面ごと) を参照)。

| メンバー | 書き方 | 説明 |
|---------|------|----|
| `ChangeTracker(T value[, comparer \| policy])` | 共通 | `value` を **参照のまま** 覚え (コピーしない)、深いコピーでスナップショットを撮る |
| `bool IsChanged` | 1 | 追跡中のオブジェクトがスナップショットと異なるか。読むたびに比較する |
| `void AcceptChanges()` | 1 | 追跡中のオブジェクトのスナップショットを撮り直す → 以後 `IsChanged == false` |
| `bool IsChangedWith(T value)` | 2 | `value` がスナップショットと異なるか。副作用なし |
| `void AcceptChanges(T value)` | 2 (と書き方 1 の非同期保存) | `value` の深いコピーを新しいスナップショットにする。追跡中のオブジェクトには触れない → 以後 `IsChangedWith(value) == false` |

削除されたメンバー: `CurrentValue`、`UpdateCurrentValue`、`RejectChanges` (と `IRevertibleChangeTracking`)、`GetOriginalValue`、`IsChangedChanged` イベント。

---

## 変更の通知

tracker は変更を通知しません (イベントはありません)。その場で編集されたオブジェクトの変化を tracker は知り得ず、書き方 2 では現在値すら持たないためです。

ボタンの活性は変更の有無だけで決まるわけではないので、Presenter は View のデータ変更ハンドラでどのみち `RaiseCanExecuteChanged()` を呼びます。同じ場所で `IsChanged` / `IsChangedWith` を読みます。

```csharp
private void OnDataChanged(object sender, EventArgs e)
{
    View.IsDirty = _tracker.IsChanged;   // 書き方 2 は _tracker.IsChangedWith(ReadModel())
    Dispatcher.RaiseCanExecuteChanged();
}
```

`AcceptChanges` の後も、`View.IsDirty` を自分で更新してください。

---

## 高度な機能

### 1. スレッド

tracker はスレッドセーフではありません (内部ロックはありません)。**tracker と追跡中のオブジェクトは 1 つのスレッドから順に使ってください** — WinForms では UI スレッドです。ロックで守れるのは tracker 自身のフィールドだけで、Presenter が編集しているオブジェクトは守れないためです。

非同期保存では、バックグラウンドには独立した保存用データだけを渡し、完了後に UI スレッドで `AcceptChanges(saved)` を呼びます ([書き方 1](#書き方-1-presenter-が-1-つのモデルを編集し続ける))。アプリ全体で共有する複製・比較エンジンは、複数の tracker から同時に使われても安全です。

### 2. パフォーマンス

`IsChanged` / `IsChangedWith` は結果をキャッシュせず、**読むたびにスナップショットと比較** します。そのため追跡中のオブジェクトをその場で編集した場合もすぐに検出されます。深い比較のコストは読むたびにかかるので、頻繁なポーリングは避けてください (データ変更ハンドラと `canExecute` で読む程度なら問題ありません)。

### 3. ウィンドウクローズとの連携

ウィンドウクローズ時のダーティチェックに使うのが典型パターンです (書き方 1 の例。書き方 2 では `IsChanged` を `IsChangedWith(ReadModel())`、`AcceptChanges()` を `AcceptChanges(ReadModel())` に読み替えます)。

```csharp
// The gate: X / Alt+F4 / owner closing and RequestClose all go through CanClose
// (details in [Window Closing Model](Concept-Window-Closing-Model))
protected override bool CanClose(CloseReason reason)
{
    if (_tracker.IsChanged &&
        !Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
    {
        return false;
    }
    return true;
}

// Push direction: Save button
private void OnSaveAndClose()
{
    _repository.Save(_order);
    _tracker.AcceptChanges();              // commit: snapshot = saved state
    RequestClose(_order, InteractionStatus.Ok);
}

// Push direction: Discard button
private void OnDiscard()
{
    _tracker.AcceptChanges();              // stop tracking the discarded edits (not "saved")
    RequestClose(InteractionStatus.Cancel);
}

// Push direction: Close button — same as X: asks when there are unsaved edits
private void OnClose() => RequestClose(InteractionStatus.Cancel);
```

`RequestClose` も X と同じ `CanClose` を通ります。保存 / 破棄のハンドラは `RequestClose` を呼ぶ **前に** ダーティ状態を確定させます (保存なら保存後に、破棄なら破棄した編集を受け入れて `AcceptChanges`)。これにより `CanClose` はクリーンな状態を見て、確認ダイアログを出さずに閉じます。確定しない「閉じる」ボタンは、ダーティなら X と同じ確認を出します。ダーティ状態の確認は `CanClose` だけに置きます。

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — ダーティチェックとの連携
- [ViewAction システム](Reference-ViewAction-System) — `canExecute: () => _tracker.IsChanged` パターン
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — 完全な実装例
- サンプル:
  - `samples/WinformsMVP.Samples/EmailDemo/` — ChangeTracker (使い方 2) + クローズモデルの組み合わせ
  - `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerTests.cs` — テストパターン
  - `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerUsageStyleTests.cs` — 2 つの書き方の動作
  - `samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs` — 書き方 2 + 非同期保存 + 破棄して閉じる
