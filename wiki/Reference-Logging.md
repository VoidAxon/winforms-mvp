# Logging

本フレームワークは独自の **`Kibinago.Logging`** 名前空間に **`Microsoft.Extensions.Logging` 互換** の最小ロギング抽象を持っています。
ロギング抽象はコアパッケージ (`Kibinago.Core`, `netstandard2.0`) にあり **外部依存ゼロ** です。`Microsoft.Extensions.Logging` の豊富なプロバイダエコシステムは **オプションでアプリ側のアダプタ** から接続します。

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

コアパッケージから外部依存が増えるとパッケージ更新のたびに整合性問題 (バージョン衝突・バインディングリダイレクト) が発生するため、**自社契約 + オプションで M.E.L. 連携** という構成を採用しています。

| 設計判断 | 効果 |
|---------|------|
| BCL のみに依存 | コアパッケージが外部依存ゼロを保てる |
| `Microsoft.Extensions.Logging` と同じ API 表面 | コード読み書きが M.E.L. と同じ感覚で済む |
| フレームワークは M.E.L. を知らない | M.E.L. 依存はアプリ側で必要なら追加 |
| Adapter は ~30 行 | 必要なら自分でコピペで書ける、ライブラリ化不要 |

---

## 契約 (3 つの型)

```csharp
namespace Kibinago.Logging
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

便利な拡張メソッド (`LogInformation` / `LogWarning` / `LogError(ex, ...)` / `LogCritical` 等) は `Kibinago.Logging.LoggerExtensions` にあり、M.E.L. と同じシグネチャを使えます。

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

名前付きプレースホルダー (`{UserName}`) と古典的なインデックス形式 (`{0}`) の両方が受け付けられます。名前付きは `Kibinago.Logging.MessageFormatter` で宣言順にインデックスへ正規化されます。M.E.L. / Serilog のキャプチャ指定 `{@Name}` / `{$Name}` も名前として受け付け、同じ名前が繰り返し現れた場合は同じ引数を再利用します (`"{Id} ... {Id}"` は引数 1 つ)。

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

コアパッケージには 2 つの実装が付属します。

| 実装 | 出力先 | 依存 | 用途 |
|------|------|------|----|
| `NullLogger` / `NullLoggerFactory` | 出力なし (破棄) | なし | デフォルト、テスト、パフォーマンス重視のパス |
| `DebugLogger` / `DebugLoggerFactory` | `System.Diagnostics.Trace.WriteLine` (Release ビルドでも出力) | BCL のみ | Visual Studio 出力ウィンドウ / 任意の `TraceListener` |

`NullLoggerFactory.Instance` はシングルトンです。完全にログを無効化したい場合に使います (フレームワーク自身、未構成時にこれにフォールバックします)。

---

## 3 つの構成パス

### パス 1: 未構成 (デフォルト、サイレント)

```csharp
// ServiceLocator.Current はデフォルトで NullLoggerFactory を使用
// — 設定不要。Logger.LogInformation(...) はコンパイル・実行されるが、出力なし
```

### パス 2: `DebugLoggerFactory`

```csharp
using Kibinago.Logging;
using Kibinago.Services.Implementations;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 外部依存ゼロ、出力は Trace (VS 出力ウィンドウ / 登録済み TraceListener)
        ServiceLocator.Configure(reg =>
            reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(new DebugLoggerFactory()));

        Application.Run(new MainForm());
    }
}
```

### パス 3: Microsoft.Extensions.Logging ブリッジ

フレームワークは M.E.L. のアダプタパッケージを提供しません。実際のアプリケーションは Composition Root に **小さなアダプタを書きます** (~30 行)。
完成形のサンプルが [`MultiProjectDemo.Shell/Logging/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/MultiProjectDemo.Shell/Logging) にあるので、コピペで使えます。

アダプタを配置したあとの Composition Root:

```csharp
using Microsoft.Extensions.Logging;
using MyApp.Logging;                              // 自分のアプリのアダプタ名前空間
using Kibinago.Services.Implementations;

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
        ServiceLocator.Configure(reg =>
            reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(msFactory.AsFrameworkLoggerFactory()));

        Application.Run(new MainForm());
    }
}
```

> 対応ターゲット (`net472` 以降 / `net10.0-windows`) ではすべてのパスが使えます (以前の `net40` ホストではパス 3 が使えませんでしたが、`net40` のサポートは終了しました)。

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

ServiceLocator.Configure(reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(msFactory.AsFrameworkLoggerFactory());
});

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
ServiceLocator.Configure(reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(msFactory.AsFrameworkLoggerFactory());
});
// ブラウザで http://localhost:5341 を開いて確認
```

### 例 3: ファイル出力 (Serilog 経由)

```csharp
// NuGet: Serilog.Extensions.Logging.File
var msFactory = LoggerFactory.Create(builder =>
    builder.AddFile("logs/app-{Date}.log", minimumLevel: LogLevel.Information));
ServiceLocator.Configure(reg =>
{
    reg.RegisterInstance<IViewMappingRegister>(register);
    reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(msFactory.AsFrameworkLoggerFactory());
});
```

---

## テストでの利用

### デフォルトの動作 (NullLoggerFactory・出力なし)

```csharp
[Fact]
public void Test_MyPresenter()
{
    // TestServices.Create() は無出力の ILoggerFactory (NullLoggerFactory) だけを登録したプロバイダ
    var sp = TestServices.Create();   // Kibinago.Testing

    PresenterTestHost.Start(sp, new MyPresenter(), mockView);   // アタッチ + 実際の初期化

    // Logger 呼び出しはコンパイル・実行されるが出力なし — オーバーヘッドゼロ
}
```

### 記録用 Logger でメッセージを検証する

`Kibinago.Testing` の `RecordingLoggerFactory` はすべてのレベルを記録します。`TestServices.CreateFakes()` が登録済みで、`fakes.Logs` として公開されています (自作のロガーを書く必要はありません):

```csharp
[Fact]
public void OnSave_ShouldLogSuccess()
{
    var fakes = TestServices.CreateFakes();   // fakes.Logs = RecordingLoggerFactory
    PresenterTestHost.Start(fakes.Provider, new MyPresenter(), mockView);

    mockView.Binder.Dispatch(StandardActions.Save);

    Assert.Contains(fakes.Logs.Entries,
        e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

`LogEntry` は `Category`、`Level`、`Exception`、`Message` (プレースホルダー展開済み)、`Template`、`Args` を持ちます。フェイクを使わずに組む場合は `TestServices.Create()` のプロバイダに `new RecordingLoggerFactory()` を `ILoggerFactory` として登録します (無出力のものを置き換えます)。

完全なテストパターン集は `tests/Kibinago.Samples.Tests/LoggingIntegrationTests.cs` にあります。

---

## 設計上の主要プロパティ

| プロパティ | 説明 |
|----------|----|
| 外部依存ゼロ | コアパッケージは M.E.L. に依存しない |
| デフォルトサイレント | `NullLoggerFactory.Instance` — 予期しない出力なし、起動コストもなし |
| 親しみのある API 表面 | 拡張メソッド名・プレースホルダー記法が M.E.L. と一致 |
| エコシステム連携はオプトイン | アプリ側の ~30 行アダプタで M.E.L. プロバイダが使える (全ターゲット) |
| テスタブル | 2 メソッドの `ILogger` 契約は容易にモック可能 |
| 構造化保持 | フォワーディングアダプタが M.E.L. の `Log<TState>` に直接委譲するため、プロパティが下流に流れる |

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — Presenter の `Logger` プロパティの位置づけ
- [Dependency Injection](Reference-DependencyInjection) — Logging を含む DI コンテナ統合
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テスト時のサイレント化・キャプチャパターン
- サンプル:
  - [`samples/Kibinago.Samples/LoggingDemoExample.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/samples/Kibinago.Samples/LoggingDemoExample.cs) — 基本デモ
  - [`samples/MultiProjectDemo.Shell/Logging/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/MultiProjectDemo.Shell/Logging) — M.E.L. アダプタの実装例
