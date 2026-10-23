# WindowNavigator

`IWindowNavigator` は **ウィンドウのライフサイクル管理** を担うサービスです。Modal/Non-Modal の表示、パラメータの受け渡し、結果の取得、シングルトンウィンドウ管理までを 1 つのインターフェイスでカバーします。

> **位置づけ**: Presenter 起点でウィンドウを開きたいとき、必ずこの `WindowNavigator` を経由します。`Form` インスタンスを `new` して `.Show()` を呼ぶことはありません。

---

## 推奨の書き方 (Fluent)

ウィンドウを開くときは、まず **Fluent 形式** を使ってください。Presenter とパラメータの型は引数から推論され、書くのは **結果の型だけ** です。パラメータの型が Presenter に合わなければコンパイルエラーになります。

```csharp
// モーダル: 結果を受け取る
var result = Navigator.For(new CustomerSearchPresenter(_repository))
                      .WithParam(keyword)
                      .ShowAsModal<Customer>();
if (result.IsOk) Show(result.Value);

// モーダル: パラメータなし
var picked = Navigator.For(new StaffPickerPresenter(_masters)).ShowAsModal<Staff>();

// 非モーダル: このウィンドウに所属させ、同じ顧客は 1 枚だけ、閉じたら結果を受け取る
Navigator.For(new CustomerEditPresenter(_repository))
         .WithParam(new CustomerEditParameters { CustomerId = id })
         .ShowWindow<int>(owner: View,
             keySelector: _ => id,
             onClosed: r => { if (r.IsOk) Reload(); });
```

- モーダルは閉じるまで戻らず、`InteractionResult<T>` (`IsOk` / `IsCancelled` / `IsError`、`Value`) を返します。非モーダルはすぐ戻り、結果は `onClosed` で受け取ります。
- Presenter が初期化中 (`OnInitialize` や `OnInitializeAsync` の最初の `await` まで) に `RequestClose` を呼ぶと、表示がキャンセルされます: `Show` / `ShowDialog` は呼ばれず、モーダルはその結果を返し、非モーダルは `null` を返します ([表示前のキャンセル](#表示前のキャンセル))。
- `keySelector` が返すキーのウィンドウがすでに開いていれば、そのウィンドウをアクティブにします。このとき新しい Presenter は破棄され、その `onClosed` は登録されません。キーは Presenter の型ごとに分かれています (内部では `(Presenter の実行時の型, キー)`)。別の画面が同じレコード ID をキーにしても、互いのウィンドウを開いてしまうことはありません。同じ Presenter 型 + 同じキーのときだけ既存のウィンドウを使います (Presenter の派生クラスは別の型として扱われます)。
- `owner` を渡すと、そのウィンドウに所属します (常に手前に表示され、オーナーと一緒に最小化・クローズされます。オーナーを閉じると、所属するウィンドウの `CanClose` が `ParentClosing` で呼ばれます)。

`ShowWindowAsModal<TPresenter, TParam, TResult>(...)` などの直接呼び出し形式も同じ機能で、以下のリファレンスはこちらで説明しています。

---

## 目次

- [推奨の書き方 (Fluent)](#推奨の書き方-fluent)
- [基本コンセプト](#基本コンセプト)
- [Modal ウィンドウ](#modal-ウィンドウ)
- [Non-Modal ウィンドウ](#non-modal-ウィンドウ)
- [シングルトンウィンドウ](#シングルトンウィンドウ)
- [Fluent API: TPresenter 型推論](#fluent-api-tpresenter-型推論)
- [パラメータ vs DI: 役割分担](#パラメータ-vs-di-役割分担)
- [結果の受け取り (RequestClose)](#結果の受け取り-requestclose)
- [Presenter からのアクセス](#presenter-からのアクセス)
- [テスト時のモック](#テスト時のモック)
- [関連ページ](#関連ページ)

---

## 基本コンセプト

`WindowNavigator` は以下のサービスと協調します。

```
WindowNavigator
   ├── IViewMappingRegister      (View インターフェイス → Form クラスの解決)
   └── IPresenter                (起動対象の Presenter インスタンス)
```

> ウィンドウクローズは `WindowLifecycleController` が管理します (1 Form に 1 インスタンス)。X / Alt+F4 / オーナーのクローズ (`FormClosing`) も、基底 `RequestClose(...)` (クローズシンク経由) も、同じコントローラでプレゼンターの `CanClose` / `CanCloseAsync` ゲートを通ります (`RequestClose` は `CloseReason.Normal`。システムシャットダウン / タスクマネージャは問い合わせずに許可)。`RequestClose` の結果はそのクローズの候補で、ゲートが拒否すれば捨てられます。表示前の `RequestClose` はゲートを通らず、表示をキャンセルします。Forms に閉じるコードは不要です。

`IViewMappingRegister` の構成方法は [ViewMappingRegister](Reference-ViewMappingRegister) を、結果の伝達設計は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

---

## Modal ウィンドウ

### パラメータなし・結果なし

```csharp
var presenter = new AboutDialogPresenter();
navigator.ShowWindowAsModal(presenter);
```

### パラメータなし・結果あり

```csharp
var presenter = new InputDialogPresenter();
InteractionResult<string> result =
    navigator.ShowWindowAsModal<InputDialogPresenter, string>(presenter);

if (result.IsOk)
{
    var input = result.Value;
    // ...
}
else if (result.IsCancelled)
{
    // ユーザーがキャンセル
}
```

### パラメータあり・結果なし

```csharp
var presenter = new EditUserPresenter();
var parameters = new EditUserParameters { UserId = 123 };
navigator.ShowWindowAsModal<EditUserPresenter, EditUserParameters>(presenter, parameters);
```

### パラメータあり・結果あり

```csharp
var presenter = new EditUserPresenter();
var parameters = new EditUserParameters { UserId = 123 };

InteractionResult<UserResult> result =
    navigator.ShowWindowAsModal<EditUserPresenter, EditUserParameters, UserResult>(
        presenter, parameters);

if (result.IsOk)
{
    var updated = result.Value;
}
```

3 つの型引数 (`TPresenter, TParam, TResult`) を毎回書くのは煩雑なので、**[Fluent API](#fluent-api-tpresenter-型推論)** を推奨します。

---

## Non-Modal ウィンドウ

ブロックせずにウィンドウを表示する場合は `ShowWindow` を使います。

### パラメータなし

```csharp
var presenter = new DocumentPresenter();
var window = navigator.ShowWindow(presenter);
```

### パラメータあり

```csharp
var presenter = new DocumentPresenter();
var parameters = new DocumentParameters { DocumentId = "doc-001" };
var window = navigator.ShowWindow<DocumentPresenter, DocumentParameters>(presenter, parameters);
```

### 閉じたときのコールバック

```csharp
navigator.ShowWindow<DocumentPresenter, EditResult>(
    presenter,
    onClosed: result =>
    {
        if (result.IsOk)
            ReloadList();
    });
```

### 表示前のキャンセル

Presenter が初期化中 (`OnInitialize`、`OnInitializeAsync` の同期部分) に `RequestClose` を呼ぶと、ウィンドウは表示されません (ゲート `CanClose` も呼ばれません)。

- モーダル (`ShowWindowAsModal` / `ShowAsModal`): `ShowDialog` を呼ばずに、`RequestClose` の結果を `InteractionResult<TResult>` として返します。
- 非モーダル (`ShowWindow`): `Show` を呼ばずに **`null`** を返します。戻る前に、キーの登録を解除 → `onClosed` を 1 回呼ぶ → Presenter と Form を破棄、の順で処理するので、`onClosed` の中で同じキーのウィンドウを開き直せます。戻り値を使う場合は `null` を確認してください。

---

## シングルトンウィンドウ

同じキーに対して **常に同じウィンドウインスタンス** を返したい場合は、`keySelector` を指定します。ドキュメントエディタ等で、同じドキュメントに対して 2 つのウィンドウが開かないようにしたいときに便利です。

```csharp
navigator.ShowWindow<DocumentPresenter>(
    presenter,
    keySelector: p => p.DocumentId);
```

挙動:

1. 内部辞書に `DocumentId` のエントリがあれば、既存ウィンドウを `Activate()` して前面に出す
2. なければ新規ウィンドウを生成して、辞書に登録
3. ウィンドウが閉じたら自動的に辞書から削除 (表示前にキャンセルされた場合も、`onClosed` の前に削除)

---

## Fluent API: TPresenter 型推論

C# は部分的なジェネリック型推論をサポートしないため、3 引数版の `ShowWindowAsModal<TPresenter, TParam, TResult>` ではすべての型引数を明示する必要があります。
これを軽減するのが **Fluent API** です。

### 比較

```csharp
// 3 引数形式 — すべての型引数を明示
var result = Navigator.ShowWindowAsModal<ComposeEmailPresenter, ComposeEmailParameters, bool>(
    presenter, parameters);

// Fluent 形式 — TResult だけ明示
var result = Navigator.For(presenter)            // TPresenter は引数から推論
                      .WithParam(parameters)     // TParam は引数から推論
                      .ShowAsModal<bool>();      // TResult だけ明示
```

### 各ステップの動作

| ステップ | 型推論 | コンパイル時制約 |
|---------|------|---------------|
| `.For(presenter)` | `TPresenter` | `TPresenter : IPresenter` |
| `.WithParam(parameters)` | `TParam` | `TPresenter : IInitializable<TParam>` |
| `.ShowAsModal<TResult>()` | (明示) | (なし) |
| `.ShowAsModal()` | — | (なし) |
| `.ShowWindow(...)` / `.ShowWindow<TResult>(...)` | — | (なし) |

`WithParam` 拡張メソッドは **より厳しい制約** を追加します。`NavigationContext<TPresenter>` 自体は `IPresenter` だけを要求しますが、`WithParam(parameters)` を呼ぶには Presenter が `IInitializable<TParam>` を実装している必要があります。型不一致は **コンパイル時に検出されます**:

```csharp
// FooPresenter は IInitializable<int> を実装していない
Navigator.For(fooPresenter).WithParam(42);
// ❌ コンパイルエラー: 'FooPresenter' does not satisfy IInitializable<int>
```

逆方向 (パラメータが必要な Presenter をパラメータなしで表示) は実行時に検出されます。`IInitializable<TParam>` だけを実装する Presenter (`WindowPresenterBase<TView, TParam>` 派生) を `ShowAsModal()` / `ShowWindow()` などパラメータなしのオーバーロードで表示すると、ウィンドウを作る前に `InvalidOperationException` が送出されます (以前は未初期化のままウィンドウが開いていました)。

### 全 4 バリエーション

```csharp
// Modal、パラメータなし、結果なし
Navigator.For(presenter).ShowAsModal();

// Modal、パラメータなし、結果あり
var name = Navigator.For(presenter).ShowAsModal<string>();

// Modal、パラメータあり、結果なし
Navigator.For(presenter).WithParam(parameters).ShowAsModal();

// Modal、パラメータあり、結果あり
var ok = Navigator.For(presenter).WithParam(parameters).ShowAsModal<bool>();

// Non-Modal (シングルトン・コールバック対応)
Navigator.For(presenter).ShowWindow<bool>(
    keySelector: p => p.DocumentId,
    onClosed: result => { /* ... */ });

// Non-Modal、パラメータあり
Navigator.For(presenter).WithParam(parameters).ShowWindow();
```

### 3 引数形式と Fluent 形式の併存

両形式は完全にサポートされており、内部的には同じインスタンスメソッドに委譲されます。Fluent API は **純粋な追加機能** で、既存の呼び出し箇所・`FakeWindowNavigator`・テストは無修正で動作します。

| 観点 | 3 引数形式 | Fluent 形式 |
|------|----------|------------|
| 明示すべき型引数 | すべて (`<TP, TParam, TResult>`) | `<TResult>` のみ (または無し) |
| `IInitializable<TParam>` チェック | コンパイル時 | コンパイル時 |
| `keySelector` の型 | `TPresenter` で型強い | `TPresenter` で型強い |
| 既存テスト/モックへの影響 | — | なし (同じインスタンスメソッドに委譲) |
| リフレクション/実行時キャスト | なし | なし |

実装: [`WindowNavigatorFluentExtensions.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/src/WinformsMVP.Core/Services/WindowNavigatorFluentExtensions.cs)、[`NavigationContext.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/src/WinformsMVP.Core/Services/NavigationContext.cs)。

---

## パラメータ vs DI: 役割分担

`WithParam(parameters)` と DI コンテナのコンストラクタ注入は、扱う情報の性質が異なります。**両者を混ぜないでください**。

| コンストラクタ (DI 管理) | `Initialize(TParam)` (Navigator 管理) |
|-------------------------|--------------------------------------|
| 安定した依存: Repository、Service、Logger | ランタイムデータ: ID、パス、モード、コンテキスト |
| コンテナのライフタイム単位で 1 つ | ウィンドウを開くたびに違う |
| `IPresenterFactory.Create<T>()` で解決される | `Navigator.WithParam(...)` で渡される |

### 例: 良い分け方

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>
{
    private readonly IUserRepository _repository;   // ← DI 注入 (毎回同じ)
    private readonly ILogger _logger;               // ← DI 注入

    public EditUserPresenter(IUserRepository repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    protected override void OnInitialize(EditUserParameters parameters)
    {
        // parameters.UserId は呼び出しごとに違う ← Navigator から
        var user = _repository.GetById(parameters.UserId);
        View.Bind(user);
    }
}

// 呼び出し側
var presenter = _presenterFactory.Create<EditUserPresenter>();   // ← DI
var parameters = new EditUserParameters { UserId = userId };     // ← ランタイム
Navigator.For(presenter).WithParam(parameters).ShowAsModal<UserResult>();
```

### 例: 悪い分け方 (避けるべき)

```csharp
// ❌ DI で取れる依存とランタイムデータをコンストラクタに混在
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    public EditUserPresenter(IUserRepository repository, int userId, bool isReadOnly) { ... }
    //                                              ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    //                                              ランタイムデータをここに混ぜると DI コンテナが解決できなくなる
}
```

詳細は [Dependency Injection](Reference-DependencyInjection) の `IPresenterFactory` セクションを参照してください。

---

## 結果の受け取り (RequestClose)

業務結果を呼び出し元に返したい Presenter は、基底 `RequestClose(result, status)` を呼ぶだけです — インターフェイスの実装も拡張メソッドも不要。`TResult` は引数から推論されます:

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>
{
    private void OnSave()
    {
        SaveUser(View.UserName);
        RequestClose(new UserResult { UserId = _userId }, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RequestClose(InteractionStatus.Cancel);
}
```

開いているウィンドウへの `RequestClose` は、X と同じく `CanClose` / `CanCloseAsync` を通ります。ゲートが許可したときだけ、渡した結果が呼び出し元に届きます (拒否されればウィンドウは開いたままで、結果は捨てられます)。保存後に確認を出さずに閉じるには、`RequestClose` の前にダーティ状態を確定させます ([ウィンドウクローズモデル](Concept-Window-Closing-Model#単一正本の不変条件))。

`InteractionResult<TResult>` の主な API:

| プロパティ/メソッド | 用途 |
|------------------|----|
| `IsOk` | Ok ステータスで `Value` あり |
| `IsCancelled` | ユーザーがキャンセル (Cancel ステータス、または × で閉じた) |
| `IsError` | 業務側でエラー (Error ステータス、`ErrorMessage` あり) |
| `Value` | `IsOk` のときの結果値 |
| `ErrorMessage` | `IsError` のときのメッセージ |

完全な設計詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

### エラーの2分類: 業務エラー vs 設定エラー

`InteractionResult.IsError` は **業務側のエラー** を表します。これは次の場合に発生します:

- Presenter 自身が `RequestClose(result, InteractionStatus.Error)` を呼んだとき。`result` が `string` / `Exception` ならそれが `ErrorMessage` / `Exception` に入ります (例: `RequestClose("保存に失敗しました", InteractionStatus.Error)`)。
- `Ok` で閉じたが、結果の型が呼び出し元の `TResult` と合わないとき、または `TResult` が null 非許容の値型なのに結果がないとき (以前は `Ok(default)` になっていました)。

一方、**ビュー解決・設定の誤り** は `IsError` では返らず、**例外として送出されます** (コードベースの規約: 設定/プログラミングミスは握りつぶさず大きく失敗させる):

| 失敗 | 送出される例外 |
|------|--------------|
| ビューインターフェイスに実装が未登録 | `KeyNotFoundException` |
| 登録された実装が `Form` でない / `IWindowView` 未実装 / Presenter が `IViewAttacher<TView>` 未実装 / ウィンドウ Presenter でない | `InvalidOperationException` |
| パラメータが必要な Presenter をパラメータなしのオーバーロードで表示 | `InvalidOperationException` |

> いずれの場合も、ウィンドウは表示され得ないため Presenter (と生成済みなら Form) は送出前に Dispose されます。これらは起動時の配線ミスであり、`ShowDialog`/`Show` に到達する前に投げられます。`IsError` を待つコードではなく、`ViewMappingRegister` への登録を正しく行うことで防いでください。

---

## Presenter からのアクセス

Presenter からは `Navigator` プロパティで `IWindowNavigator` にアクセスできます (コンストラクタ注入は不要)。

```csharp
public class MainPresenter : WindowPresenterBase<IMainView>
{
    private void OnOpenEditor()
    {
        var presenter = new EditUserPresenter();
        var parameters = new EditUserParameters { UserId = View.SelectedUserId };

        var result = Navigator.For(presenter)
                              .WithParam(parameters)
                              .ShowAsModal<UserResult>();

        if (result.IsOk)
            ReloadList();
    }
}
```

DI コンテナと連携する場合、Presenter 自身も子 Presenter を DI で解決したいことがあります。その場合は `IPresenterFactory` をコンストラクタ注入してください ([Dependency Injection](Reference-DependencyInjection) 参照)。

---

## テスト時のモック

`WindowNavigator` を本物で動かすと WinForms フォームが立ち上がってしまうため、Presenter のテストでは **`FakeWindowNavigator`** (`WinformsMVP.Testing` パッケージ) に差し替えます。

`TestServices.CreateFakes()` が登録済みで、`fakes.Navigator` として公開されています。本番の `WindowNavigator` と同じ規則 (結果の型変換、キーの重複判定、Presenter の破棄、パラメータ必須の Presenter をパラメータなしで開くと例外) で動きます。渡された子 Presenter はアタッチも初期化もされません — 子画面自体は、別のインスタンスを `PresenterTestHost` でテストします。

```csharp
[Fact]
public async Task OnCompose_OpensComposeWindow()
{
    // Arrange — モーダルの答えを Presenter の型ごとに登録する
    var fakes = TestServices.CreateFakes();
    fakes.Navigator.RespondTo<ComposeEmailPresenter, bool>(_ => InteractionResult<bool>.Cancel());   // 送信せずに閉じた

    PresenterTestHost.Start(fakes.Provider, new MainEmailPresenter(mockRepository), mockView);   // アタッチ + 実際の初期化

    // Act — 実際のエントリポイント (Dispatch) で駆動する (非同期ハンドラなので完了を待つ)
    await mockView.Binder.DispatchAsync(EmailActions.Compose);

    // Assert — モーダルが 1 回開かれ、正しいパラメータが渡されたか
    Assert.Single(fakes.Navigator.Calls);

    var parameters = fakes.Navigator.LastModalParameters<ComposeEmailParameters>();
    Assert.Equal(ComposeMode.New, parameters.Mode);
}
```

`FakeWindowNavigator` の主なメンバー:

| メンバー | 用途 |
|---------|----|
| `RespondTo<TPresenter, TResult>(r => InteractionResult<TResult>…)` | 結果あり版モーダル (`ShowWindowAsModal<…, TResult>` / `ShowAsModal<TResult>()`) の答え。`r` は `ModalRequest` (`Presenter` / `Parameters` / `HasParameters` / `Owner`)。`TPresenter` の派生型にも一致し、後から登録したものが優先。呼び出し側の期待と違う型の値は本番と同じく `Error` |
| `RespondTo<TPresenter>(r => InteractionResult…)` | 結果なし版モーダル (`ShowWindowAsModal<TPresenter>`) の答え |
| `CancelUnscripted()` | 答えのないモーダルを `Cancel` にする (既定では `UnscriptedInteractionException`) |
| `LastModalParameters<T>()` | 最後のモーダル要求に渡されたパラメータ |
| `Windows` (`FakeWindow` の一覧) | 非モーダルで開いたウィンドウ。`CloseWithResult(value[, status])` / `Close()` (X = `Cancel`) / `CloseWithError(message[, exception])` で閉じると、呼び出し側の `onClosed` に本番と同じ形で結果が届く。`IsOpen` / `Key` / `Owner` / `Parameters` / `Activations` |
| `Calls` (`NavigationCall` の一覧) | すべての要求 (`PresenterType` / `Modal` / `Parameters` / `ResultType` / `Owner` / `Key` / `Activated`) |

モーダルの Presenter は答えを返した後に破棄されます (本番でウィンドウが閉じたときと同じ)。非モーダルで同じキーを再度開くと、既存のウィンドウがアクティブになり (`Activations` が増える)、新しい Presenter は破棄されます。

実際の使用例は [`MainEmailPresenterTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/WinformsMVP.Samples.Tests/Presenters/MainEmailPresenterTests.cs) を、詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## 関連ページ

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Navigator` プロパティの位置づけ
- [ViewMappingRegister](Reference-ViewMappingRegister) — Navigator が依存する View 解決の仕組み
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — `RequestClose` / `CanClose` と `InteractionResult<TResult>` の詳細
- [Dependency Injection](Reference-DependencyInjection) — `IPresenterFactory` で子 Presenter を DI 経由で解決する方法
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — ダーティチェック・保存確認の実装
- サンプル:
  - `samples/WinformsMVP.Samples/NavigatorDemo/` — Modal / Non-Modal / シングルトン
  - `samples/WinformsMVP.Samples/WindowClosingDemo/` — `RequestClose` / `CanClose` の完全例
