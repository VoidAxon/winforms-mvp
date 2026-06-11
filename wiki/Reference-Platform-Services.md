# Platform Services

`Platform Services` は、UI 操作・I/O・ロギング・ウィンドウナビゲーション等を **WinForms から切り離された抽象** として提供するサービス群です。
Presenter は `MessageBox.Show()` や `new OpenFileDialog()` を直接呼びません。代わりにこれらのサービスを使うことで、Presenter が WinForms に依存しない状態を保ち、単体テストでモックに差し替えられます。

> **MVP の鉄則 2** ([Presenter は UI 型を直接扱わない](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を実現する中核機構です。

---

## 目次

- [全体構成](#全体構成)
- [`IMessageService` — メッセージダイアログ](#imessageservice--メッセージダイアログ)
- [`IAnchoredMessageService` — カーソル位置固定フィードバック](#ianchoredmessageservice--カーソル位置固定フィードバック)
- [`IDialogProvider` — システムダイアログ](#idialogprovider--システムダイアログ)
- [`IFileService` — ファイル I/O](#ifileservice--ファイル-io)
- [`ILogger` / `ILoggerFactory` — ロギング](#ilogger--iloggerfactory--ロギング)
- [`IWindowNavigator` — ウィンドウナビゲーション](#iwindownavigator--ウィンドウナビゲーション)
- [`InteractionResult<T>` — 失敗しうる操作のラッパー](#interactionresultt--失敗しうる操作のラッパー)
- [Presenter からのアクセス方法](#presenter-からのアクセス方法)
- [サービスの構成 (`ServiceLocator`)](#サービスの構成-servicelocator)
- [モックでのテスト](#モックでのテスト)
- [関連ページ](#関連ページ)

---

## 全体構成

サービスはすべて `ServiceLocator.Current` (`IServiceProvider`) から解決されます。デフォルトでは `DefaultServiceProvider` がフレームワーク組み込みサービスをプリシードしており、アプリ起動時に `ServiceLocator.Configure(...)` か `ServiceLocator.Current` への代入で差し替えられます。

| サービス | インターフェイス | 用途 |
|---------|---------------|----|
| メッセージ | `IMessageService` | 情報・警告・エラー・確認ダイアログ |
| システムダイアログ | `IDialogProvider` | OpenFile / SaveFile / FolderBrowser 等 |
| ファイル I/O | `IFileService` | `ReadAllText` / `WriteAllText` 等 |
| ロギング | `ILogger` / `ILoggerFactory` | 構造化ロギング (詳細は [Logging](Reference-Logging)) |
| ウィンドウナビゲーション | `IWindowNavigator` | Modal / Non-Modal ウィンドウ表示 (詳細は [WindowNavigator](Reference-WindowNavigator)) |

---

## `IMessageService` — メッセージダイアログ

`MessageBox.Show()` の代替です。Presenter は本サービス経由でしかダイアログを出してはいけません。

### 主な API

```csharp
public interface IMessageService
{
    // 情報・警告・エラー
    void ShowInfo(string text, string caption = "");
    void ShowWarning(string text, string caption = "");
    void ShowError(string text, string caption = "");

    // 確認 (戻り値は bool または列挙)
    bool ConfirmYesNo(string text, string caption = "");
    ConfirmResult ConfirmYesNoCancel(string text, string caption = "");

    // 非ブロッキング通知 (duration はミリ秒、type に既定値はない)
    // 隅に積み上がるトースト。詳細は「トースト通知」リファレンスを参照。
    void ShowToast(string text, ToastType type, int duration = 3000);
    // 外観の個別指定 (位置=画面の隅・サイズ・フォント・表示時間)。既定値は ToastDefaults。
    void ShowToast(string text, ToastType type, ToastOptions options);
}
```

> トーストの積み上げ挙動・`ToastOptions` / `ToastDefaults` / `AnchoredToast` の詳細は
> [トースト通知](Reference-Toast-Notifications) を参照してください。

> **位置 (Point) 指定の対話は Presenter の責務ではありません。** 画面座標を知っているのは
> View だけなので、`IMessageService` には `Point` を取るメソッドを置きません。特定座標に
> 出したい場合は **View 層ユーティリティ**を使います:
> - `AnchoredMessageBox.ShowInfo/ShowWarning/ShowError/ConfirmYesNo(text, Point, caption)`
>   — ネイティブ MessageBox を指定座標に表示 (画面外なら自動で引き戻す)。
> - `AnchoredToast.Show(text, type, Point, options)` — 単一のトーストを指定座標に表示。
>
> いずれも `Application.OpenForms` には現れません。Presenter からは呼ばず、Form /
> UserControl など View のコードからのみ使用してください。

### 使用例

```csharp
private void OnSave()
{
    try
    {
        SaveData();
        Messages.ShowInfo("Saved!", "Success");
    }
    catch (ValidationException ex)
    {
        Messages.ShowWarning(ex.Message, "Validation Failed");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Save failed: {ex.Message}", "Error");
    }
}

private void OnDelete()
{
    if (!Messages.ConfirmYesNo("Delete this item?", "Confirm Delete"))
        return;

    DeleteItem();
    Messages.ShowInfo("Deleted.", "Success");
}
```

### 戻り値の型

| メソッド | 戻り値 | 注意 |
|---------|------|----|
| `ShowInfo` / `ShowWarning` / `ShowError` | `void` | 表示のみ |
| `ConfirmYesNo` | `bool` | `true` = Yes |
| `ConfirmYesNoCancel` | `ConfirmResult` 列挙 (`Yes` / `No` / `Cancel`) | UI 型ではない |

すべて **業務側の型** を返します。`DialogResult` 等の WinForms 型は使いません。

---

## `IAnchoredMessageService` — カーソル位置固定フィードバック

クリック位置の近くにトーストまたはメッセージボックスを表示するためのサービスです。
メソッド名は `IMessageService` と対称で、違いは「どこに出るか」だけです。
実装は `AnchoredToast` / `AnchoredMessageBox` 静的ユーティリティに委譲します。

> **同期呼び出しの契約:** `Point` を取らない形のアンカーはコール時のカーソル位置です。
> UI スレッド上のイベント/アクションハンドラ内で同期に呼んでください。`await` の後は
> カーソルが移動している可能性があるため、非同期処理後のフィードバックには
> `Messages.ShowToast` (コーナートースト) を使ってください。

### インターフェイス

各メソッドは 2 形態あります — `Point` なし(カーソル位置にアンカー)と `Point` あり
(呼び出し側が自分でアンカー座標を決める。コントロールの矩形やヒットテスト結果を知っている
**View 層コード向け**であり、Presenter は使いません):

```csharp
public interface IAnchoredMessageService
{
    // Toast (non-blocking)
    void ShowToast(string text, ToastType type, ToastOptions options = null);
    void ShowToast(string text, ToastType type, Point anchor, ToastOptions options = null);

    // Message dialogs (blocking)
    void ShowInfo(string text, string caption = "");
    void ShowInfo(string text, Point anchor, string caption = "");
    void ShowWarning(string text, string caption = "");
    void ShowWarning(string text, Point anchor, string caption = "");
    void ShowError(string text, string caption = "");
    void ShowError(string text, Point anchor, string caption = "");

    // Confirmations (blocking; true = affirmative)
    bool ConfirmYesNo(string text, string caption = "");
    bool ConfirmYesNo(string text, Point anchor, string caption = "");
    bool ConfirmOkCancel(string text, string caption = "");
    bool ConfirmOkCancel(string text, Point anchor, string caption = "");
    ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
    ConfirmResult ConfirmYesNoCancel(string text, Point anchor, string caption = "");
}
```

ボタン/アイコンの組み合わせはパラメータではなく**メソッド名が表現**します
(`IMessageService` と同じ流儀)。`Point` は `System.Drawing` の型であり WinForms 依存ではありません。

### Presenter からの呼び出し方 — `IViewBase` 拡張メソッド

Presenter は直接 `IAnchoredMessageService` を依存として受け取りません。代わりに
`IViewBase` 拡張メソッド (`AnchoredMessageViewExtensions`) を使います:

```csharp
private void OnSave()
{
    // 同期ハンドラ内で呼ぶ → クリック位置にトーストが現れる
    View.ShowHint("Saved.");
    View.ShowToast("Saved!", ToastType.Success);
}

private void OnDelete()
{
    if (View.ConfirmYesNo("Delete this item?", "Confirm"))
    {
        View.ShowToast("Deleted", ToastType.Warning);
    }
}
```

利用可能な `IViewBase` 拡張メソッド:
`ShowToast / ShowInfo / ShowWarning / ShowError / ConfirmYesNo / ConfirmOkCancel / ConfirmYesNoCancel`
— それぞれサービスと同じ 2 形態(カーソル版 / `Point` 版)があります。

> **使い分けの規約:** Presenter から呼ぶのは**カーソル版だけ**です(Presenter は座標を扱わない)。
> `Point` 版は、自分でアンカー座標を決めたい **View 層コード**向けです — Form が自分自身に対して
> `this.ShowToast("Saved", ToastType.Success, _saveButton.PointToScreen(...))` のように呼びます。
> 型としては合法でも、Presenter から `Point` を渡すのはレイヤ規約違反です。

### 登録

`IAnchoredMessageService` はフレームワーク組み込みサービスとして自動登録されます:
- `ServiceLocator.Configure(...)` 経由のデフォルトプロバイダ
- M.E.DI の `services.AddWinformsMVP(viewRegistry)` (TryAdd セマンティクス)

### マルチモニター対応

`ToastNotification.ShowAnchored` は `Screen.FromPoint(anchor).WorkingArea` を使用するため、
非プライマリモニター上のアンカー座標でも正しいスクリーンに表示されます。

### テストパターン

`View.ShowToast(...)` はグローバル `ServiceLocator.Current` からサービスを解決するため、
テストでは `ServiceLocator.Configure` でモックを登録し、`Dispose` 時に `Reset()` します:

```csharp
[Collection("ServiceLocator")]
public class MyPresenterTests : IDisposable
{
    private readonly MockAnchoredMessageService _anchored = new MockAnchoredMessageService();
    private readonly MyPresenter _presenter = new MyPresenter();
    private readonly StubView _view = new StubView();

    public MyPresenterTests()
    {
        ServiceLocator.Configure(reg => reg.RegisterInstance<IAnchoredMessageService>(_anchored));
        _presenter.AttachView(_view);
        _presenter.Initialize();
    }

    public void Dispose() { _presenter.Dispose(); ServiceLocator.Reset(); }

    [Fact]
    public void Save_ShowsSuccessToast()
    {
        _presenter.Dispatch(MyActions.Save);
        Assert.Single(_anchored.Toasts);
        Assert.Equal(ToastType.Success, _anchored.Toasts[0].Type);
    }
}
```

`MockAnchoredMessageService` は `tests/WinformsMVP.Samples.Tests/Mocks/` にあります。

---

## `IDialogProvider` — システムダイアログ

OpenFile / SaveFile / FolderBrowser 等のシステム標準ダイアログを抽象化します。
戻り値は [`InteractionResult<T>`](#interactionresultt--失敗しうる操作のラッパー) で、ユーザー操作 (OK/Cancel) と失敗を明示的に分けて扱えます。

### 主な API

```csharp
public interface IDialogProvider
{
    InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null);
    InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null);
    InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null);
    InteractionResult<Color>  ShowColorDialog(ColorDialogOptions options = null);
    InteractionResult<Font>   ShowFontDialog(FontDialogOptions options = null);
}
```

### 使用例

```csharp
private void OnImport()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
        Title = "Select a file to import",
    });

    if (result.IsOk)
    {
        var path = result.Value;
        ImportFile(path);
    }
    else if (result.IsCancelled)
    {
        // ユーザーが Cancel — 何もしない
    }
    else if (result.IsError)
    {
        Messages.ShowError(result.ErrorMessage, "Import Failed");
    }
}
```

### Options 型

各メソッドはオプション型を持ち、WinForms 型を露出させずに設定できます。

```csharp
public class OpenFileDialogOptions
{
    public string Title { get; set; }
    public string Filter { get; set; }
    public string InitialDirectory { get; set; }
    public bool RestoreDirectory { get; set; } = true;
    public bool CheckFileExists { get; set; } = true;
    public bool Multiselect { get; set; } = false;
    public string DefaultExt { get; set; }
}
```

---

## `IFileService` — ファイル I/O

`System.IO.File` の薄いラッパーで、Presenter から直接 `File.ReadAllText` を呼ばないためのものです。テスト時に in-memory モックに差し替えるのが目的です。

```csharp
public interface IFileService
{
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    byte[] ReadAllBytes(string path);
    void WriteAllBytes(string path, byte[] bytes);
    bool Exists(string path);
    void Delete(string path);
}
```

```csharp
private void LoadFile(string path)
{
    if (!Files.Exists(path))
    {
        Messages.ShowWarning("File not found.", "Open File");
        return;
    }

    var content = Files.ReadAllText(path);
    View.Content = content;
}
```

---

## `ILogger` / `ILoggerFactory` — ロギング

`Microsoft.Extensions.Logging` 互換の API を持つ自社抽象です。`net40` ホストでも動作するように BCL のみに依存しています。

詳細は **[Logging](Reference-Logging)** を参照してください。ここでは Presenter からの呼び出し方だけ示します。

```csharp
private void OnSave()
{
    try
    {
        SaveData();
        Logger.LogInformation("User {UserId} saved data", _userId);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to save data for user {UserId}", _userId);
        Messages.ShowError("Save failed", "Error");
    }
}
```

---

## `IWindowNavigator` — ウィンドウナビゲーション

子ウィンドウの表示・結果取得を担います。

詳細は **[WindowNavigator](Reference-WindowNavigator)** を参照してください。Presenter からの呼び出し例:

```csharp
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
```

---

## `InteractionResult<T>` — 失敗しうる操作のラッパー

ファイル選択・ウィンドウ表示等、3 つの結果 (成功 / キャンセル / エラー) を持つ操作の戻り値型です。
**例外ベースの制御フローを避け**、結果を明示的に分岐できるのが利点です。

### 主要 API

```csharp
public class InteractionResult<T>
{
    public bool IsOk   { get; }
    public bool IsCancelled { get; }
    public bool IsError     { get; }

    public T      Value        { get; }   // IsOk のとき有効
    public string ErrorMessage { get; }   // IsError のとき有効
    public Exception Exception { get; }   // IsError のとき有効 (任意)

    public static InteractionResult<T> Ok(T value);
    public static InteractionResult<T> Cancel();
    public static InteractionResult<T> Error(string message, Exception ex = null);
}
```

### カスタム操作で使う例

```csharp
public InteractionResult<Customer> LoadCustomer(int customerId)
{
    try
    {
        var customer = _repository.GetById(customerId);
        if (customer == null)
            return InteractionResult<Customer>.Error("Customer not found");

        return InteractionResult<Customer>.Ok(customer);
    }
    catch (Exception ex)
    {
        return InteractionResult<Customer>.Error("Failed to load customer", ex);
    }
}

// 呼び出し側
var result = LoadCustomer(id);
if (result.IsOk)
    View.Customer = result.Value;
else
    Messages.ShowError(result.ErrorMessage, "Error");
```

---

## Presenter からのアクセス方法

すべての Presenter は基底クラスで以下のプロパティを継承しています。**コンストラクタ注入は不要** です。これらは `ServiceLocator.Current` 経由で遅延解決されます。

| プロパティ | 型 | 用途 |
|----------|----|----|
| `Messages` | `IMessageService` | メッセージダイアログ |
| `Dialogs` | `IDialogProvider` | システムダイアログ |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | ロギング |
| `Navigator` | `IWindowNavigator` | ウィンドウ表示 |

業務固有のサービス (`IUserRepository`、`IOrderService` 等) は通常通り **コンストラクタ注入** で受け取ります。DI パターンの選び分けは [Dependency Injection](Reference-DependencyInjection) を参照。

---

## サービスの構成 (`ServiceLocator`)

`ServiceLocator.Current` がアプリ全体で使われる `IServiceProvider` の実体です。`Program.cs` で 1 回だけ設定します。デフォルトは `DefaultServiceProvider` にフレームワーク組み込みサービスがプリシードされた状態です。

### 最小構成 (デフォルトのまま使う)

```csharp
// 設定不要 — ServiceLocator.Current は初回アクセス時に自動的に DefaultServiceProvider を生成する。
// Messages / Dialogs / Files は標準実装、View マッピングは空、ロギングは NullLoggerFactory。
```

### View マッピングを上書きする

```csharp
var register = new ViewMappingRegister();
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

ServiceLocator.Configure(reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<WinformsMVP.Logging.ILoggerFactory>(new DebugLoggerFactory());
});
```

### M.E.DI コンテナ統合

```csharp
// AddWinformsMVP → BuildServiceProvider → UseWinformsMVP の順で設定する
services.AddWinformsMVP(viewRegistry);
var provider = services.BuildServiceProvider();
provider.UseWinformsMVP();   // ServiceLocator.Current = provider
```

詳細は [Dependency Injection](Reference-DependencyInjection) を参照。

---

## モックでのテスト

> ⚠️ **各 `Mock*` サービス (`MockMessageService`、`MockDialogProvider`、`MockFileService`、`MockWindowNavigator`)・`MockServices` は、いずれもテストプロジェクト用のヘルパーで `WinformsMVP` パッケージには同梱されていません** (`tests/WinformsMVP.Samples.Tests/Mocks/`)。自分のテストプロジェクトにコピーして使ってください。Presenter にサービスを差し込むには `PresenterBase.SetServiceProvider` (internal — `InternalsVisibleTo` 経由) を使います。

テスト用サービスを差し替えるには、`ServiceLocator.Configure(...)` でテスト用プロバイダを設定するか、`InternalsVisibleTo` を通じて `SetServiceProvider` を直接呼びます。

```csharp
[Fact]
public void OnSave_ShowsSuccessMessage()
{
    // テスト用サービスプロバイダを用意する
    var mockMessages = new MockMessageService();
    var sp = new DefaultServiceProvider();
    sp.RegisterInstance<IMessageService>(mockMessages);
    // ... 他のサービスも必要に応じて登録

    var presenter = new MyPresenter();
    ((IServiceProviderAware)presenter).SetServiceProvider(sp);   // InternalsVisibleTo が必要

    presenter.AttachView(mockView);
    presenter.Initialize();
    presenter.Dispatcher.Dispatch(StandardActions.Save);

    Assert.True(mockMessages.InfoMessageShown);
    Assert.True(mockMessages.HasCall(MessageType.Info, "Saved"));
}
```

主な検証フラグ:

| サービスのモック | 検証用プロパティ |
|---------------|--------------|
| `MockMessageService` | `InfoMessageShown` / `WarningMessageShown` / `ErrorMessageShown` / `ConfirmDialogShown` / `Calls` / `GetLastCall().Message` / `HasCall(type, contains)` / `ConfirmYesNoResult` (= 戻り値設定) |
| `MockDialogProvider` | `OpenFileDialogResult` / `SaveFileDialogResult` / `FolderBrowserDialogResult` / `PrintPreviewDialogResult` (= 戻り値設定。`string` は空/null で `Cancel`) |
| `MockFileService` | `AddFile(path, contents)` (= 事前投入) / `Exists` / `ReadAllText` / `WriteAllText` / `Clear()` |
| `MockWindowNavigator` | `ShowModalCalls` / `LastPresenter` / `LastParameters` / `ShowModalBoolResult` (= 戻り値設定) / `ShowModalInteractionResult` |

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 2 (Presenter から UI 型を排除) の説明
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Messages` 等のプロパティの位置づけ
- [Logging](Reference-Logging) — `ILogger` の詳細
- [WindowNavigator](Reference-WindowNavigator) — `IWindowNavigator` の詳細
- [Dependency Injection](Reference-DependencyInjection) — DI 連携・モジュール化された構成
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — Mock サービスの使い方
- [HowTo: エラー処理戦略](HowTo-Handle-Errors) — `IMessageService` と `InteractionResult<T>` の組み合わせ
