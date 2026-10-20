# ChangeTracker

`ChangeTracker<T>` は **編集 / キャンセル** シナリオに必要な、堅牢な **変更追跡** を提供します。
`IChangeTracking` と `IRevertibleChangeTracking` の両インターフェイスを実装しており、ベースライン (元の値) との比較・コミット (確定) ・破棄 (元に戻す) を一貫した API で扱えます。

> **位置づけ**: 編集画面の `OnSave` / `OnCancel` ハンドラの実装と、`CanExecute` 判定 (「未保存の変更があるか」) の両方を支える基幹コンポーネントです。

---

## 目次

- [型制約](#型制約)
- [スナップショット (複製) の解決順序](#スナップショット-複製-の解決順序)
- [比較の解決順序](#比較の解決順序)
- [第三者ライブラリの差し込み](#第三者ライブラリの差し込み)
- [項目を比較から外す (画面ごと)](#項目を比較から外す-画面ごと)
- [使い方の最小例](#使い方の最小例)
- [なぜ深いコピーが必要か](#なぜ深いコピーが必要か)
- [主要 API](#主要-api)
- [イベント](#イベント)
- [高度な機能](#高度な機能)
- [関連ページ](#関連ページ)

---

## 型制約

```csharp
public class ChangeTracker<T> where T : class
```

- **`T : class`** — 参照型のみ対応
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

ベースラインを保存するために `T` のコピーを作る必要があります。`TrackChanges(value)` は以下の順で複製戦略を決定します。

1. **`T` ちょうどに登録された `ChangeTrackingPolicy<T>`** (`mvp.ChangeTracking.For<T>(policy)`) → その `Clone`
2. **登録された `IObjectCloner`** (`mvp.ChangeTracking.UseCloner(...)`) → その `DeepClone`
3. **どちらも無い** → 組み込みのリフレクション深いコピー (`BuiltInObjectCloner`)

`new ChangeTracker<T>(value)` は常に 3 (またはコンストラクタで渡したポリシー) を使います。
リフレクション深いコピーは正確ですが速度面では手書きのコピーに劣ります。性能が重要なら検証済みの深いコピーをポリシーで登録してください。

> 組み込みの `ObjectCloner` / 深い比較は、`HashSet` / `SortedSet` / `Queue` / `Stack` / `LinkedList` / `ReadOnlyCollection` を要素単位で扱います (セットの比較子やスタックの順序は維持)。`Type` / `MemberInfo` / `Uri` / `Version` は不変として扱いコピーせずに共有します。コレクションを通る循環参照はコピー側に解決されます (コンストラクタでしか要素を受け取れないコレクションを通る循環のみ `NotSupportedException`)。参照同一性の要素を持つセットは要素の深い等価性で対応付けて比較するため、未変更のディープコピーと等しく判定され、構築直後に dirty になることはありません。

---

## 比較の解決順序

`IsChanged` 判定 (現在値とベースラインの比較) は以下の順で戦略を決定します。

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
- 自動プロパティのほか、`_name` / `m_name` のような手書きのバッキングフィールドを持つプロパティ (`BindableBase.SetProperty(ref _name, value)` など) も対象にできます。
- 新しく追加したプロパティは何もしなくても比較されます (除外漏れで変更を見逃すことがない)。
- メンバーの除外はアプリケーションの型だけに効きます (`System.*` / `Microsoft.*` の型の内部には及びません。例えば `IgnoreMember("Value")` が `int?` の比較を壊すことはありません)。独自の `Equals` を持つ型でも、除外があればフィールド単位で比較します。
- `IgnoreType<T>()` は、`object` 型やインターフェイス型のメンバー・コレクションの要素に入っている値にも効きます。その型の値どうし (別の派生型を含む) や `null` との違いは変更として扱いませんが、まったく別の値 (例えば文字列) に置き換えた場合は変更です。
- `Ignore` の後に変更の有無が変わると、`IsChangedChanged` が発火します。
- この 3 種類 (型のメンバー・メンバー名・型) は、CompareNETObjects や ObjectsComparer などの比較ライブラリが共通して持つ除外方法です。比較エンジンを差し替えた場合、tracker の除外設定は `ChangeTrackingIgnores` として `IObjectContentComparer.ContentEquals` に渡されるので、アダプタでライブラリの設定に対応付けます (上の例)。
- 比較関数を自分で渡した tracker (`new ChangeTracker<T>(value, comparer)`、デリゲートで作った `ChangeTrackingPolicy<T>`) では `Ignore` は `InvalidOperationException` になります。その比較関数の中で除外してください。

## 使い方の最小例

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnInitialize()
    {
        var user = LoadUser(userId);
        _changeTracker = TrackChanges(user);                    // ベースラインのスナップショット
        View.ShowUser(user);                                    // データは View が保持・編集する
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            StandardActions.Save,
            OnSave,
            // tracker は「変わったか」を答えるだけ。現在値は View から渡す。
            // (Save の活性を編集に追随させるには、View の変更時に RaiseCanExecuteChanged() を
            //  呼ぶ配線が別途必要 — [HowTo: 入力検証](HowTo-Validate-Form-Input) 参照)
            canExecute: () => _changeTracker.IsChangedWith(View.GetModel()));

        Dispatcher.Register(StandardActions.Reset, OnReset);
    }

    private void OnSave()
    {
        var model = View.GetModel();              // データは View から取得(tracker からではない)
        SaveUser(model);
        _changeTracker.AcceptChanges(model);      // 保存できた値を新しいベースラインに
    }

    private void OnReset()
    {
        // 編集を破棄してベースラインを再表示する
        View.ShowUser(_changeTracker.GetOriginalValue());
    }
}
```

> **ChangeTracker はデータを持ちません。** データの源は常に View(またはモデル)です。tracker には「ベースライン」と「変わったか」の判定だけを任せ、現在値は `View.GetModel()` のように View から取り出して `IsChangedWith` に渡します。`CurrentValue` プロパティは開発時のデバッグ確認用で、IntelliSense からは隠してあります。

---

## なぜ深いコピーが必要か

浅いコピー (MemberwiseClone) は、参照型プロパティが元のオブジェクトと **同じインスタンスを共有** してしまいます。すると tracker が取るベースラインのスナップショットが元オブジェクトから独立せず、変更検出が壊れます。

### 浅いコピーの問題

```csharp
var user = new UserModel { Address = new Address { City = "Tokyo" } };
var tracker = new ChangeTracker<UserModel>(user);   // ベースラインのスナップショットを取る

// 浅いコピーだと、ベースラインは user と同じ Address インスタンスを共有してしまう。
// その状態で(View 上で)元オブジェクトを編集すると…
user.Address.City = "Osaka";

// ベースラインまで一緒に書き換わるため、変更を検出できない
bool changed = tracker.IsChangedWith(user);   // 浅いコピーだと false(期待: true)
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

### プロパティ

| メンバー | 型 | 説明 |
|---------|----|----|
| `CurrentValue` | `T` | tracker が保持する作業スナップショット (読み取り専用)。**開発時のデバッグ確認用**で、IntelliSense からは隠してある (`[EditorBrowsable(Never)]`)。データの源は View であって tracker ではない |
| `IsChanged` | `bool` | 現在値がベースラインと異なるか (読むたびに比較。詳細は「パフォーマンス」の注記)。ダーティ判定は通常 `IsChangedWith(View の値)` を使う |

### メソッド

| メソッド | 説明 |
|---------|----|
| `UpdateCurrentValue(T value)` | 現在値を更新 (`IsChangedChanged` イベントを発火することがある) |
| `AcceptChanges()` / `AcceptChanges(T value)` | ベースラインを確定。引数版は指定値を新しいベースラインにする(保存後に View の値で呼ぶ) |
| `RejectChanges()` | ベースラインに戻す |
| `IsChangedWith(T value)` | 指定値(通常は View の現在値)がベースラインと異なるか判定。**ダーティ判定はこれを使う** |
| `GetOriginalValue()` | ベースラインのコピーを取得 |

---

## イベント

| イベント | 発火タイミング |
|---------|--------------|
| `IsChangedChanged` | `IsChanged` 状態が変わったとき |

UI への通知 (ボタンの Enabled 更新等) に使えます。

```csharp
_changeTracker.IsChangedChanged += (s, e) =>
{
    Dispatcher.RaiseCanExecuteChanged();   // Save ボタン等を再評価
};
```

---

## 高度な機能

### 1. スレッドセーフ

すべての操作は内部でロック保護されており、マルチスレッド環境で安全に使用できます。
非同期な業務処理 (`async/await`) からも問題なく `UpdateCurrentValue` 等を呼べます。

### 2. パフォーマンス

`IsChanged` は結果をキャッシュせず、**読むたびに現在値とベースラインを比較** します。そのため `CurrentValue` のオブジェクトをインプレースで編集した場合も検出されます (以前のバージョンはキャッシュしていたため、`AcceptChanges` / `RejectChanges` / `UpdateCurrentValue` を呼ぶまで反映されませんでした)。深い比較のコストは読むたびにかかるので、頻繁なポーリングは避けてください。

> ⚠️ `ChangeTracker` はあなたのデータを所有せず `INotifyPropertyChanged` も購読しないため、**View 側にしか無い編集値**は `IsChanged` からは見えません。**ダーティ判定は現在値を渡して比較する `IsChangedWith(View.GetModel())` で行ってください**。Save / ウィンドウクローズのような一発判定に最適で、本ページの例もこの形です。
>
> `IsChangedChanged` が発火するのは `AcceptChanges` / `RejectChanges` / `UpdateCurrentValue` で状態が変わったときだけです (インプレース編集では発火しません。イベントは内部ロックの外で発火するので、ハンドラから tracker を呼んでもデッドロックしません)。イベント駆動 UI で通知が欲しい場合は、View 変更時に `UpdateCurrentValue(View.GetModel())` を呼んでください (実例 `samples/.../EmailDemo/ComposeEmailPresenter.cs`)。それでもデータの源は View です。

### 3. ウィンドウクローズとの連携

ウィンドウクローズ時のダーティチェックに使うのが典型パターンです (一発判定なので実時比較 `IsChangedWith` を使う)。

```csharp
// Pull direction: override CanClose (details in [Window Closing Model](Concept-Window-Closing-Model))
protected override bool CanClose(CloseReason reason)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
        return true;

    if (_changeTracker.IsChangedWith(View.GetModel()) &&
        !Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
    {
        return false;
    }
    return true;
}

// Push direction: Save button
private void OnSave()
{
    var model = View.GetModel();
    SaveData(model);
    _changeTracker.AcceptChanges(model);   // commit: baseline = saved value
    RequestClose(result, InteractionStatus.Ok);
}
```

`AcceptChanges(model)` の後に `RequestClose` を呼ぶことで、ベースラインが保存値に更新されます。続いてフレームワークが `CanClose` を呼んでも `IsChangedWith(View.GetModel())` が `false` を返し、再確認ダイアログが出ません。(この順序は推奨ですが、フレームワーク構造上 Push 起点の閉じは `CanClose` をスキップするため、二重確認が出ない保証は `AcceptChanges` の呼び出し順序に依存しません。)

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — ダーティチェックとの連携
- [ViewAction システム](Reference-ViewAction-System) — `CanExecute: () => _changeTracker.IsChangedWith(...)` パターン
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — 完全な実装例
- サンプル:
  - `samples/WinformsMVP.Samples/WindowClosingDemo/` — ChangeTracker + クローズモデルの組み合わせ
  - `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerTests.cs` — テストパターン
