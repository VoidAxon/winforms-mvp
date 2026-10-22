# Platform Services

`Platform Services` は、UI 操作・I/O・ロギング・ウィンドウナビゲーション等を **WinForms から切り離された抽象** として提供するサービス群です。
Presenter は `MessageBox.Show()` や `new OpenFileDialog()` を直接呼びません。代わりにこれらのサービスを使うことで、Presenter が WinForms に依存しない状態を保ち、単体テストでモックに差し替えられます。

> **MVP の鉄則 2** ([Presenter は UI 型を直接扱わない](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を実現する中核機構です。

---

## 目次

- [全体構成](#全体構成)
- [`IMessageService` — メッセージダイアログ](#imessageservice--メッセージダイアログ)
- [アンカー付きフィードバック (View 層)](#アンカー付きフィードバック-view-層)
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

サービスはすべて `ServiceLocator.Current` (`IServiceProvider`) から解決されます。デフォルトでは `DefaultServiceProvider` がフレームワーク組み込みサービスをプリシードしており、アプリ起動時に `MvpApplication.Configure(...)` (推奨) / `ServiceLocator.Configure(...)` / `ServiceLocator.Current` への代入で差し替えられます ([サービスの構成](#サービスの構成-servicelocator) 参照)。

| サービス | インターフェイス | 用途 |
|---------|---------------|----|
| メッセージ | `IMessageService` | 情報・警告・エラー・確認ダイアログ |
| トースト | `IToastService` | 隅に積み上がる非ブロッキング通知 (詳細は [トースト通知](Reference-Toast-Notifications)) |
| システムダイアログ | `IDialogProvider` | OpenFile / SaveFile / FolderBrowser 等 |
| ファイル I/O | `IFileService` | `ReadAllText` / `WriteAllText` 等 |
| ロギング | `ILogger` / `ILoggerFactory` | 構造化ロギング (詳細は [Logging](Reference-Logging)) |
| ウィンドウナビゲーション | `IWindowNavigator` | Modal / Non-Modal ウィンドウ表示 (詳細は [WindowNavigator](Reference-WindowNavigator)) |
| イベント集約 | `IEventAggregator` | Presenter 間の Pub/Sub。シングルトンとして既定登録 (詳細は [EventAggregator](Reference-EventAggregator)) |

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
    bool ConfirmOkCancel(string text, string caption = "");
    ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
}
```

> **トーストは `IMessageService` ではなく `IToastService`** です。Presenter からは `Toasts` プロパティで
> `Toasts.Show(text, type[, duration])` を呼びます (`duration` が null なら設定された既定値)。
> ワーカースレッドから呼んでも UI スレッドで表示されます。1 回ごとの外観指定 (`ToastOptions`) や
> 座標指定は View 用の `IWinFormsToastService` / `AnchoredToast` で行います。詳細は
> [トースト通知](Reference-Toast-Notifications) を参照してください。

> **見た目の差し替え:** ダイアログの見た目を決めるのは `IMessageDialogBackend` の 1 か所だけです。
> 組み込みの `MessageService` (Presenter の `Messages`) と `AnchoredMessageBox` はどちらもこれを通ります。
> 既定はネイティブ MessageBox を使う `NativeMessageDialogBackend` です。社内標準のダイアログにしたい場合は
> 起動時に `mvp.Messages.UseDialogs(new MyDialogBackend())` を指定します。caption を省略したときのタイトルは
> `mvp.Messages.DefaultCaption` (既定 `"System"`) です。

> **位置 (Point) 指定の対話は Presenter の責務ではありません。** 画面座標を知っているのは
> View だけなので、`IMessageService` には `Point` を取るメソッドを置きません。特定座標に
> 出したい場合は **View 層ユーティリティ**を使います:
> - `AnchoredMessageBox.ShowInfo/ShowWarning/ShowError/ConfirmYesNo(text, Point, caption)`
>   — MessageBox を指定座標に表示 (画面外なら自動で引き戻す。見た目は `IMessageDialogBackend` に従う)。
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

## アンカー付きフィードバック (View 層)

特定の位置に出すトースト/メッセージボックスは **View 層の関心事**です(画面座標を知っているのは View)。
Presenter 向けの汎用 API は意図的に提供していません — どの入力(マウス/キーボード/コード)から
来たかを推測して位置を決める仕組みは完全にはなり得ず、`Messages` との使い分けも混乱を招くためです。

使い方は 2 つだけ:

1. **View コードから直接** — `AnchoredToast.Show(text, type, anchor)` /
   `AnchoredMessageBox.ConfirmYesNo(text, anchor, caption)` などの静的ユーティリティを呼びます
   (アンカー座標は View が自分で決める)。**Presenter からは呼ばないでください。**
2. **Presenter が位置に意味のあるフィードバックを出したいとき** — View に小さな**意味メソッド**を
   1 つ生やします。配置の決定は Form 実装側に置かれ、入力の種類によらず常に正しい位置になります:

```csharp
// View interface — semantic, zero coordinates
bool ConfirmDelete();

// Form — the placement decision lives where the controls are
public bool ConfirmDelete()
{
    var anchor = _deleteButton.PointToScreen(new Point(0, _deleteButton.Height));
    return AnchoredMessageBox.ConfirmYesNo("Delete this item?", anchor, "Confirm");
}

// Presenter — knows the outcome, never sees a coordinate
if (View.ConfirmDelete()) { ... }
```

> **既定の選択は `Messages`**(コーナートースト/中央ダイアログ)です。トリガーがマウスでも
> キーボードでもコードでも常に正しく、ほとんどのフィードバックはこれで足ります。アンカー付きは
> 「この位置に出ることに意味がある」場合だけの選択肢です。実例は
> `samples/WinformsMVP.Samples/AnchoredMessageDemo/` を参照してください。

`AnchoredToast` はマルチモニター対応です(`Screen.FromPoint` でアンカーのあるスクリーンにクランプ)。

---

## `IDialogProvider` — システムダイアログ

OpenFile / SaveFile / FolderBrowser / Color のシステム標準ダイアログを抽象化します。
戻り値は [`InteractionResult<T>`](#interactionresultt--失敗しうる操作のラッパー) で、ユーザー操作 (OK/Cancel) と失敗を明示的に分けて扱えます。

### 主な API

```csharp
public interface IDialogProvider
{
    InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null);
    InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null);
    InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null);
    InteractionResult<Color>  ShowColorDialog(ColorDialogOptions options = null);
}

// WinformsMVP パッケージ — 戻り値が GDI+ / 印刷の型になるダイアログ
public interface IWinFormsDialogProvider : IDialogProvider
{
    InteractionResult<Font>            ShowFontDialog(FontDialogOptions options = null);
    InteractionResult<PrinterSettings> ShowPrintDialog(PrintDialogOptions options = null);
    InteractionResult<PageSettings>    ShowPageSetupDialog(PageSetupDialogOptions options = null);
    InteractionResult<bool>            ShowPrintPreviewDialog(PrintPreviewDialogOptions options = null); // 表示できたら Ok(true)
}
```

> フォント / 印刷系ダイアログは `IDialogProvider` から `IWinFormsDialogProvider` に移動しました。Presenter 向けの `IDialogProvider` (WinformsMVP.Core) をプラットフォーム非依存に保つためです。必要な場合は View 側コード (または WinForms を知るサービス) で `IWinFormsDialogProvider` を解決してください。`ServiceLocator` の既定プロバイダは同じ `DialogProvider` インスタンスを両方のインターフェイスで登録し、`AddWinformsMVP` も `IWinFormsDialogProvider` を既定登録します。

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
    public string Filter { get; set; }
    public string InitialDirectory { get; set; }
    public string FileName { get; set; }
    public bool CheckFileExists { get; set; } = true;
    public bool CheckPathExists { get; set; } = true;
    public string Title { get; set; }
}
```

呼び出しごとに指定しなかったタイトル・フィルタ・エラーメッセージの既定値は、起動時に `mvp.Dialogs`
(`OpenFileDialogTitle` / `SaveFileDialogTitle` / `DefaultFileFilter` / `FolderBrowserDescription` /
`FileOpenErrorMessage` など。`DialogProviderOptions` として登録) で設定します。呼び出しごとの値 (例 `OpenFileDialogOptions.Title`) が優先されます。

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

`Microsoft.Extensions.Logging` 互換の API を持つ自社抽象です。外部依存なしで `WinformsMVP.Core` に収めるため、BCL のみに依存しています。

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
| `Toasts` | `IToastService` | トースト通知 |
| `Dialogs` | `IDialogProvider` | システムダイアログ |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | ロギング |
| `Navigator` | `IWindowNavigator` | ウィンドウ表示 |

変更追跡には `TrackChanges(value)` (起動時の `mvp.ChangeTracking` 設定を反映した `ChangeTracker<T>` を作る) も使えます。詳細は [ChangeTracker](Reference-ChangeTracker)。

業務固有のサービス (`IUserRepository`、`IOrderService` 等) は通常通り **コンストラクタ注入** で受け取ります。DI パターンの選び分けは [Dependency Injection](Reference-DependencyInjection) を参照。

---

## サービスの構成 (`ServiceLocator`)

`ServiceLocator.Current` がアプリ全体で使われる `IServiceProvider` の実体です。`Program.cs` で 1 回だけ設定します。デフォルトは `DefaultServiceProvider` にフレームワーク組み込みサービスがプリシードされた状態です。

### 最小構成 (デフォルトのまま使う)

```csharp
// 設定不要 — ServiceLocator.Current は初回アクセス時に自動的に DefaultServiceProvider を生成する。
// Messages / Toasts / Dialogs / Files は標準実装、View マッピングは空、ロギングは NullLoggerFactory。
// WinForms 系の組み込み (MessageService / トースト / DialogProvider / WindowNavigator / ViewMappingRegister) は
// 実行時に WinformsMVP アセンブリの内部フックから登録される。WinformsMVP.Core だけを配置した場合
// (Presenter 単体テストなど) は FileService・NullLoggerFactory・EventAggregator・組み込みの変更追跡エンジンのみ。
```

### フレームワークをカスタマイズする (`MvpApplication.Configure`)

フレームワークの調整 (ダイアログの見た目、トーストの既定値、変更追跡エンジン、ロギング、ディスパッチャーのミドルウェアなど) と業務サービスの登録は、起動時の 1 回の呼び出しにまとめます。

```csharp
MvpApplication.Configure(
    framework: mvp =>
    {
        mvp.Messages.UseDialogs(new CompanyStyleDialogBackend());
        mvp.Messages.DefaultCaption = "Contoso CRM";
        mvp.Toasts.Duration = 4500;
        mvp.Dialogs.DefaultFileFilter = "CSV (*.csv)|*.csv|All Files (*.*)|*.*";
        mvp.Logging.Use(new DebugLoggerFactory());
    },
    services: reg =>
    {
        reg.RegisterInstance<IViewMappingRegister>(register);
        BillingModule.Register(reg);
    });
```

`mvp.` のグループは `Messages` / `Toasts` / `Dialogs` / `ChangeTracking` / `Events` / `Logging` / `Actions` です。
優先順位は **`services:` で直接登録したサービス > `mvp.` での選択 > 組み込みの既定値**。直接登録したオプションオブジェクト
(例 `ToastHostOptions`) は `mvp.` で組み立てたものを丸ごと置き換えます (値のマージはしません)。
詳細は [HowTo: フレームワークをカスタマイズする](HowTo-Customize-Framework) を参照してください。

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

`ServiceLocator.Configure(reg => ...)` は `framework:` ブロックを持たない低レベルの入口として引き続き使えます (組み込み登録の後に `reg` の登録が後勝ちで上書き)。

> **破棄:** `DefaultServiceProvider` は `IDisposable` で、自分のファクトリが生成したシングルトン (例 `EventAggregator`、トーストサービス) を破棄します。`RegisterInstance` で渡したインスタンスは破棄しません。`ServiceLocator` は自分が組み立てたプロバイダ (遅延生成の既定、`Configure` / `MvpApplication.Configure` で作ったもの) を、置き換えや `Reset()` のときに破棄します。`ServiceLocator.Current` に代入したプロバイダは代入側の所有で、破棄されません。アプリ終了時の破棄は `MvpApplication.Run(mainForm)` (メインフォームが閉じたら `Shutdown()`) か `MvpApplication.Shutdown()` で行います。

### M.E.DI コンテナ統合

```csharp
// AddWinformsMVP → BuildServiceProvider → UseWinformsMVP の順で設定する
services.AddWinformsMVP(viewRegistry, mvp =>
{
    mvp.Toasts.Duration = 4500;   // MvpApplication.Configure の framework: と同じビルダー
});
var provider = services.BuildServiceProvider();
provider.UseWinformsMVP();   // ServiceLocator.Current = provider
```

詳細は [Dependency Injection](Reference-DependencyInjection) を参照。

---

## モックでのテスト

> **パッケージ:** フェイクと `PresenterTestHost` / `TestServices` は `WinformsMVP.Testing` パッケージ (`netstandard2.0`、`WinformsMVP.Core` のみに依存) にあります。テストプロジェクトから参照してください (`dotnet new winformsmvp-tests` は参照済み)。フェイクは任意で、自作のフェイクや Moq と混ぜて使えます。

テスト用サービスは `PresenterTestHost.Start` の第 1 引数で、その Presenter だけに渡します。グローバルな `ServiceLocator.Current` を差し替える必要はないので、テストを並列に実行できます。`TestServices.CreateFakes()` は下表のフェイクをすべて登録したプロバイダ (`fakes.Provider`) を返します。`TestServices.Create()` は無出力の `ILoggerFactory` (どの Presenter もアタッチ時に必要) だけを登録したプロバイダを返します (自作のフェイクだけで組むとき)。

```csharp
[Fact]
public void OnSave_ShowsSuccessMessage()
{
    var fakes = TestServices.CreateFakes();         // フレームワークサービスのフェイクを登録済み
    // 自前の実装に差し替えるなら Start の前に: fakes.Provider.RegisterInstance<IFileService>(myFiles);

    PresenterTestHost.Start(fakes.Provider, new MyPresenter(), mockView);   // アタッチ + 実際の初期化
    mockView.Binder.Dispatch(StandardActions.Save);

    Assert.Contains(fakes.Messages.Calls, m => m.Kind == MessageKind.Info && m.Text.Contains("Saved"));
}
```

主なフェイク:

| `FakeServices` のプロパティ | 型 | 主なメンバー |
|---------------|------|--------------|
| `Messages` | `FakeMessageService` | `Calls` (`Kind` / `Text` / `Caption` / `IsConfirmation`)、答えの用意: `EnqueueYesNo` / `EnqueueOkCancel` / `EnqueueYesNoCancel` → `YesNoHandler` / `OkCancelHandler` / `YesNoCancelHandler` → `AnswerUnscripted(yes)`、`ClearCalls()` / `ResetScripts()` |
| `Toasts` | `FakeToastService` | `Calls` (`Text` / `Type` / `Duration`)、`ClearCalls()` |
| `Dialogs` | `FakeDialogProvider` | `EnqueueOpenFile` / `EnqueueOpenMultipleFiles` / `EnqueueSaveFile` / `EnqueueFolderBrowser` / `EnqueueColor`、`OpenFileHandler` 等 (オプションを受け取る関数)、`CancelUnscripted()`、`Calls` (`Kind` / `Options`) |
| `Files` | `InMemoryFileService` | どの OS でも Windows のパス規則 (基準フォルダ `C:\test`)、`SeedFile(path, text)` / `SeedFile(path, bytes)` / `SeedDirectory`、`FilePaths` / `DirectoryPaths`、`Failure` (操作ごとに例外を注入) |
| `Navigator` | `FakeWindowNavigator` | モーダル: `RespondTo<TPresenter, TResult>(r => ...)` / `RespondTo<TPresenter>(...)`、`LastModalParameters<T>()`、`CancelUnscripted()`。非モーダル: `Windows` (`FakeWindow.CloseWithResult(value)` / `Close()` / `CloseWithError(...)`)。`Calls` |
| `Logs` | `RecordingLoggerFactory` | `Entries` (`Category` / `Level` / `Exception` / `Message` / `Template` / `Args`)、`Clear()` |

答えを用意していない確認ダイアログ・ファイルダイアログ・モーダルウィンドウは `UnscriptedInteractionException` になります (想定外の問い合わせが黙って片方の分岐に進まないように)。フェイクのサービスメソッドは `virtual` なので、1 操作だけ上書きした派生クラスも作れます。

詳しいテストパターンは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照してください。

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 2 (Presenter から UI 型を排除) の説明
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Messages` 等のプロパティの位置づけ
- [Logging](Reference-Logging) — `ILogger` の詳細
- [WindowNavigator](Reference-WindowNavigator) — `IWindowNavigator` の詳細
- [Dependency Injection](Reference-DependencyInjection) — DI 連携・モジュール化された構成
- [HowTo: フレームワークをカスタマイズする](HowTo-Customize-Framework) — `mvp.` で差し替えられるものの一覧
- [トースト通知](Reference-Toast-Notifications) — `IToastService` / `IWinFormsToastService` / `AnchoredToast`
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — Mock サービスの使い方
- [HowTo: エラー処理戦略](HowTo-Handle-Errors) — `IMessageService` と `InteractionResult<T>` の組み合わせ
