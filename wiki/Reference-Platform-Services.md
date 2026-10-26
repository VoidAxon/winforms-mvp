# Platform Services

`Platform Services` は、UI 操作・ロギング・ウィンドウナビゲーション等を **WinForms から切り離された抽象** として提供するサービス群です。
Presenter は `MessageBox.Show()` や `new OpenFileDialog()` を直接呼びません。代わりにこれらのサービスを使うことで、Presenter が WinForms に依存しない状態を保ち、単体テストでモックに差し替えられます。

> **MVP の鉄則 2** ([Presenter は UI 型を直接扱わない](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を実現する中核機構です。

---

## 目次

- [全体構成](#全体構成)
- [`IMessageService` — メッセージダイアログ](#imessageservice--メッセージダイアログ)
- [アンカー付きフィードバック (View 層)](#アンカー付きフィードバック-view-層)
- [`IDialogProvider` — システムダイアログ](#idialogprovider--システムダイアログ)
- [ファイル・データベース・HTTP](#ファイルデータベースhttp)
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

> **スレッド:** ワーカースレッド (`Task.Run` の中など) から呼んでも、ダイアログは UI スレッドで (開いている Form 経由の `Invoke` で) 表示され、アプリのウィンドウに対してモーダルになります。ワーカーは答えが返るまで待ちます。そのワーカーを UI スレッドで `Wait()` / `.Result` で待たないでください (開けないダイアログを待ち続けます。`await` する)。UI スレッド (メッセージループを回しているスレッド) からの呼び出しはそのスレッドで表示します。Form が 1 つも開いていないときは呼び出したスレッドで表示します。

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
> [`samples/Kibinago.Samples/AnchoredMessageDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/AnchoredMessageDemo) を参照してください。

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
```

> **フォント・印刷系のダイアログはラップしていません。** `FontDialog` / `PrintDialog` / `PageSetupDialog` / `PrintPreviewDialog` は Form のコードで直接使います (オーナーを指定でき、すべての機能が使えます)。Presenter が始める業務の流れ (帳票の書式を選ぶ、出荷票のプリンタを選ぶ等) は、**意味のある View メソッド** (`InteractionResult<ReportTypography> ChooseReportTypography(ReportTypography current)` のように、戻り値はアプリ自身のデータ型) か、アプリの印刷サービスとして表し、`Font` や `PrinterSettings` は Form / サービスの実装の中に閉じ込めます。

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

## ファイル・データベース・HTTP

フレームワークはファイル・データベース・HTTP のサービスを **提供しません** (MVP の規則が Presenter に禁じているのは UI だけで、これらは UI ではありません)。テストしやすく保つための推奨:

- **業務の用途ごとのインターフェイスをコンストラクタで注入する** — `IOrderRepository`、`ICsvExporter`、`IPriceApiClient` のように、画面が必要とする操作だけを持つインターフェイス。`System.IO` / ADO.NET / EF / `HttpClient` はその実装の中に置きます。テストでは、そのインターフェイスのフェイクを渡します。
- ファイルシステム全体を差し替えたい場合は [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) (`IFileSystem`) などのライブラリを使えます。
- 小さなツールや既存コードの移行中に Presenter から `File.ReadAllText` を直接呼ぶのは禁止ではありません (検査するアナライザーもありません)。ただし `PresenterTestHost` が差し替えられるのは **フレームワークのサービスだけ** なので、直接呼んだ I/O はテストでも本物のディスク・DB・ネットワークに届きます。

```csharp
public interface ICsvExporter
{
    void Export(string path, IReadOnlyList<Order> orders);
}

public sealed class OrderListPresenter : WindowPresenterBase<IOrderListView>
{
    private readonly ICsvExporter _exporter;

    public OrderListPresenter(ICsvExporter exporter) { _exporter = exporter; }

    private void OnExport()
    {
        var target = Dialogs.ShowSaveFileDialog(new SaveFileDialogOptions { Filter = "CSV (*.csv)|*.csv" });
        if (!target.IsOk) return;
        _exporter.Export(target.Value, View.Orders);
        Toasts.Show("出力しました", ToastType.Success);
    }
}
```

### DI コンテナを使わない場合: `Services`

`MvpApplication.Configure(services: reg => ...)` で登録した自前のサービスは、Presenter の `protected IServiceProvider Services` から取得できます (`Services.Resolve<ICsvExporter>()`)。`Navigator` が開く Presenter のように、コンストラクタ引数を渡す側がその依存を知らないときに使います。

- 依存が見えるので、**まずコンストラクタ注入** を検討してください。
- `Services` を使えるのは `OnViewAttached` / `RegisterViewActions` / `OnInitialize` 以降です。コンストラクタでフレームワークのサービスを使った Presenter はアタッチ時に拒否されます。
- テストでは `PresenterTestHost.Start(provider, ...)` に渡したプロバイダから解決されるので、そこに登録したフェイクが使われます (グローバルな `ServiceLocator.Current` には触れません)。M.E.DI の `IPresenterFactory` で作った Presenter では、その Presenter のスコープから解決されます。

### アーキテクチャテスト (任意)

「Presenter は `System.Net.Http` や ADO.NET に依存しない」をチームの規則にしたい場合は、アプリのテストで [NetArchTest](https://github.com/BenMorris/NetArchTest) や [ArchUnitNET](https://github.com/TNG/ArchUnitNET) を使って確認できます (フレームワークには組み込みません。境界はアプリごとに違うためです)。

```csharp
[Fact]
public void Presenters_DoNotTalkToTheNetworkOrTheDatabase()
{
    var result = Types.InAssembly(typeof(OrderListPresenter).Assembly)
        .That().HaveNameEndingWith("Presenter")
        .ShouldNot().HaveDependencyOnAny("System.Net.Http", "System.Data.SqlClient", "Microsoft.EntityFrameworkCore")
        .GetResult();

    Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? new List<string>()));
}
```

---

## `ILogger` / `ILoggerFactory` — ロギング

`Microsoft.Extensions.Logging` 互換の API を持つ自社抽象です。外部依存なしで `Kibinago.Core` に収めるため、BCL のみに依存しています。

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
| `Logger` | `ILogger` | ロギング |
| `Navigator` | `IWindowNavigator` | ウィンドウ表示 |

変更追跡には `TrackChanges(value)` (起動時の `mvp.ChangeTracking` 設定を反映した `ChangeTracker<T>` を作る) も使えます。詳細は [ChangeTracker](Reference-ChangeTracker)。

業務固有のサービス (`IUserRepository`、`IOrderService` 等) は通常通り **コンストラクタ注入** で受け取ります (DI コンテナを使わない場合は `Services` から取得できます — [ファイル・データベース・HTTP](#ファイルデータベースhttp))。DI パターンの選び分けは [Dependency Injection](Reference-DependencyInjection) を参照。

---

## サービスの構成 (`ServiceLocator`)

`ServiceLocator.Current` がアプリ全体で使われる `IServiceProvider` の実体です。`Program.cs` で 1 回だけ設定します。デフォルトは `DefaultServiceProvider` にフレームワーク組み込みサービスがプリシードされた状態です。

### 最小構成 (デフォルトのまま使う)

```csharp
// 設定不要 — ServiceLocator.Current は初回アクセス時に自動的に DefaultServiceProvider を生成する。
// Messages / Toasts / Dialogs は標準実装、View マッピングは空、ロギングは NullLoggerFactory。
// WinForms 系の組み込み (MessageService / トースト / DialogProvider / WindowNavigator / ViewMappingRegister) は
// 実行時に Kibinago アセンブリの内部フックから登録される。Kibinago.Core だけを配置した場合
// (Presenter 単体テストなど) は NullLoggerFactory・EventAggregator・組み込みの変更追跡エンジンのみ。
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

MvpApplication.Configure(services: reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(new DebugLoggerFactory());
});
```

`ServiceLocator.Configure(reg => ...)` は `framework:` ブロックを持たない低レベルの入口として引き続き使えます (組み込み登録の後に `reg` の登録が後勝ちで上書き)。

> **破棄:** `DefaultServiceProvider` は `IDisposable` で、自分のファクトリが生成したシングルトン (例 `EventAggregator`、トーストサービス) を破棄します。`RegisterInstance` で渡したインスタンスは破棄しません。`ServiceLocator` は自分が組み立てたプロバイダ (遅延生成の既定、`Configure` / `MvpApplication.Configure` で作ったもの) を、置き換えや `Reset()` のときに破棄します。`ServiceLocator.Current` に代入したプロバイダは代入側の所有で、破棄されません。アプリ終了時の破棄は `MvpApplication.Run(mainForm)` (メインフォームが閉じたら `Shutdown()`) か `MvpApplication.Shutdown()` で行います。

### M.E.DI コンテナ統合

```csharp
// AddKibinago → BuildServiceProvider → UseKibinago の順で設定する
services.AddKibinago(viewRegistry, mvp =>
{
    mvp.Toasts.Duration = 4500;   // MvpApplication.Configure の framework: と同じビルダー
});
var provider = services.BuildServiceProvider();
provider.UseKibinago();   // ServiceLocator.Current = provider
```

詳細は [Dependency Injection](Reference-DependencyInjection) を参照。

---

## モックでのテスト

> **パッケージ:** フェイクと `PresenterTestHost` / `TestServices` は `Kibinago.Testing` パッケージ (`netstandard2.0`、`Kibinago.Core` のみに依存) にあります。テストプロジェクトから参照してください (`dotnet new kibinago-tests` は参照済み)。フェイクは任意で、自作のフェイクや Moq と混ぜて使えます。

テスト用サービスは `PresenterTestHost.Start` の第 1 引数で、その Presenter だけに渡します。グローバルな `ServiceLocator.Current` を差し替える必要はないので、テストを並列に実行できます。`TestServices.CreateFakes()` は下表のフェイクをすべて登録したプロバイダ (`fakes.Provider`) を返します。`TestServices.Create()` は無出力の `ILoggerFactory` (どの Presenter もアタッチ時に必要) だけを登録したプロバイダを返します (自作のフェイクだけで組むとき)。

```csharp
[Fact]
public void OnSave_ShowsSuccessMessage()
{
    var fakes = TestServices.CreateFakes();         // フレームワークサービスのフェイクを登録済み
    // 自前の実装に差し替えるなら Start の前に: fakes.Provider.RegisterInstance<IToastService>(myToasts);

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
