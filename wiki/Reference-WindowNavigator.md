# WindowNavigator

`IWindowNavigator` は **ウィンドウのライフサイクル管理** を担うサービスです。Modal/Non-Modal の表示、パラメータの受け渡し、結果の取得、シングルトンウィンドウ管理までを 1 つのインターフェイスでカバーします。

> **位置づけ**: Presenter 起点でウィンドウを開きたいとき、必ずこの `WindowNavigator` を経由します。`Form` インスタンスを `new` して `.Show()` を呼ぶことはありません。

---

## 目次

- [基本コンセプト](#基本コンセプト)
- [Modal ウィンドウ](#modal-ウィンドウ)
- [Non-Modal ウィンドウ](#non-modal-ウィンドウ)
- [シングルトンウィンドウ](#シングルトンウィンドウ)
- [Fluent API: TPresenter 型推論](#fluent-api-tpresenter-型推論)
- [パラメータ vs DI: 役割分担](#パラメータ-vs-di-役割分担)
- [結果の受け取り (IRequestClose 連携)](#結果の受け取り-irequestclose-連携)
- [Presenter からのアクセス](#presenter-からのアクセス)
- [テスト時のモック](#テスト時のモック)
- [関連ページ](#関連ページ)

---

## 基本コンセプト

`WindowNavigator` は以下のサービスと協調します。

```
WindowNavigator
   ├── IViewMappingRegister      (View インターフェイス → Form クラスの解決)
   ├── IPresenter                (起動対象の Presenter インスタンス)
   ├── IRequestClose<TResult>    (結果を返す Presenter なら追加で実装)
   └── IWindowClosingBridge      (FormClosing → IWindowView.Closing のブリッジ)
```

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

if (result.IsSuccess)
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

if (result.IsSuccess)
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
        if (result.IsSuccess)
            ReloadList();
    });
```

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
3. ウィンドウが閉じたら自動的に辞書から削除

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

両形式は完全にサポートされており、内部的には同じインスタンスメソッドに委譲されます。Fluent API は **純粋な追加機能** で、既存の呼び出し箇所・`MockWindowNavigator`・テストは無修正で動作します。

| 観点 | 3 引数形式 | Fluent 形式 |
|------|----------|------------|
| 明示すべき型引数 | すべて (`<TP, TParam, TResult>`) | `<TResult>` のみ (または無し) |
| `IInitializable<TParam>` チェック | コンパイル時 | コンパイル時 |
| `keySelector` の型 | `TPresenter` で型強い | `TPresenter` で型強い |
| 既存テスト/モックへの影響 | — | なし (同じインスタンスメソッドに委譲) |
| リフレクション/実行時キャスト | なし | なし |

実装: [`WindowNavigatorFluentExtensions.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/src/WinformsMVP/Services/WindowNavigatorFluentExtensions.cs)、[`NavigationContext.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/src/WinformsMVP/Services/NavigationContext.cs)。

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

## 結果の受け取り (IRequestClose 連携)

業務結果を呼び出し元に返したい Presenter は `IRequestClose<TResult>` を実装します。Navigator はその `CloseRequested` イベントを購読し、`InteractionResult<TResult>` として呼び出し元に返します。

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>,
                                  IRequestClose<UserResult>
{
    public event EventHandler<CloseRequestedEventArgs<UserResult>> CloseRequested;

    private void OnSave()
    {
        SaveUser(View.UserName);
        RaiseClose(new UserResult { UserId = ... }, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RaiseClose(null, InteractionStatus.Cancel);

    private void RaiseClose(UserResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserResult>(result, status));
}
```

`InteractionResult<TResult>` の主な API:

| プロパティ/メソッド | 用途 |
|------------------|----|
| `IsSuccess` | Ok ステータスで `Value` あり |
| `IsCancelled` | ユーザーがキャンセル (Cancel ステータス、または Pull 方向で × でブロック) |
| `IsError` | 業務側でエラー (Error ステータス、`ErrorMessage` あり) |
| `Value` | `IsSuccess` のときの結果値 |
| `ErrorMessage` | `IsError` のときのメッセージ |

完全な設計詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

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

        if (result.IsSuccess)
            ReloadList();
    }
}
```

DI コンテナと連携する場合、Presenter 自身も子 Presenter を DI で解決したいことがあります。その場合は `IPresenterFactory` をコンストラクタ注入してください ([Dependency Injection](Reference-DependencyInjection) 参照)。

---

## テスト時のモック

`WindowNavigator` を本物で動かすと WinForms フォームが立ち上がってしまうため、Presenter のテストでは **`MockWindowNavigator`** に差し替えます。

```csharp
[Fact]
public void OnOpenEditor_ShowsModalDialog()
{
    var mockNavigator = new MockWindowNavigator();
    mockNavigator.SetupModalResult<EditUserPresenter, EditUserParameters, UserResult>(
        result: InteractionResult<UserResult>.Ok(new UserResult { UserId = 1 }));

    var platform = new MockPlatformServices(windowNavigator: mockNavigator);
    var presenter = new MainPresenter()
        .WithPlatformServices(platform);

    presenter.AttachView(mockView);
    presenter.Initialize();
    presenter.Dispatcher.Dispatch(MainActions.OpenEditor);

    Assert.True(mockNavigator.ShownPresenters.Any(p => p is EditUserPresenter));
}
```

`MockWindowNavigator` の用意するメソッド:

| メソッド | 用途 |
|---------|----|
| `SetupModalResult<TP, TParam, TR>(result)` | 特定の Modal 呼び出しに対する結果を事前に設定 |
| `SetupModalResult<TP, TR>(result)` | パラメータなし版 |
| `ShownPresenters` | これまで表示しようとした Presenter のリスト |
| `LastParameter` | 最後に渡されたパラメータ |

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## 関連ページ

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Navigator` プロパティの位置づけ
- [ViewMappingRegister](Reference-ViewMappingRegister) — Navigator が依存する View 解決の仕組み
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — `IRequestClose<TResult>` と `InteractionResult<TResult>` の詳細
- [Dependency Injection](Reference-DependencyInjection) — `IPresenterFactory` で子 Presenter を DI 経由で解決する方法
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — ダーティチェック・保存確認の実装
- サンプル:
  - `samples/WinformsMVP.Samples/NavigatorDemo/` — Modal / Non-Modal / シングルトン
  - `samples/WinformsMVP.Samples/WindowClosingDemo/` — `IRequestClose<TResult>` の完全例
