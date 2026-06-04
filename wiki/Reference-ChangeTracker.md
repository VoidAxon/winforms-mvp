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
- **`ICloneable` の実装は任意** — 推奨はされるが必須ではない。未実装でも動作する
- **`IEquatable<T>` 等の実装も任意** — フォールバックでリフレクション深い比較に切り替わる

---

## スナップショット (複製) の解決順序

ベースラインを保存するために `T` のコピーを作る必要があります。`ChangeTracker<T>` は以下の順で複製戦略を決定します。

1. **`T` が `ICloneable` を実装** → その `Clone()` を使用 (高速パス、推奨)
2. **未実装** → グローバルフック `ChangeTrackerDefaults.Cloner` を使用 (デフォルトは組み込みリフレクション深いコピー `ObjectCloner`)

リフレクション深いコピーは正確だが速度面では `ICloneable` 実装に劣ります。性能が重要なら `ICloneable` を実装してください。

---

## 比較の解決順序

`IsChanged` 判定 (現在値とベースラインの比較) は以下の順で戦略を決定します。

1. **コンストラクタに `comparer` を渡した** → それを使用
2. **`T` が値等価性を持つ** (`IEquatable<T>` / `IComparable<T>` / `Equals` オーバーライド) → それを使用
3. **いずれもない** → グローバルフック `ChangeTrackerDefaults.Comparer` (デフォルトはリフレクション深い比較)

> **動作変更の注意**: 旧バージョンでは `Equals` を override していないモデルは構築直後に `IsChanged == true` (参照比較によるバグ) でしたが、現在はリフレクション深い比較により正しく判定されます。`ICloneable` + `Equals` を実装済みのモデルは挙動不変です。

---

## 第三者ライブラリの差し込み

アプリ起動時に 1 回だけ、グローバルフックを差し替えることで複製・比較戦略を上書きできます。

```csharp
// 例: Force.DeepCloner を使う
ChangeTrackerDefaults.Cloner = obj => obj.DeepClone();

// 例: 自前の高速比較器を使う
ChangeTrackerDefaults.Comparer = new MyFastComparer();
```

通常はデフォルト (リフレクション深い処理) で十分ですが、ホットパスでパフォーマンスが問題になる場合に検討してください。

---

## 使い方の最小例

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnInitialize()
    {
        var user = LoadUser(userId);
        _changeTracker = new ChangeTracker<UserModel>(user);   // ベースラインのスナップショット
        View.ShowUser(user);                                    // データは View が保持・編集する
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            CommonActions.Save,
            OnSave,
            // tracker は「変わったか」を答えるだけ。現在値は View から渡す。
            // (Save の活性を編集に追随させるには、View の変更時に RaiseCanExecuteChanged() を
            //  呼ぶ配線が別途必要 — [HowTo: 入力検証](HowTo-Validate-Form-Input) 参照)
            canExecute: () => _changeTracker.IsChangedWith(View.GetModel()));

        Dispatcher.Register(CommonActions.Reset, OnReset);
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

### 正しい `Clone()` の実装

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

リフレクション深いコピー (`ICloneable` 未実装時のフォールバック) を使う場合、上記の罠は **自動的に回避されます** が、パフォーマンスは劣ります。

---

## 主要 API

### プロパティ

| メンバー | 型 | 説明 |
|---------|----|----|
| `CurrentValue` | `T` | tracker が保持する作業スナップショット (読み取り専用)。**開発時のデバッグ確認用**で、IntelliSense からは隠してある (`[EditorBrowsable(Never)]`)。データの源は View であって tracker ではない |
| `IsChanged` | `bool` | 現在値がベースラインと異なるか (結果キャッシュあり。詳細は「パフォーマンス最適化」の注記)。ダーティ判定は通常 `IsChangedWith(View の値)` を使う |

### メソッド

| メソッド | 説明 |
|---------|----|
| `UpdateCurrentValue(T value)` | 現在値を更新 (`IsChangedChanged` イベントを発火することがある) |
| `AcceptChanges()` | 現在値を新しいベースラインとして確定 |
| `RejectChanges()` | ベースラインに戻す |
| `IsChangedWith(T value)` | 指定された値がベースラインと異なるか判定 |
| `GetOriginalValue()` | ベースラインのコピーを取得 |
| `CanAcceptChanges(out string error)` | 変更を確定可能かチェック (派生クラスでオーバーライドして検証ロジックを差し込む) |
| `CanRejectChanges(out string error)` | 変更を破棄可能かチェック (同上) |

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

### 2. パフォーマンス最適化

`IsChanged` プロパティは結果をキャッシュし、頻繁にアクセスしても比較コストは累積しません。

> ⚠️ **`IsChanged`(キャッシュ版)が再計算されるのは `AcceptChanges` / `RejectChanges` / `UpdateCurrentValue` を呼んだときだけです。** `ChangeTracker` はあなたのデータを所有せず `INotifyPropertyChanged` も購読しないため、View 側で編集された値を `IsChanged` は感知できません。**ダーティ判定は現在値を渡して実時比較する `IsChangedWith(View.GetModel())` で行ってください**(キャッシュを読まない)。Save / ウィンドウクローズのような一発判定に最適で、本ページの例もこの形です。
>
> tracker に現在値を反映させたい場合 (イベント駆動 UI 等) は、View 変更時に `UpdateCurrentValue(View.GetModel())` を呼べばキャッシュ `IsChanged` が保たれ `IsChangedChanged` も発火します (実例 `samples/.../EmailDemo/ComposeEmailPresenter.cs`)。それでもデータの源は View です。

### 3. 検証サポート (派生クラス)

`CanAcceptChanges` / `CanRejectChanges` をオーバーライドすると、確定・破棄前の追加検証を挟めます。

```csharp
public class ValidatedChangeTracker<T> : ChangeTracker<T> where T : class
{
    public ValidatedChangeTracker(T initialValue) : base(initialValue) { }

    public override bool CanAcceptChanges(out string error)
    {
        if (CurrentValue is IValidatable validatable && !validatable.IsValid)
        {
            error = "Validation failed";
            return false;
        }
        error = null;
        return true;
    }
}
```

### 4. ウィンドウクローズとの連携

ウィンドウクローズ時のダーティチェックに使うのが典型パターンです (一発判定なので実時比較 `IsChangedWith` を使う)。

```csharp
// Pull 方向のクローズハンドラ (詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model))
private void OnViewClosing(object sender, WindowClosingEventArgs args)
{
    if (args.Reason == CloseReason.SystemShutdown) return;

    if (_changeTracker.IsChangedWith(View.GetModel()) &&
        !Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
    {
        args.Cancel = true;
    }
}

// Push 方向 (Save ボタン)
private void OnSave()
{
    var model = View.GetModel();
    SaveData(model);
    _changeTracker.AcceptChanges(model);                  // ← Close 前に確定(保存値を新ベースラインに)
    RaiseClose(result, InteractionStatus.Ok);
}
```

`OnSave` で `AcceptChanges(model)` を呼んでから `RaiseClose` する順序は重要です。ベースラインが保存値に更新されるため、その後にフレームワークが Pull 方向のハンドラを呼んでも `IsChangedWith(View.GetModel())` が `false` を返し、再確認ダイアログが出ません。

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — ダーティチェックとの連携
- [ViewAction システム](Reference-ViewAction-System) — `CanExecute: () => _changeTracker.IsChangedWith(...)` パターン
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — 完全な実装例
- サンプル:
  - `samples/WinformsMVP.Samples/WindowClosingDemo/` — ChangeTracker + クローズモデルの組み合わせ
  - `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerTests.cs` — テストパターン
