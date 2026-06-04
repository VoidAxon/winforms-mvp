# Platform Services

`Platform Services` は、UI 操作・I/O・ロギング・ウィンドウナビゲーション等を **WinForms から切り離された抽象** として提供するサービス群です。
Presenter は `MessageBox.Show()` や `new OpenFileDialog()` を直接呼びません。代わりにこれらのサービスを使うことで、Presenter が WinForms に依存しない状態を保ち、単体テストでモックに差し替えられます。

> **MVP の鉄則 2** ([Presenter は UI 型を直接扱わない](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を実現する中核機構です。

---

## 目次

- [全体構成](#全体構成)
- [`IMessageService` — メッセージダイアログ](#imessageservice--メッセージダイアログ)
- [`IDialogProvider` — システムダイアログ](#idialogprovider--システムダイアログ)
- [`IFileService` — ファイル I/O](#ifileservice--ファイル-io)
- [`ILogger` / `ILoggerFactory` — ロギング](#ilogger--iloggerfactory--ロギング)
- [`IWindowNavigator` — ウィンドウナビゲーション](#iwindownavigator--ウィンドウナビゲーション)
- [`InteractionResult<T>` — 失敗しうる操作のラッパー](#interactionresultt--失敗しうる操作のラッパー)
- [Presenter からのアクセス方法](#presenter-からのアクセス方法)
- [サービスの構成 (`PlatformServices.Default`)](#サービスの構成-platformservicesdefault)
- [モックでのテスト](#モックでのテスト)
- [関連ページ](#関連ページ)

---

## 全体構成

サービスはすべて `IPlatformServices` から取得できます。`PlatformServices.Default` (シングルトン) が実体を保持します。

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
    void ShowInfo(string text, string caption = null);
    void ShowWarning(string text, string caption = null);
    void ShowError(string text, string caption = null);

    // 確認 (戻り値は bool または列挙)
    bool ConfirmYesNo(string text, string caption = null);
    ConfirmResult ConfirmYesNoCancel(string text, string caption = null);

    // 位置指定版 (View 層の文脈からだけ使用、Presenter からは原則使わない)
    void ShowInfoAt(string text, string caption, Point screenPoint);
    bool ConfirmYesNoAt(string text, string caption, Point screenPoint);

    // 非ブロッキング通知 (実装が対応する場合)
    void ShowToast(string text, ToastType type = ToastType.Info, TimeSpan? duration = null);
}
```

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

## `IDialogProvider` — システムダイアログ

OpenFile / SaveFile / FolderBrowser 等のシステム標準ダイアログを抽象化します。
戻り値は [`InteractionResult<T>`](#interactionresultt--失敗しうる操作のラッパー) で、ユーザー操作 (OK/Cancel) と失敗を明示的に分けて扱えます。

### 主な API

```csharp
public interface IDialogProvider
{
    InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null);
    InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null);
    InteractionResult<string> ShowFolderBrowser(FolderBrowserOptions options = null);
    InteractionResult<Color>  ShowColorDialog(Color? initialColor = null);
    InteractionResult<Font>   ShowFontDialog(Font initialFont = null);
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
    bool FileExists(string path);
    void DeleteFile(string path);
}
```

```csharp
private void LoadFile(string path)
{
    if (!Files.FileExists(path))
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

すべての Presenter は基底クラスで以下のプロパティを継承しています。**コンストラクタ注入は不要** です。

| プロパティ | 型 | 用途 |
|----------|----|----|
| `Messages` | `IMessageService` | メッセージダイアログ |
| `Dialogs` | `IDialogProvider` | システムダイアログ |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | ロギング |
| `Navigator` | `IWindowNavigator` | ウィンドウ表示 |
| `Platform` | `IPlatformServices` | 上記すべてを束ねるコンテナ。カスタムサービスを取得する場合に使用 |

業務固有のサービス (`IUserRepository`、`IOrderService` 等) は通常通り **コンストラクタ注入** で受け取ります。DI パターンの選び分けは [Dependency Injection](Reference-DependencyInjection) を参照。

---

## サービスの構成 (`PlatformServices.Default`)

`PlatformServices.Default` がアプリ全体で使われる実体です。`Program.cs` で 1 回だけ設定します。

### 最小構成

```csharp
PlatformServices.Default = new DefaultPlatformServices();
// View マッピング・ロギング・DI なし。Messages / Dialogs / Files は標準実装が使われる。
```

### View マッピング + ロギング付き

```csharp
var register = new ViewMappingRegister();
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: register,
    loggerFactory: new DebugLoggerFactory());
```

### DI コンテナ統合

```csharp
PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: register,
    loggerFactory: msFactory.AsFrameworkLoggerFactory(),
    serviceProvider: provider);
```

詳細は [Dependency Injection](Reference-DependencyInjection) を参照。

---

## モックでのテスト

`MockPlatformServices` でサービス全体を差し替えられます。

```csharp
[Fact]
public void OnSave_ShowsSuccessMessage()
{
    var mockPlatform = new MockPlatformServices();
    var presenter = new MyPresenter().WithPlatformServices(mockPlatform);

    presenter.AttachView(mockView);
    presenter.Initialize();
    presenter.Dispatcher.Dispatch(StandardActions.Save);

    Assert.True(mockPlatform.MessageService.InfoMessageShown);
    Assert.True(mockPlatform.MessageService.HasCall(MessageType.Info, "Saved"));
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
