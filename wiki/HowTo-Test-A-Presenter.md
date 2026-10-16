# HowTo: Presenter をテストする

このページでは、Presenter の単体テストを **モック View + モックサービス** で書く方法を、典型的なシナリオ別に示します。
Presenter は WinForms に依存しないため、**UI スレッド不要・実行高速・完全独立** で動かせます。

> **本フレームワークの主要メリットの 1 つが、まさにこのテスタビリティです。**

> **ターゲットについて:** Presenter と基底クラスは `WinformsMVP.Core` (`netstandard2.0`、WinForms 非参照) にあるため、Presenter だけのテストプロジェクトは Core を参照すれば任意のランタイムで動かせます (このとき `ServiceLocator` の既定プロバイダには `FileService` と `NullLoggerFactory` しか入らないので、フレームワークサービスはモックを渡してください)。本リポジトリの `WinformsMVP.Samples.Tests` は Form も扱う `net48` プロジェクトなので、`dotnet test` には Windows (または mono) が必要です。

---

## 目次

- [テスト戦略](#テスト戦略)
- [モックの全体像](#モックの全体像)
- [基本的なテストの書き方](#基本的なテストの書き方)
- [シナリオ別のレシピ](#シナリオ別のレシピ)
  - [メッセージダイアログの呼び出しを検証する](#メッセージダイアログの呼び出しを検証する)
  - [ファイル選択ダイアログの戻り値を制御する](#ファイル選択ダイアログの戻り値を制御する)
  - [ファイル I/O をモックする](#ファイル-io-をモックする)
  - [ConfirmYesNo の分岐をテストする](#confirmyesno-の分岐をテストする)
  - [子ウィンドウの表示を検証する](#子ウィンドウの表示を検証する)
  - [ロギングを検証する](#ロギングを検証する)
- [テストを駆動するときのルール](#テストを駆動するときのルール)
- [ベストプラクティス](#ベストプラクティス)
- [関連ページ](#関連ページ)

---

## テスト戦略

| レイヤー | テスト方針 |
|---------|---------|
| **Presenter** (← 本ページの主題) | モック View + モックサービスで完全独立 |
| **Model** | 通常のドメイン単体テスト |
| **View** | UI 自動化または手動テスト (フレームワークではない) |

「View は WinForms がテスト済み」と割り切り、業務ロジックを 100% Presenter 側で書いて単体テストするのが基本戦略です。

---

## モックの全体像

```
MockServices                    (モックサービスを束ね DefaultServiceProvider に登録するコンテナ)
   ├── MockMessageService       (Info/Warning/Error/Confirm の呼び出し記録)
   ├── MockDialogProvider       (Open/Save/Folder ダイアログの戻り値制御)
   ├── MockFileService          (in-memory ファイルシステム)
   ├── LoggerFactory            (既定は NullLoggerFactory・出力なし)
   └── MockWindowNavigator      (子ウィンドウ表示の検証)
   .Provider                    (DefaultServiceProvider — presenter.WithServiceProvider(mock.Provider) に渡す)

Mock<IXxxView>                  (View インターフェイス用のモック — CapturingActionBinder を公開する)
CapturingActionBinder           (フレームワークが View に渡す dispatcher を捕捉。テストから Dispatch する入口)
業務依存 (IXxxRepository 等)     (Presenter のコンストラクタに注入。フレームワークサービスとは別経路)
```

モックは `WinformsMVP.Samples.Tests/Mocks/` 配下にあります。**業務依存 (Repository など) は `MockServices` には含まれません** — それらは Presenter のコンストラクタに直接注入します (下記参照)。

---

## 基本的なテストの書き方

```csharp
public class UserEditorPresenterTests
{
    private readonly MockServices        _mockServices;
    private readonly MockUserRepository  _repository;   // 業務依存はコンストラクタ注入
    private readonly MockUserEditorView  _view;
    private readonly UserEditorPresenter _presenter;

    public UserEditorPresenterTests()
    {
        // Arrange (共通) — 各テストで独立にインスタンス生成
        _mockServices = new MockServices();
        _repository   = new MockUserRepository();
        _view         = new MockUserEditorView();

        // フレームワークサービスは WithServiceProvider 経由、業務依存 (Repository) はコンストラクタ経由
        _presenter = new UserEditorPresenter(_repository)
            .WithServiceProvider(_mockServices.Provider);

        _presenter.AttachView(_view);   // AttachView を忘れて Initialize すると InvalidOperationException
        _presenter.Initialize();

        // 初期化時の呼び出し記録をクリアして純粋なテスト実行に集中
        _mockServices.Reset();
        _view.MethodCalls.Clear();
    }

    [Fact]
    public void OnSave_WithValidData_ShowsSuccessMessage()
    {
        // Arrange
        _view.UserName = "John";
        _view.Email    = "john@example.com";

        // Act
        _view.Binder.Dispatch(StandardActions.Save);

        // Assert — 種別ブール + 本文は HasCall / GetLastCall().Message で検証
        Assert.True(_mockServices.MessageService.InfoMessageShown);
        Assert.True(_mockServices.MessageService.HasCall(MessageType.Info, "saved"));
    }
}
```

**AAA パターン** (Arrange / Act / Assert) で書くと、テストの意図が明確になります。

> **アクションは `_view.Binder.Dispatch(...)` で駆動します。** Presenter の `Dispatcher` は protected で、テスト用に公開されることはありません。代わりにモック View が `CapturingActionBinder` を公開します — フレームワークは `Initialize()` 中に `View.ActionBinder.Bind(dispatcher)` を自動で呼ぶため、binder はそこで dispatcher を捕捉できます。本物の Form のボタンが使うのと同一の経路なので、`CanExecute` もミドルウェアも production と同じように評価されます。

```csharp
// tests/WinformsMVP.Samples.Tests/Mocks/CapturingActionBinder.cs (リポジトリに同梱 — 要点のみ抜粋)
public sealed class CapturingActionBinder : IViewActionBinder
{
    public ViewActionDispatcher Dispatcher { get; private set; }

    public void Bind(ViewActionDispatcher dispatcher) => Dispatcher = dispatcher;   // フレームワークが呼ぶ

    public void Dispatch(ViewAction action, object payload = null) => Dispatcher.Dispatch(action, payload);
    public bool CanDispatch(ViewAction action) => Dispatcher.CanDispatch(action);
}
```

モック View 側は 2 行で公開します:

```csharp
public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
public IViewActionBinder ActionBinder => Binder;
```

> **業務依存はコンストラクタ、フレームワークサービスは `WithServiceProvider`。** `MockServices` が束ねるのは Message/Dialog/File/Navigator (+ Logger) の **フレームワークサービス** だけです。`IUserRepository` のような業務依存は Presenter のコンストラクタに注入します (`new UserEditorPresenter(_repository)`)。実例は [`MainEmailPresenterTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/WinformsMVP.Samples.Tests/Presenters/MainEmailPresenterTests.cs) の `new MainEmailPresenter(_mockRepository).WithServiceProvider(_mockServices.Provider)` を参照。

---

## シナリオ別のレシピ

### メッセージダイアログの呼び出しを検証する

```csharp
[Fact]
public void OnSave_WhenSaveFails_ShowsError()
{
    _view.UserName = "John";
    _repository.SaveShouldThrow = true;   // 失敗をシミュレート (コンストラクタ注入した mock repo)

    _view.Binder.Dispatch(StandardActions.Save);

    Assert.True(_mockServices.MessageService.ErrorMessageShown);
    Assert.True(_mockServices.MessageService.HasCall(MessageType.Error, "Failed to save"));
}

[Fact]
public void OnDelete_WithoutSelection_ShowsWarning()
{
    _view.SelectedId = null;   // 未選択

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.True(_mockServices.MessageService.WarningMessageShown);
}
```

### ファイル選択ダイアログの戻り値を制御する

```csharp
[Fact]
public void OnImport_WhenUserSelectsFile_ImportsContent()
{
    // 戻り値をセット — OpenFileDialogResult は string。値を入れると Ok(その値)、空/null なら Cancel が返る
    _mockServices.DialogProvider.OpenFileDialogResult = "C:\\users.csv";
    _mockServices.FileService.AddFile("C:\\users.csv", "id,name\n1,John");

    _view.Binder.Dispatch(ImportActions.Import);

    Assert.Equal(1, _view.Users.Count);
    Assert.Equal("John", _view.Users[0].Name);
}

[Fact]
public void OnImport_WhenUserCancels_DoesNothing()
{
    _mockServices.DialogProvider.OpenFileDialogResult = null;   // null/空 → Cancel をシミュレート

    _view.Binder.Dispatch(ImportActions.Import);

    Assert.Equal(0, _view.Users.Count);   // キャンセル時は取り込みが起きない
}
```

### ファイル I/O をモックする

`MockFileService` は in-memory ファイルシステムを提供します。

```csharp
[Fact]
public void OnLoadConfig_ReadsFromFileSystem()
{
    _mockServices.FileService.AddFile("C:\\config.json", "{ \"theme\": \"dark\" }");

    _view.Binder.Dispatch(ConfigActions.Load);

    Assert.Equal("dark", _view.Theme);
}

[Fact]
public void OnSaveConfig_WritesToFileSystem()
{
    _view.Theme = "light";

    _view.Binder.Dispatch(ConfigActions.Save);

    Assert.Equal("{ \"theme\": \"light\" }",
        _mockServices.FileService.ReadAllText("C:\\config.json"));
}
```

### ConfirmYesNo の分岐をテストする

```csharp
[Fact]
public void OnDelete_UserConfirms_RemovesItem()
{
    _view.SelectedId = 42;
    _mockServices.MessageService.ConfirmYesNoResult = true;   // Yes を選択

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.True(_mockServices.MessageService.ConfirmDialogShown);
    Assert.True(_repository.WasDeleted(42));
}

[Fact]
public void OnDelete_UserDeclines_DoesNotRemove()
{
    _view.SelectedId = 42;
    _mockServices.MessageService.ConfirmYesNoResult = false;  // No を選択

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.True(_mockServices.MessageService.ConfirmDialogShown);
    Assert.False(_repository.WasDeleted(42));
}
```

### 子ウィンドウの表示を検証する

`MockWindowNavigator` で子 Presenter の起動と戻り値をセットします。

モーダルの戻りは `ShowModalBoolResult` で切り替えます (`true`→`Ok` / `false`→`Cancel`)。表示された Presenter は `ShowModalCalls` で検証します。

```csharp
[Fact]
public void OnEditUser_OpensEditorAndReloadsOnSuccess()
{
    _view.SelectedUserId = 1;
    _mockServices.WindowNavigator.ShowModalBoolResult = true;   // Ok を返す

    _view.Binder.Dispatch(MainActions.EditUser);

    Assert.True(_mockServices.WindowNavigator.ShowModalCalls
        .Any(p => p is EditUserPresenter));
    Assert.True(_view.ReloadCalled);
}

[Fact]
public void OnEditUser_WhenCancelled_DoesNotReload()
{
    _view.SelectedUserId = 1;
    _mockServices.WindowNavigator.ShowModalBoolResult = false;   // Cancel を返す

    _view.Binder.Dispatch(MainActions.EditUser);

    Assert.False(_view.ReloadCalled);
}
```

> 結果あり版モーダルの `Value` は常に `default(TResult)` です。特定の戻り値に依存するアサーションが必要なら `MockWindowNavigator` を拡張してください。

### ロギングを検証する

既定の `MockServices` は `NullLoggerFactory` を使うのでログ出力はありません (テストを汚さない)。ログ内容を検証したい場合は、エントリを記録する小さな `ILogger` を**自分で用意し**、`DefaultServiceProvider` 経由で差し込みます (テストプロジェクトに専用モックは同梱されていません)。

```csharp
// 記録用ロガー (テストファイル内などに用意する小さなヘルパー)
private class RecordingLogger : ILogger
{
    public List<(LogLevel Level, string Message, Exception Ex)> Entries { get; } = new();
    public bool IsEnabled(LogLevel level) => true;
    public void Log(LogLevel level, Exception exception, string message, params object[] args)
        => Entries.Add((level, MessageFormatter.Format(message, args), exception));
}

private class RecordingLoggerFactory : ILoggerFactory
{
    public RecordingLogger Logger { get; } = new RecordingLogger();
    public ILogger CreateLogger(string categoryName) => Logger;
    public ILogger CreateLogger(Type type) => Logger;
}

[Fact]
public void OnSave_LogsSuccess()
{
    var loggerFactory = new RecordingLoggerFactory();
    var sp = new DefaultServiceProvider();
    sp.RegisterInstance<WinformsMVP.Logging.ILoggerFactory>(loggerFactory);

    var presenter = new MyPresenter().WithServiceProvider(sp);
    presenter.AttachView(_view);
    presenter.Initialize();

    _view.Binder.Dispatch(StandardActions.Save);

    Assert.Contains(loggerFactory.Logger.Entries,
        e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

同じ記録用ロガーのコピー&ペースト版と詳細は [Reference-Logging#テストでの利用](Reference-Logging#テストでの利用) を参照。

---

## テストを駆動するときのルール

> **production と同じ経路を通すこと**。テストのために可視性を緩めない。

```csharp
// ❌ Wrong — private メソッドを直接呼ぶと CanExecute 判定を迂回する
presenter.OnSave();

// ✅ Right — View の binder 経由で Dispatch (CanExecute も評価される)
view.Binder.Dispatch(StandardActions.Save);
```

`Dispatcher` を public/internal に広げてテストするのも誤りです — `CapturingActionBinder` (上記) が公開 API だけで同じ経路を提供します。

### ウィンドウクローズのテスト

Pull ゲート (`CanClose`) は `ICloseParticipant.CanCloseGate` を通じてテストします。`ICloseParticipant` は internal で、テストプロジェクトは `InternalsVisibleTo` で到達できます:

```csharp
// Pull direction (CanClose) — ICloseParticipant.CanCloseGate
bool? allow = null;
((ICloseParticipant)presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);
Assert.False(allow);   // e.g. dirty state blocks close

// System shutdown is never blocked
((ICloseParticipant)presenter).CanCloseGate(CloseReason.SystemShutdown, ok => allow = ok);
Assert.True(allow);
```

Push direction (`RequestClose`) は recording sink を注入してテストします:

```csharp
private sealed class RecordingSink : ICloseSink
{
    public readonly List<(object result, InteractionStatus status)> Closed
        = new List<(object, InteractionStatus)>();
    public void Close(object result, InteractionStatus status)
        => Closed.Add((result, status));
}

// In the test:
var sink = new RecordingSink();
((ICloseParticipant)presenter).BindCloseSink(sink);

view.Binder.Dispatch(StandardActions.Save);

Assert.Single(sink.Closed);
Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
```

`private` だったメソッドを `internal` に変えてテストすると、CanExecute スキップ等の不具合が production で発覚するまで隠れます。

完全な実例: `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs` および `WindowClosingDemoPresenterTests.cs`。

---

## ベストプラクティス

### 1. 各テストで独立にモックを作る

```csharp
// ✅ コンストラクタで毎回新規生成 (xUnit はテストごとにクラスを new する)
public class MyTests
{
    private readonly MockServices _mockServices = new MockServices();
}

// ❌ static フィールドで共有するとテスト間で状態が汚染される
private static MockServices _mockServices = new MockServices();
```

### 2. AAA パターンで書く

```csharp
[Fact]
public void TestName()
{
    // Arrange — テストデータ・モック設定
    _view.TaskText = "Test";
    _mockServices.MessageService.ConfirmYesNoResult = true;

    // Act — テスト対象のアクション
    _view.Binder.Dispatch(MyActions.DoSomething);

    // Assert — 期待値の検証
    Assert.True(_mockServices.MessageService.InfoMessageShown);
}
```

### 3. 説明的なテスト名にする

```csharp
// ✅ Good — 何をテストしているか分かる
AddTask_WithValidText_AddsTaskToView()
AddTask_WithEmptyText_ShowsWarning()
RemoveTask_UserConfirms_RemovesTask()

// ❌ Bad — 何を検証しているか不明
Test1()
TestAddTask()
```

### 4. 1 テスト 1 主題

```csharp
// ✅ 関連性の高い検証は同じテストに含めてよい
[Fact]
public void OnSave_ShowsMessageAndUpdatesView()
{
    _view.Binder.Dispatch(StandardActions.Save);
    Assert.True(_mockServices.MessageService.InfoMessageShown);
    Assert.Equal(1, _view.SavedCount);
}

// ❌ 関連の薄い検証を 1 テストに詰め込まない
[Fact]
public void TestEverything()
{
    // Save、Delete、Refresh、Load、Export を全部検証... → 失敗時に原因特定が困難
}
```

### 5. 初期化時の状態を Reset する

```csharp
public MyTests()
{
    _presenter = new MyPresenter().WithServiceProvider(_mockServices.Provider);
    _presenter.AttachView(_view);
    _presenter.Initialize();

    // 初期化で発生した呼び出し記録をクリア
    _mockServices.Reset();
    _view.MethodCalls.Clear();
}
```

### 6. Mock View は最小限の状態を持たせる

```csharp
public class MockUserEditorView : IUserEditorView
{
    public string UserName { get; set; }
    public string Email { get; set; }

    public List<string> MethodCalls { get; } = new List<string>();

    public void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        MethodCalls.Add($"ShowValidationErrors({string.Join(",", errors)})");
    }

    public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
    public IViewActionBinder ActionBinder => Binder;
}
```

`CapturingActionBinder` は本物の `ViewActionBinder` と違い WinForms のコントロール戦略を登録しないため軽量で、そのままテストの Dispatch 入口を兼ねます。なお `ActionBinder` プロパティの型は `IViewActionBinder` です (`IWindowView` から継承 — View インターフェイス側での再宣言は不要)。

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — モックサービスの API 一覧
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Dispatch` の動作
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — クローズ動作のテスト
- [Reference-Logging#テストでの利用](Reference-Logging#テストでの利用) — ロギングのモック
- サンプル:
  - `tests/WinformsMVP.Samples.Tests/Presenters/` — Presenter 単体テストの実例
  - `tests/WinformsMVP.Samples.Tests/Mocks/` — モック実装
