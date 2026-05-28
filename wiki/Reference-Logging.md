# Logging

本フレームワークは独自の **`WinformsMVP.Logging`** 名前空間に **`Microsoft.Extensions.Logging` 互換** の最小ロギング抽象を持っています。
メインパッケージ (`WinformsMVP`) は **外部依存ゼロ** で `net40;net48` をマルチターゲットし、`Microsoft.Extensions.Logging` の豊富なプロバイダエコシステムは **オプションでアプリ側のアダプタ** から接続します。

> **位置づけ**: Presenter は `Logger.LogInformation(...)` を呼ぶだけ。プロバイダ (Debug / Console / File / Application Insights / Seq) の有無は構成側 (Composition Root) でしか決まらないため、業務コードは無修正で出力先を切り替えられます。

---

## 目次

- [なぜ自社抽象か](#なぜ自社抽象か)
- [契約 (3 つの型)](#契約-3-つの型)
- [Presenter からの使い方](#presenter-からの使い方)
- [ログレベルとガイドライン](#ログレベルとガイドライン)
- [構造化ロギングのベストプラクティス](#構造化ロギングのベストプラクティス)
- [組み込み実装](#組み込み実装)
- [3 つの構成パス](#3-つの構成パス)
- [M.E.L. プロバイダのつなぎ方](#mel-プロバイダのつなぎ方)
- [テストでの利用](#テストでの利用)
- [関連ページ](#関連ページ)

---

## なぜ自社抽象か

`Microsoft.Extensions.Logging.Abstractions` は `net461` 以降が前提のため、`net40` をターゲットに含むこのフレームワークでは直接依存にできません。さらに、メインパッケージから外部依存が増えるとパッケージ更新のたびに整合性問題が発生するため、**自社契約 + オプションで M.E.L. 連携** という構成を採用しています。

| 設計判断 | 効果 |
|---------|------|
| BCL のみに依存 | `net40` ホストで動作 |
| `Microsoft.Extensions.Logging` と同じ API 表面 | コード読み書きが M.E.L. と同じ感覚で済む |
| メインパッケージは M.E.L. を知らない | M.E.L. 依存はアプリ側で必要なら追加 |
| Adapter は ~30 行 | 必要なら自分でコピペで書ける、ライブラリ化不要 |

---

## 契約 (3 つの型)

```csharp
namespace WinformsMVP.Logging
{
    public interface ILogger
    {
        bool IsEnabled(LogLevel level);
        void Log(LogLevel level, Exception exception, string message, params object[] args);
    }

    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        ILogger CreateLogger(Type type);
    }

    public enum LogLevel
    {
        Trace = 0, Debug = 1, Information = 2, Warning = 3,
        Error = 4, Critical = 5, None = 6,
    }
}
```

`LogLevel` の数値は `Microsoft.Extensions.Logging.LogLevel` と一致しているので、Adapter は単純キャストで変換できます。

便利な拡張メソッド (`LogInformation` / `LogWarning` / `LogError(ex, ...)` / `LogCritical` 等) は `WinformsMVP.Logging.LoggerExtensions` にあり、M.E.L. と同じシグネチャを使えます。

---

## Presenter からの使い方

すべての Presenter は `Logger` プロパティを自動で持っています。コンストラクタ注入は不要です。

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    protected override void OnInitialize()
    {
        Logger.LogInformation("MyPresenter initialized");
    }

    private void OnSave()
    {
        try
        {
            var userId = GetCurrentUserId();
            SaveData();

            // 名前付きプレースホルダー: {UserId} は MessageFormatter で正規化される
            Logger.LogInformation("User {UserId} saved data successfully", userId);
        }
        catch (Exception ex)
        {
            // 例外ロギングはコンテキスト付きで
            Logger.LogError(ex, "Failed to save data for user {UserId}", userId);
            Messages.ShowError("Failed to save data", "Error");
        }
    }

    private void OnComplexOperation()
    {
        Logger.LogDebug("Starting complex operation");
        Logger.LogInformation("Processing started");
        Logger.LogWarning("This might take a while");

        if (errorCondition)
            Logger.LogError("Operation failed due to {Reason}", reason);
    }
}
```

---

## ログレベルとガイドライン

| レベル | 使いどころ | 例 |
|-------|---------|----|
| **Trace** | 極めて詳細な診断情報 | あまり使わない |
| **Debug** | 詳細な診断情報 | `Logger.LogDebug("Cache hit for key {Key}", key)` |
| **Information** | 一般的な情報 | `Logger.LogInformation("User {UserName} logged in", userName)` |
| **Warning** | 注意すべき状態 | `Logger.LogWarning("Retry {Attempt} of {Max}", attempt, max)` |
| **Error** | 業務処理が失敗したが継続可能 | `Logger.LogError(ex, "Failed to process order {OrderId}", orderId)` |
| **Critical** | アプリ全体に影響する深刻なエラー | `Logger.LogCritical(ex, "Database connection lost")` |

---

## 構造化ロギングのベストプラクティス

名前付きプレースホルダー (`{UserName}`) と古典的なインデックス形式 (`{0}`) の両方が受け付けられます。名前付きは `WinformsMVP.Logging.MessageFormatter` で宣言順にインデックスへ正規化されます。

M.E.L. に橋渡しするアダプタを使った場合、構造化プロパティは **そのまま** 下流に流れます (アダプタが文字列を pre-format せず、M.E.L. の `Log<TState>` に委譲するため)。

### ✅ Good — 構造化パラメータ

```csharp
// 名前付きプレースホルダー — M.E.L. 経由で構造化データとしてキャプチャされる
Logger.LogInformation("User {UserName} opened document {DocumentId}", userName, docId);
```

### ❌ Bad — 文字列補間

```csharp
// 文字列補間は構造を失う — M.E.L. プロバイダで UserName で検索できなくなる
Logger.LogInformation($"User {userName} opened document {docId}");
```

ログ集約システム (Application Insights、Seq、Elasticsearch 等) を使う場合は、構造化を保つことが必須です。

---

## 組み込み実装

メインパッケージには 2 つの実装が付属します。

| 実装 | 出力先 | 依存 | 用途 |
|------|------|------|----|
| `NullLogger` / `NullLoggerFactory` | 出力なし (破棄) | なし | デフォルト、テスト、パフォーマンス重視のパス |
| `DebugLogger` / `DebugLoggerFactory` | `System.Diagnostics.Debug.WriteLine` | BCL のみ (`net40` 動作) | 開発中の Visual Studio 出力ウィンドウ |

`NullLoggerFactory.Instance` はシングルトンです。完全にログを無効化したい場合に使います (フレームワーク自身、未構成時にこれにフォールバックします)。

---

## 3 つの構成パス

### パス 1: 未構成 (デフォルト、サイレント)

```csharp
// PlatformServices.Default は NullLoggerFactory を自動使用
PlatformServices.Default = new DefaultPlatformServices();

// Logger.LogInformation(...) はコンパイル・実行されるが、出力なし
```

### パス 2: `DebugLoggerFactory` (`net40` 対応)

```csharp
using WinformsMVP.Logging;
using WinformsMVP.Services.Implementations;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 外部依存ゼロ、出力は VS Debug ウィンドウ
        PlatformServices.Default = new DefaultPlatformServices(
            viewMappingRegister: null,
            loggerFactory: new DebugLoggerFactory());

        Application.Run(new MainForm());
    }
}
```

### パス 3: Microsoft.Extensions.Logging ブリッジ (`net48` 専用)

メインパッケージは M.E.L. のアダプタパッケージを提供しません。実際のアプリケーションは Composition Root に **小さなアダプタを書きます** (~30 行)。
完成形のサンプルが [`MultiProjectDemo.Shell/Logging/`](https://github.com/VoidAxon/winforms-mvp/tree/master/src/MultiProjectDemo.Shell/Logging) にあるので、コピペで使えます。

アダプタを配置したあとの Composition Root:

```csharp
using Microsoft.Extensions.Logging;
using MyApp.Logging;                              // 自分のアプリのアダプタ名前空間
using WinformsMVP.Services.Implementations;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // M.E.L. の Factory を通常通り構築
        var msFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddDebug()
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        // 自分のアダプタの拡張メソッドでフレームワーク契約に変換
        PlatformServices.Default = new DefaultPlatformServices(
            viewMappingRegister: null,
            loggerFactory: msFactory.AsFrameworkLoggerFactory());

        Application.Run(new MainForm());
    }
}
```

> **`net40` ホストはパス 3 を使えません**。`Microsoft.Extensions.Logging.Abstractions` が `net461+` を要求するため。`net40` ではパス 1 (サイレント) かパス 2 (`DebugLoggerFactory`) を使ってください。

---

## M.E.L. プロバイダのつなぎ方

アダプタを通せば、M.E.L. のプロバイダエコシステム全体が使えます。ASP.NET Core と同じ感覚で設定し、Presenter の `Logger.LogInformation(...)` 呼び出しはそのまま流れます。

### 例 1: Azure Application Insights

```csharp
// NuGet: Microsoft.Extensions.Logging.ApplicationInsights
var msFactory = LoggerFactory.Create(builder =>
{
    builder.AddApplicationInsights(
        configureTelemetryConfiguration: config =>
        {
            config.ConnectionString = "InstrumentationKey=xxx";
        },
        configureApplicationInsightsLoggerOptions: options => { });
});

PlatformServices.Default = new DefaultPlatformServices(
    register, msFactory.AsFrameworkLoggerFactory());

// Presenter
Logger.LogInformation("Submitting order {OrderId} with correlation {CorrelationId}",
    orderId, correlationId);
// Application Insights クエリ: WHERE CorrelationId = 'xxx'
```

### 例 2: Seq (ローカル開発)

```csharp
// NuGet: Seq.Extensions.Logging
// Docker: docker run -d -p 5341:80 datalust/seq
var msFactory = LoggerFactory.Create(builder => builder.AddSeq("http://localhost:5341"));
PlatformServices.Default = new DefaultPlatformServices(register, msFactory.AsFrameworkLoggerFactory());
// ブラウザで http://localhost:5341 を開いて確認
```

### 例 3: ファイル出力 (Serilog 経由)

```csharp
// NuGet: Serilog.Extensions.Logging.File
var msFactory = LoggerFactory.Create(builder =>
    builder.AddFile("logs/app-{Date}.log", minimumLevel: LogLevel.Information));
PlatformServices.Default = new DefaultPlatformServices(register, msFactory.AsFrameworkLoggerFactory());
```

---

## テストでの利用

### デフォルトの動作 (NullLoggerFactory・出力なし)

```csharp
[Fact]
public void Test_MyPresenter()
{
    // MockPlatformServices は LoggerFactory を NullLoggerFactory.Instance に初期化
    var mockPlatform = new MockPlatformServices();
    var presenter = new MyPresenter().WithPlatformServices(mockPlatform);

    presenter.AttachView(mockView);
    presenter.Initialize();

    // Logger 呼び出しはコンパイル・実行されるが出力なし — オーバーヘッドゼロ
}
```

### キャプチャ用 Logger でメッセージを検証する

```csharp
private class MockLogger : ILogger
{
    public List<(LogLevel Level, string Message, Exception Ex)> Entries { get; } = new();

    public bool IsEnabled(LogLevel level) => true;

    public void Log(LogLevel level, Exception exception, string message, params object[] args)
        => Entries.Add((level, MessageFormatter.Format(message, args), exception));
}

private class MockLoggerFactory : ILoggerFactory
{
    public MockLogger Logger { get; } = new MockLogger();
    public ILogger CreateLogger(string categoryName) => Logger;
    public ILogger CreateLogger(Type type) => Logger;
}

[Fact]
public void OnSave_ShouldLogSuccess()
{
    var loggerFactory = new MockLoggerFactory();
    var platform = new DefaultPlatformServices(null, loggerFactory);

    var presenter = new MyPresenter();
    presenter.SetPlatformServices(platform);
    presenter.AttachView(mockView);
    presenter.Initialize();

    presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.Contains(loggerFactory.Logger.Entries,
        e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

完全なテストパターン集は `src/WinformsMVP.Samples.Tests/LoggingIntegrationTests.cs` にあります。

---

## 設計上の主要プロパティ

| プロパティ | 説明 |
|----------|----|
| `net40` 互換 | メインパッケージは M.E.L. に依存しない |
| デフォルトサイレント | `NullLoggerFactory.Instance` — 予期しない出力なし、起動コストもなし |
| 親しみのある API 表面 | 拡張メソッド名・プレースホルダー記法が M.E.L. と一致 |
| エコシステム連携はオプトイン | `net48` ホストでアプリ側の ~30 行アダプタで M.E.L. プロバイダが使える |
| テスタブル | 2 メソッドの `ILogger` 契約は容易にモック可能 |
| 構造化保持 | フォワーディングアダプタが M.E.L. の `Log<TState>` に直接委譲するため、プロパティが下流に流れる |

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — Presenter の `Logger` プロパティの位置づけ
- [Dependency Injection](Reference-DependencyInjection) — Logging を含む DI コンテナ統合
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テスト時のサイレント化・キャプチャパターン
- サンプル:
  - [`src/WinformsMVP.Samples/LoggingDemoExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/src/WinformsMVP.Samples/LoggingDemoExample.cs) — 基本デモ
  - [`src/MultiProjectDemo.Shell/Logging/`](https://github.com/VoidAxon/winforms-mvp/tree/master/src/MultiProjectDemo.Shell/Logging) — M.E.L. アダプタの実装例
