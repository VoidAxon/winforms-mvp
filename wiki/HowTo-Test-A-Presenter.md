# HowTo: Presenter をテストする

このページでは、Presenter の単体テストを **モック View + モックサービス** で書く方法を、典型的なシナリオ別に示します。
Presenter は WinForms に依存しないため、**UI スレッド不要・実行高速・完全独立** で動かせます。

> **本フレームワークの主要メリットの 1 つが、まさにこのテスタビリティです。**

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
MockPlatformServices            (すべてのサービスを束ねるコンテナ)
   ├── MockMessageService       (Info/Warning/Error/Confirm の呼び出し記録)
   ├── MockDialogProvider       (Open/Save/Folder ダイアログの戻り値制御)
   ├── MockFileService          (in-memory ファイルシステム)
   ├── MockLoggerFactory        (ロギング記録、または NullLoggerFactory)
   └── MockWindowNavigator      (子ウィンドウ表示の検証)

Mock<IXxxView>                  (View インターフェイス用のモック)
```

すべてのモックは `WinformsMVP.Samples.Tests/Mocks/` 配下にあります。

---

## 基本的なテストの書き方

```csharp
public class UserEditorPresenterTests
{
    private readonly MockPlatformServices _platform;
    private readonly MockUserEditorView   _view;
    private readonly UserEditorPresenter  _presenter;

    public UserEditorPresenterTests()
    {
        // Arrange (共通) — 各テストで独立にインスタンス生成
        _platform = new MockPlatformServices();
        _view     = new MockUserEditorView();
        _presenter = new UserEditorPresenter()
            .WithPlatformServices(_platform);

        _presenter.AttachView(_view);
        _presenter.Initialize();

        // 初期化時の呼び出し記録をクリアして純粋なテスト実行に集中
        _platform.Reset();
        _view.MethodCalls.Clear();
    }

    [Fact]
    public void OnSave_WithValidData_ShowsSuccessMessage()
    {
        // Arrange
        _view.UserName = "John";
        _view.Email    = "john@example.com";

        // Act
        _presenter.Dispatcher.Dispatch(CommonActions.Save);

        // Assert
        Assert.True(_platform.MessageService.InfoMessageShown);
        Assert.Contains("saved", _platform.MessageService.LastInfoMessage);
    }
}
```

**AAA パターン** (Arrange / Act / Assert) で書くと、テストの意図が明確になります。

---

## シナリオ別のレシピ

### メッセージダイアログの呼び出しを検証する

```csharp
[Fact]
public void OnSave_WhenSaveFails_ShowsError()
{
    _view.UserName = "John";
    _platform.UserRepository.SaveShouldThrow = true;   // 失敗をシミュレート

    _presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.True(_platform.MessageService.ErrorMessageShown);
    Assert.Contains("Failed to save", _platform.MessageService.LastErrorMessage);
}

[Fact]
public void OnDelete_WithoutSelection_ShowsWarning()
{
    _view.SelectedId = null;   // 未選択

    _presenter.Dispatcher.Dispatch(CommonActions.Delete);

    Assert.True(_platform.MessageService.WarningMessageShown);
}
```

### ファイル選択ダイアログの戻り値を制御する

```csharp
[Fact]
public void OnImport_WhenUserSelectsFile_ImportsContent()
{
    // 戻り値をセット
    _platform.DialogProvider.OpenFileDialogResult =
        InteractionResult<string>.Ok("C:\\users.csv");
    _platform.FileService.AddFile("C:\\users.csv", "id,name\n1,John");

    _presenter.Dispatcher.Dispatch(ImportActions.Import);

    Assert.Equal(1, _view.Users.Count);
    Assert.Equal("John", _view.Users[0].Name);
}

[Fact]
public void OnImport_WhenUserCancels_DoesNothing()
{
    _platform.DialogProvider.OpenFileDialogResult =
        InteractionResult<string>.Cancel();

    _presenter.Dispatcher.Dispatch(ImportActions.Import);

    Assert.Equal(0, _view.Users.Count);
    Assert.False(_platform.FileService.ReadCount > 0);
}
```

### ファイル I/O をモックする

`MockFileService` は in-memory ファイルシステムを提供します。

```csharp
[Fact]
public void OnLoadConfig_ReadsFromFileSystem()
{
    _platform.FileService.AddFile("C:\\config.json", "{ \"theme\": \"dark\" }");

    _presenter.Dispatcher.Dispatch(ConfigActions.Load);

    Assert.Equal("dark", _view.Theme);
}

[Fact]
public void OnSaveConfig_WritesToFileSystem()
{
    _view.Theme = "light";

    _presenter.Dispatcher.Dispatch(ConfigActions.Save);

    Assert.Equal("{ \"theme\": \"light\" }",
        _platform.FileService.ReadAllText("C:\\config.json"));
}
```

### ConfirmYesNo の分岐をテストする

```csharp
[Fact]
public void OnDelete_UserConfirms_RemovesItem()
{
    _view.SelectedId = 42;
    _platform.MessageService.ConfirmYesNoResult = true;   // Yes を選択

    _presenter.Dispatcher.Dispatch(CommonActions.Delete);

    Assert.True(_platform.MessageService.ConfirmDialogShown);
    Assert.True(_platform.UserRepository.WasDeleted(42));
}

[Fact]
public void OnDelete_UserDeclines_DoesNotRemove()
{
    _view.SelectedId = 42;
    _platform.MessageService.ConfirmYesNoResult = false;  // No を選択

    _presenter.Dispatcher.Dispatch(CommonActions.Delete);

    Assert.True(_platform.MessageService.ConfirmDialogShown);
    Assert.False(_platform.UserRepository.WasDeleted(42));
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
    _platform.WindowNavigator.ShowModalBoolResult = true;   // Ok を返す

    _presenter.Dispatcher.Dispatch(MainActions.EditUser);

    Assert.True(_platform.WindowNavigator.ShowModalCalls
        .Any(p => p is EditUserPresenter));
    Assert.True(_view.ReloadCalled);
}

[Fact]
public void OnEditUser_WhenCancelled_DoesNotReload()
{
    _view.SelectedUserId = 1;
    _platform.WindowNavigator.ShowModalBoolResult = false;   // Cancel を返す

    _presenter.Dispatcher.Dispatch(MainActions.EditUser);

    Assert.False(_view.ReloadCalled);
}
```

> 結果あり版モーダルの `Value` は常に `default(TResult)` です。特定の戻り値に依存するアサーションが必要なら `MockWindowNavigator` を拡張してください。

### ロギングを検証する

メッセージを検証したい場合は `MockLogger` を使います。デフォルトの `MockPlatformServices` は `NullLoggerFactory` を使うのでログ出力なし (テストを汚さない)。

```csharp
[Fact]
public void OnSave_LogsSuccess()
{
    var loggerFactory = new MockLoggerFactory();
    var platform = new DefaultPlatformServices(null, loggerFactory);

    var presenter = new MyPresenter().WithPlatformServices(platform);
    presenter.AttachView(_view);
    presenter.Initialize();

    presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.Contains(loggerFactory.Logger.Entries,
        e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

詳細は [Reference-Logging#テストでの利用](Reference-Logging#テストでの利用) を参照。

---

## テストを駆動するときのルール

> **production と同じ経路を通すこと**。テストのために可視性を緩めない。

```csharp
// ❌ Wrong — private メソッドを直接呼ぶと CanExecute 判定を迂回する
presenter.OnSave();

// ✅ Right — Dispatcher 経由で呼ぶ (CanExecute も評価される)
presenter.Dispatcher.Dispatch(CommonActions.Save);

// ✅ Right — View イベントをシミュレート
view.RaiseClosing(CloseReason.Normal);

// ✅ Right — Presenter のイベントを購読
presenter.CloseRequested += (s, e) => captured = e;
```

`private` だったメソッドを `internal` に変えてテストすると、CanExecute スキップ等の不具合が production で発覚するまで隠れます。

---

## ベストプラクティス

### 1. 各テストで独立にモックを作る

```csharp
// ✅ コンストラクタで毎回新規生成 (xUnit はテストごとにクラスを new する)
public class MyTests
{
    private readonly MockPlatformServices _platform = new MockPlatformServices();
}

// ❌ static フィールドで共有するとテスト間で状態が汚染される
private static MockPlatformServices _platform = new MockPlatformServices();
```

### 2. AAA パターンで書く

```csharp
[Fact]
public void TestName()
{
    // Arrange — テストデータ・モック設定
    _view.TaskText = "Test";
    _platform.MessageService.ConfirmYesNoResult = true;

    // Act — テスト対象のアクション
    _presenter.Dispatcher.Dispatch(MyActions.DoSomething);

    // Assert — 期待値の検証
    Assert.True(_platform.MessageService.InfoMessageShown);
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
    _presenter.Dispatcher.Dispatch(CommonActions.Save);
    Assert.True(_platform.MessageService.InfoMessageShown);
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
    _presenter = new MyPresenter().WithPlatformServices(_platform);
    _presenter.AttachView(_view);
    _presenter.Initialize();

    // 初期化で発生した呼び出し記録をクリア
    _platform.Reset();
    _view.MethodCalls.Clear();
}
```

### 6. Mock View は最小限の状態を持たせる

```csharp
public class MockUserEditorView : IUserEditorView
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool HasUnsavedChanges { get; set; }

    public List<string> MethodCalls { get; } = new List<string>();

    public void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        MethodCalls.Add($"ShowValidationErrors({string.Join(",", errors)})");
    }

    public ViewActionBinder ActionBinder => null;   // テストでは Bind 不要
}
```

`ActionBinder` を `null` にすると、フレームワークの自動 Bind がスキップされてテストが軽くなります。Dispatcher 自体は依然として動作します。

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — モックサービスの API 一覧
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Dispatch` の動作
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — クローズ動作のテスト
- [Reference-Logging#テストでの利用](Reference-Logging#テストでの利用) — ロギングのモック
- サンプル:
  - `tests/WinformsMVP.Samples.Tests/Presenters/` — Presenter 単体テストの実例
  - `tests/WinformsMVP.Samples.Tests/Mocks/` — モック実装
