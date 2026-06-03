# Dependency Injection

本フレームワークは、レガシーコードから本格的な DI コンテナまで対応できるよう、**3 つの DI パターン** をサポートします。
さらに `Microsoft.Extensions.DependencyInjection` (M.E.DI) との連携用にオプションパッケージ `WinformsMVP.DependencyInjection` を提供しており、複数プロジェクト構成の大規模アプリでも整理して構成できます。

> **メインパッケージは `Microsoft.Extensions.DependencyInjection` に依存していません**。BCL の `IServiceProvider` 抽象だけを知っています。M.E.DI 統合は完全にオプトインです。

---

## 目次

- [3 つの DI パターン](#3-つの-di-パターン)
  - [Service Locator](#pattern-1-service-locator-単純な-presenter-向け)
  - [Constructor Injection](#pattern-2-constructor-injection-テスタブルなコード向け)
  - [Hybrid](#pattern-3-hybrid-両者の良いとこ取り)
- [選び方の指針](#選び方の指針)
- [従来コードからの移行](#従来コードからの移行)
- [M.E.DI との統合](#medi-との統合)
- [複数プロジェクト構成 (`WinformsMVP.DependencyInjection`)](#複数プロジェクト構成-winformsmvpdependencyinjection)
- [`IPresenterFactory`: 子 Presenter の生成](#ipresenterfactory-子-presenter-の生成)
- [`IModuleRegistrar`: モジュール単位の登録](#imoduleregistrar-モジュール単位の登録)
- [完全な Shell プロジェクトの起動](#完全な-shell-プロジェクトの起動)
- [よくある誤解](#よくある誤解)
- [関連ページ](#関連ページ)

---

## 3 つの DI パターン

### Pattern 1: Service Locator (単純な Presenter 向け)

`PlatformServices.Default` を使い、コンストラクタ不要で書く方式です。

```csharp
public class SimpleDemoPresenter : WindowPresenterBase<ISimpleDemoView>
{
    // コンストラクタなし — PlatformServices.Default が使われる

    protected override void OnInitialize()
    {
        Messages.ShowInfo("Hello!");
        var file = Dialogs.ShowOpenFileDialog();
    }
}
```

| 観点 | 評価 |
|------|----|
| ✅ ボイラープレートゼロ | 学習コスト最小 |
| ✅ 既存コードからの段階移行が楽 | コンストラクタを増やさなくて済む |
| ⚠️ テスト時に `WithPlatformServices(...)` ヘルパーが必要 | できなくはない |
| ⚠️ グローバル状態への依存 | テスタブルだが暗黙的 |

**使いどころ**: プロトタイプ、デモ、レガシーコード移行の初期段階、フォーム単位の小規模スクリプト。

### Pattern 2: Constructor Injection (テスタブルなコード向け)

明示的にコンストラクタで依存を受け取ります。

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IMessageService _messages;
    private readonly IUserRepository _repository;

    public UserEditorPresenter(IMessageService messages, IUserRepository repository)
    {
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override void OnInitialize()
    {
        var users = _repository.GetAll();
        View.Users = users;
    }

    private void OnSave()
    {
        _repository.Save(userData);
        _messages.ShowInfo("Saved!", "Success");
    }
}
```

| 観点 | 評価 |
|------|----|
| ✅ 高いテスタビリティ | モック注入が自然 |
| ✅ 依存が明示的 | コンストラクタで一覧できる |
| ✅ SOLID 原則に沿う | 依存性逆転 |
| ⚠️ ボイラープレートが増える | フィールド + コンストラクタ |
| ⚠️ DI コンテナか手動生成が必要 | |

**使いどころ**: 本番コード、業務サービスを持つ Presenter、単体テストを書きたいケース。

### Pattern 3: Hybrid (両者の良いとこ取り)

業務サービスはコンストラクタ注入、Platform サービスはプロパティアクセス。**推奨パターン**。

```csharp
public class OrderProcessorPresenter : WindowPresenterBase<IOrderProcessorView>
{
    private readonly IOrderService _orderService;   // ← 業務サービスだけ DI

    public OrderProcessorPresenter(IOrderService orderService)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        View.Orders = _orderService.GetPendingOrders();
    }

    private void OnProcess()
    {
        try
        {
            _orderService.ProcessOrders(selectedOrders);
            Messages.ShowInfo("Processed!", "Success");   // ← Platform はプロパティ
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Failed: {ex.Message}", "Error");
        }
    }

    private void OnExport()
    {
        var result = Dialogs.ShowSaveFileDialog("Export Orders");   // ← Platform はプロパティ
        if (result.IsOk)
            _orderService.ExportTo(result.Value);
    }
}
```

| 観点 | 評価 |
|------|----|
| ✅ コンストラクタが業務サービスだけで簡潔 | 読みやすい |
| ✅ Platform サービスへのアクセスが容易 | 短く書ける |
| ✅ 高いテスタビリティ | モック注入 + `WithPlatformServices` |
| ✅ 可読性が最高 | 推奨 |

**使いどころ**: ほとんどの本番シナリオ。

---

## 選び方の指針

| シナリオ | 推奨パターン |
|---------|------------|
| プロトタイプ/デモ | Service Locator |
| レガシーコード移行 | Service Locator |
| テスタブルな業務ロジック | Constructor Injection |
| 本番アプリ (一般) | **Hybrid** |
| 大規模エンタープライズアプリ | DI Container + Constructor Injection |

---

## 従来コードからの移行

### シナリオ 1: `MessageBox.Show()` の置き換え

**Before**:

```csharp
private void btnSave_Click(object sender, EventArgs e)
{
    try
    {
        SaveData();
        MessageBox.Show("Saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

**After — Service Locator (移行が楽)**:

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    // コンストラクタ不要 — そのまま `Messages` プロパティを使う

    private void OnSave()
    {
        try
        {
            SaveData();
            Messages.ShowInfo("Saved!", "Success");
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

**After — Constructor Injection (テスタビリティ重視)**:

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private readonly IMessageService _messages;

    public MyPresenter(IMessageService messages)
    {
        _messages = messages;
    }

    private void OnSave()
    {
        try
        {
            SaveData();
            _messages.ShowInfo("Saved!", "Success");
        }
        catch (Exception ex)
        {
            _messages.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

### シナリオ 2: `OpenFileDialog` の置き換え

**Before**:

```csharp
using (var dialog = new OpenFileDialog())
{
    dialog.Filter = "Text files (*.txt)|*.txt";
    if (dialog.ShowDialog() == DialogResult.OK)
        LoadFile(dialog.FileName);
}
```

**After**:

```csharp
private void OnOpenFile()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "Text files (*.txt)|*.txt",
    });

    if (result.IsOk)
        LoadFile(result.Value);
}
```

詳しい移行手順は [HowTo: 従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) を参照してください。

---

## M.E.DI との統合

`Microsoft.Extensions.DependencyInjection` を使った構成にも完全に対応します。**`net48` 専用** (M.E.DI 自体が `netstandard2.0+`)。

### `Program.cs` での構成

```csharp
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 1. ServiceCollection を構築
        var services = new ServiceCollection();

        // フレームワークサービス
        services.AddSingleton<IPlatformServices, DefaultPlatformServices>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IDialogProvider, DialogProvider>();
        services.AddSingleton<IFileService, FileService>();

        // 業務サービス
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderService, OrderService>();

        // Presenter
        services.AddTransient<UserEditorPresenter>();
        services.AddTransient<OrderProcessorPresenter>();

        var provider = services.BuildServiceProvider();

        // 2. PlatformServices をコンテナで上書き
        PlatformServices.Default = provider.GetRequiredService<IPlatformServices>();

        // 3. メインウィンドウ
        Application.Run(new MainForm(provider));
    }
}
```

### `MainForm` でのコンテナ利用

```csharp
public class MainForm : Form
{
    private readonly IServiceProvider _services;

    public MainForm(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    private void btnEditUser_Click(object sender, EventArgs e)
    {
        // コンテナから Presenter を取得
        var presenter = _services.GetRequiredService<UserEditorPresenter>();
        PlatformServices.Default.WindowNavigator.ShowWindowAsModal(presenter);
    }
}
```

### M.E.DI 統合の利点

- ✅ サービス登録を中央集約
- ✅ ライフタイム管理 (Singleton / Scoped / Transient)
- ✅ 自動的な依存解決
- ✅ 既存コードはそのまま動く (`PlatformServices.Default` 経由でアクセス)

**M.E.DI 統合はオプションです**。フレームワークは Service Locator か手動 Constructor Injection でも完全に動作します。

---

## 複数プロジェクト構成 (`WinformsMVP.DependencyInjection`)

複数の UI プロジェクトを持つ大規模ソリューションでは、オプションパッケージ **`WinformsMVP.DependencyInjection`** が `Microsoft.Extensions.DependencyInjection` と本フレームワークを綺麗に統合します。

メインパッケージ自体は BCL の `IServiceProvider` 抽象だけを知っているので、レガシーなプロジェクトはこの拡張を採用しない選択ができます。

### 2 つの直交する解決層

このフレームワークは、しばしば混同される 2 つの関心事を意図的に分離しています。

| レイヤー | 解決する問い | 担当 |
|---------|------------|----|
| **View 解決** | 「`IFooView` を実装する Form クラスは?」 | `IViewMappingRegister` (メインパッケージ) |
| **Service / Presenter 解決** | 「Presenter のコンストラクタ依存を埋める」 | `IServiceProvider` (任意の DI コンテナ) |

レガシーなプロジェクトは `IViewMappingRegister` だけ (任意で `Func<T>` Factory) で使えます。DI 連携が必要なプロジェクトは、2 つのブリッジを併用します。

- **`WithServiceProvider(provider)`** — View レジストリを拡張して、未登録のインターフェイスを DI コンテナに委譲
- **`IPresenterFactory`** — 親 Presenter が子 Presenter (DI 管理の依存を持つ) を生成するためのブリッジ

両ブリッジは独立しており、どちらか一方だけ、両方、どちらも使わない選択が可能です。

---

## `IPresenterFactory`: 子 Presenter の生成

親 Presenter が子ウィンドウを開くとき、子が DI 管理の依存を持っていると `new ChildPresenter()` では生成できません。代わりに **`IPresenterFactory`** を注入します。

```csharp
public class MainPresenter : WindowPresenterBase<IMainView>
{
    private readonly IPresenterFactory _presenters;

    public MainPresenter(IPresenterFactory presenters)
    {
        _presenters = presenters;
    }

    private void OnEditUser(int userId)
    {
        // (1) IPresenterFactory がコンストラクタ依存 (IUserRepository, ILogger, ...) を解決
        var presenter = _presenters.Create<EditUserPresenter>();

        // (2) Navigator がランタイムパラメータを IInitializable<TParam>.Initialize 経由で渡す
        var parameters = new EditUserParameters { UserId = userId, IsReadOnly = false };
        var result = Navigator.For(presenter)
                              .WithParam(parameters)
                              .ShowAsModal<UserResult>();
    }
}
```

### 重要な分離

| コンストラクタ (DI 管理) | `Initialize(TParam)` (Navigator 管理) |
|-------------------------|--------------------------------------|
| 安定した依存: Repository、Service、Logger | ランタイムデータ: ID、パス、モード、コンテキスト |
| コンテナのライフタイム単位で 1 つ | ウィンドウを開くたびに違う |
| `IPresenterFactory.Create<T>()` で解決される | `Navigator.WithParam(...)` で渡される |

「ランタイム引数を Presenter のコンストラクタに混ぜる」は **絶対に避けてください**。DI コンテナが解決できなくなります。
代わりに専用の Parameters クラス + `IInitializable<TParam>` パターンを使います。

### 内部実装: `ServiceProviderPresenterFactory`

`IPresenterFactory` の既定実装は `ServiceProviderPresenterFactory` (`WinformsMVP.DependencyInjection` パッケージ) で、`AddWinformsMVP()` を呼ぶと自動的に DI コンテナに登録されます。

実装は単純です:

```csharp
public class ServiceProviderPresenterFactory : IPresenterFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderPresenterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TPresenter Create<TPresenter>() where TPresenter : IPresenter
        => _serviceProvider.GetRequiredService<TPresenter>();
}
```

`GetRequiredService<T>()` を呼ぶだけなので、Presenter のコンストラクタ依存は **コンテナの通常の解決ルール** にそのまま乗ります。未登録の Presenter を要求すると `InvalidOperationException` がスローされる、という M.E.DI の標準セマンティクスをそのまま継承します。

カスタム Factory を使いたい場合は、`AddWinformsMVP()` を呼ぶ **前** に独自の `IPresenterFactory` を登録すれば、`TryAdd` セマンティクスにより上書きされません。

---

## `IModuleRegistrar`: モジュール単位の登録

各 UI サブプロジェクトが、自分の View と Service の登録を 1 か所に集めます。

### 2 つの Registrar インターフェイスの使い分け

フレームワークには **2 つの登録用インターフェイス** があり、目的に応じて選びます。

| インターフェイス | 名前空間 | パッケージ | 持つメソッド | 使うべき場面 |
|---|---|---|---|---|
| `IViewModuleRegistrar` | `WinformsMVP.Modules` | コア (`WinformsMVP`) | `RegisterViews()` のみ | DI を使わないプロジェクト、または DI ありアプリの中の DI 不要なレガシーモジュール |
| `IModuleRegistrar` | `WinformsMVP.DependencyInjection` | DI パッケージ | `RegisterViews()` + `RegisterServices()` | M.E.DI で Service・Presenter を登録するモジュール |

`IModuleRegistrar` は **`IViewModuleRegistrar` を継承** しています (`interface IModuleRegistrar : IViewModuleRegistrar`)。つまり「DI ありモジュール」は自動的に「View 専用モジュール」としても扱え、Shell プロジェクトは登録時にどちらの型でも受け取れます。

### IModuleRegistrar の典型実装

```csharp
// MyApp.UserModule.UI/UserModuleRegistrar.cs
public class UserModuleRegistrar : IModuleRegistrar
{
    public void RegisterViews(IViewMappingRegister registry)
        => registry.RegisterFromAssembly(typeof(UserModuleRegistrar).Assembly);

    public void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<UserListPresenter>();
        services.AddTransient<EditUserPresenter>();
        services.AddSingleton<IUserRepository, UserRepository>();
    }
}
```

> 💡 `RegisterFromAssembly(assembly)` は、指定したアセンブリ内のすべての Form を走査し、それが実装する `IXxxView` インターフェイス (`IWindowView` を継承するもの) を見つけて自動登録します。1 行で「このモジュールの全 View」を登録できる便利メソッドです。詳細は [ViewMappingRegister](Reference-ViewMappingRegister) を参照してください。

---

## 完全な Shell プロジェクトの起動

```csharp
// MyApp.Shell/Program.cs
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 1. View レジストリを作成
        var viewRegistry = new ViewMappingRegister();

        // 2. DI コンテナを作成し、各モジュールに登録させる
        var services = new ServiceCollection();
        services.RegisterModules(viewRegistry,
            new UserModuleRegistrar(),
            new OrderModuleRegistrar(),
            new ReportModuleRegistrar());

        // 3. フレームワーク自身のサービスを登録 (IPresenterFactory 等)
        services.AddWinformsMVP(viewRegistry);

        // 4. プロバイダを構築して PlatformServices に結ぶ
        var provider = services.BuildServiceProvider();
        var loggerFactory = LoggerFactory.Create(b => b.AddDebug());

        PlatformServices.Default = new DefaultPlatformServices(
            viewMappingRegister: viewRegistry,
            loggerFactory: loggerFactory.AsFrameworkLoggerFactory(),
            serviceProvider: provider);

        // 5. ルート Presenter をコンテナから取得して起動
        var mainPresenter = provider.GetRequiredService<MainShellPresenter>();
        PlatformServices.Default.WindowNavigator.ShowWindow(mainPresenter);

        Application.Run();
    }
}
```

### グローバル Dispatcher ミドルウェアの設定 (4 引数版コンストラクタ)

`DefaultPlatformServices` には **4 引数版** のコンストラクタもあり、第 4 引数 `configureDispatcher` (`Action<ViewActionDispatcher>`) ですべての Presenter に共通する `ViewActionDispatcher` のミドルウェアを設定できます。

```csharp
PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: viewRegistry,
    loggerFactory: loggerFactory.AsFrameworkLoggerFactory(),
    serviceProvider: provider,
    configureDispatcher: d => d
        .Use(new AuditMiddleware(auditSink, () => CurrentUser.Name))
        .Use(new AuthorizationMiddleware(currentUser))
        .Use(new ErrorDialogMiddleware(messages, dispatchLogger)));
```

監査ログ・認可チェック・テレメトリ・エラーダイアログ等、**横断的処理を 1 か所に集約** できます。ここで設定したミドルウェアは、`PresenterBase.SetView` の中で各 Presenter の Dispatcher に対して **最外層** として適用されるため、Presenter ローカルの `Use(...)` 呼び出しよりも先に走ります。

詳細・実装方法は [ViewAction システム > ミドルウェアパイプライン](Reference-ViewAction-System#ミドルウェアパイプライン) を参照してください。

### レガシープロジェクトの起動 (DI なし)

```csharp
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var viewRegistry = new ViewMappingRegister();
        viewRegistry.RegisterModules(
            new UserModuleRegistrar(),        // IViewModuleRegistrar (DI なし) を実装
            new OrderModuleRegistrar());

        PlatformServices.Default = new DefaultPlatformServices(viewRegistry);

        var mainPresenter = new MainShellPresenter();
        PlatformServices.Default.WindowNavigator.ShowWindow(mainPresenter);

        Application.Run();
    }
}
```

---

## よくある誤解

### 「Presenter を `IViewMappingRegister` に登録すべきか?」

**いいえ**。レジストリには View だけ登録します。Presenter は DI コンテナに入れる (または手動 `new`) ものです。

### 「DI コンテナを使うなら `IViewMappingRegister` は不要?」

**いいえ**。両方必要です。View 解決と Service 解決は別の関心事です。

### 「View を `IServiceProvider` から直接解決すべき?」

**推奨しません**。`WithServiceProvider(...)` でレジストリにコンテナをブリッジしてください。レジストリの明示登録が常に優先されます。

### 「レガシープロジェクトは `WinformsMVP.DependencyInjection` に依存すべき?」

**いいえ**。このパッケージはまさにそういうレガシープロジェクトが依存しなくて済むために存在します。

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — Service Locator パターンの基盤
- [WindowNavigator](Reference-WindowNavigator) — `IPresenterFactory` と Navigator パラメータの分離
- [ViewMappingRegister](Reference-ViewMappingRegister) — View 解決層の詳細
- [HowTo: 従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) — 段階的なリファクタリング
- サンプル:
  - `samples/MultiProjectDemo.Shell/Program.cs` — 完全な多プロジェクト構成
  - `samples/MultiProjectDemo.UserModule/UserModuleRegistrar.cs` — モジュール登録の実例
