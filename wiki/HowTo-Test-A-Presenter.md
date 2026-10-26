# HowTo: Presenter をテストする

このページでは、Presenter の単体テストを **モック View + フェイクサービス** (`Kibinago.Testing` パッケージ) で書く方法を、典型的なシナリオ別に示します。
Presenter は WinForms に依存しないため、**UI スレッド不要・実行高速・完全独立** で動かせます。

> **本フレームワークの主要メリットの 1 つが、まさにこのテスタビリティです。**

> **パッケージ:** テストプロジェクトは `Kibinago.Testing` (`netstandard2.0`、`Kibinago.Core` に依存、テストフレームワーク非依存) を参照します (`dotnet add package Kibinago.Testing --prerelease`。`dotnet new kibinago-tests` は参照済み)。`PresenterTestHost`、`TestServices`、`CapturingActionBinder` と、フレームワークサービスのフェイクが入っています。どれも任意で、自作のフェイクや Moq と混ぜて使えます。
>
> **ターゲットについて:** Presenter と基底クラスは `Kibinago.Core` (`netstandard2.0`、WinForms 非参照) にあるため、Presenter だけのテストプロジェクトは任意のランタイムで動かせます。本リポジトリの `Kibinago.Samples.Tests` は Form も扱う `net48` プロジェクトなので、`dotnet test` には Windows (または mono) が必要です。

---

## 目次

- [テスト戦略](#テスト戦略)
- [モックの全体像](#モックの全体像)
- [基本的なテストの書き方](#基本的なテストの書き方)
- [シナリオ別のレシピ](#シナリオ別のレシピ)
  - [メッセージダイアログの呼び出しを検証する](#メッセージダイアログの呼び出しを検証する)
  - [ConfirmYesNo の分岐をテストする](#confirmyesno-の分岐をテストする)
  - [ファイル選択ダイアログの戻り値を制御する](#ファイル選択ダイアログの戻り値を制御する)
  - [ファイル I/O をフェイクにする](#ファイル-io-をフェイクにする)
  - [子ウィンドウの表示を検証する](#子ウィンドウの表示を検証する)
  - [ロギングを検証する](#ロギングを検証する)
  - [非同期処理をテストする](#非同期処理をテストする)
  - [子 Presenter を含む画面のテスト](#子-presenter-を含む画面のテスト)
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
TestServices.CreateFakes()      → FakeServices (Kibinago.Testing)
   ├── Messages   FakeMessageService      呼び出しを記録し、確認ダイアログの答えをスクリプトで返す
   ├── Toasts     FakeToastService        トーストを記録
   ├── Dialogs    FakeDialogProvider      ファイル / フォルダ / 色ダイアログの答えをスクリプトで返す
   ├── Files      InMemoryFileService     in-memory ファイルシステム (どの OS でも Windows のパス規則)
   ├── Navigator  FakeWindowNavigator     子ウィンドウの要求を記録し、モーダルの結果を返す / 非モーダルを閉じる
   ├── Logs       RecordingLoggerFactory  ログを記録
   └── Provider   DefaultServiceProvider  上のフェイクを登録済み — PresenterTestHost.Start(fakes.Provider, presenter, view) に渡す

TestServices.Create()           無出力の ILoggerFactory だけを登録した DefaultServiceProvider (自作フェイクだけで組むとき)
PresenterTestHost               モック View をアタッチして実際の初期化を走らせ、RequestClose をゲートに通して受理されたクローズを記録し、CanClose / CanCloseAsync ゲートを駆動する
CapturingActionBinder           フレームワークが View に渡す dispatcher を捕捉。テストから Dispatch する入口
FakeValidationErrorDisplay      IValidatableView のモック View が ValidationErrors として返す。Presenter の Validation が送ったエラーを記録
Mock<IXxxView>                  View インターフェイス用のモック (テスト側で書く — CapturingActionBinder を公開する)
業務依存 (IXxxRepository 等)     Presenter のコンストラクタに注入 (テスト側で書く。フレームワークサービスとは別経路)
```

**業務依存 (Repository など) と View のモックはテスト側で書きます** — パッケージが提供するのはフレームワークサービスのフェイクだけです。

**フェイクは任意です。** 全部使う、1 つだけ自前に差し替える、まったく使わない、のどれでも構いません:

```csharp
var fakes = TestServices.CreateFakes();                                    // 全部
fakes.Provider.RegisterInstance<IFileService>(new MyFileService());       // 1 つ差し替え (Start の前に)
var sp = TestServices.Create(); sp.RegisterInstance<IMessageService>(mock.Object);   // 使わない (Moq など)
class SlowFiles : InMemoryFileService { public override string ReadAllText(string path) { ... } }   // 1 操作だけ上書き
```

**答えを用意していない問い合わせは例外になります。** 確認ダイアログ・ファイルダイアログ・モーダルウィンドウをテストが想定していないのに Presenter が出すと、`UnscriptedInteractionException` (何を聞かれたかを含むメッセージ) が投げられます — 想定外の質問が黙って片方の分岐に進むことはありません。気にしないテストでは、`fakes.Messages.AnswerUnscripted(yes: true)` / `fakes.Dialogs.CancelUnscripted()` / `fakes.Navigator.CancelUnscripted()` で既定の答えを決められます。

---

## 基本的なテストの書き方

```csharp
public class UserEditorPresenterTests
{
    private readonly FakeServices        _fakes = TestServices.CreateFakes();
    private readonly MockUserRepository  _repository = new MockUserRepository();   // 業務依存はコンストラクタ注入
    private readonly MockUserEditorView  _view = new MockUserEditorView();
    private readonly UserEditorPresenter _presenter;

    public UserEditorPresenterTests()
    {
        // Arrange (共通) — xUnit はテストごとにクラスを new するので、各テストは独立
        _presenter = new UserEditorPresenter(_repository);

        // フレームワークサービスは Start の第 1 引数で渡す (この Presenter だけに効く。グローバルな
        // ServiceLocator.Current は触らないので、テストを並列に走らせてよい)。
        // View のアタッチ + 実際の初期化 (RegisterViewActions → binder 自動バインド → OnInitialize)。
        // Presenter の AttachView / Initialize は明示的インターフェイス実装なので直接は呼べない。
        PresenterTestHost.Start(_fakes.Provider, _presenter, _view);

        // 初期化時の呼び出し記録をクリアして純粋なテスト実行に集中
        _fakes.Messages.ClearCalls();
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

        // Assert — 記録された呼び出し (Kind / Text / Caption) を検証
        Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Info && m.Text.Contains("saved"));
    }
}
```

**AAA パターン** (Arrange / Act / Assert) で書くと、テストの意図が明確になります。

> **アクションは `_view.Binder.Dispatch(...)` で駆動します。** Presenter の `Dispatcher` は protected で、テスト用に公開されることはありません。代わりにモック View が `CapturingActionBinder` を公開します — フレームワークは初期化中 (`PresenterTestHost.Start` の中) に `View.ActionBinder.Bind(dispatcher)` を自動で呼ぶため、binder はそこで dispatcher を捕捉できます。本物の Form のボタンが使うのと同一の経路なので、`CanExecute` もミドルウェアも production と同じように評価されます。

```csharp
// Kibinago.Testing.CapturingActionBinder (パッケージ同梱 — 要点のみ抜粋)
public class CapturingActionBinder : IViewActionBinder
{
    public ViewActionDispatcher Dispatcher { get; private set; }

    public void Bind(ViewActionDispatcher dispatcher) => Dispatcher = dispatcher;   // フレームワークが呼ぶ
    public void Unbind() => Dispatcher = null;      // Presenter の破棄時にフレームワークが呼ぶ。以後の Dispatch は何もしない
    public bool IsBound => Dispatcher != null;      // Bind から Unbind (Presenter の破棄) までの間 true

    public void Dispatch(ViewAction action, object payload = null) => Dispatcher.Dispatch(action, payload);
    public Task DispatchAsync(ViewAction action, object payload = null) => Dispatcher.DispatchAsync(action, payload);   // 非同期ハンドラの完了を待つ
    public bool CanDispatch(ViewAction action) => Dispatcher.CanDispatch(action);
}
```

モック View 側は 2 行で公開します:

```csharp
public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
public IViewActionBinder ActionBinder => Binder;
```

> **View が別の Presenter に付け直されたか** (モード切替で子パネルの Presenter を入れ替えるなど) は `IsBound` / `Dispatcher` に加えて `BindCount` / `UnbindCount` で確かめます。旧 Presenter の破棄で 1 回解除、新しい Presenter で 2 回目のバインド — `Assert.Equal(2, view.Binder.BindCount); Assert.Equal(1, view.Binder.UnbindCount);`。

> **業務依存はコンストラクタ、フレームワークサービスは `PresenterTestHost.Start(services, ...)`。** `TestServices.CreateFakes()` が登録するのは Message/Toast/Dialog/File/Navigator/Logger の **フレームワークサービス** だけです。`IUserRepository` のような業務依存は Presenter のコンストラクタに注入します (`new UserEditorPresenter(_repository)`)。実例は [`MainEmailPresenterTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/Kibinago.Samples.Tests/Presenters/MainEmailPresenterTests.cs) を参照。

> **自分でプロバイダを組むときは `TestServices.Create()` から始めます。** どの Presenter もアタッチ時に `ILoggerFactory` を必要とするため、それだけを登録済みで返します。渡したプロバイダにないサービスはエラーになり、グローバルの既定サービスで補われることはありません (テストが何に依存しているかがコードに現れるように)。プロバイダは呼び出し側の所有で、ホストは破棄しません。Presenter がコンストラクタで `Logger` や `Messages` を使っていると、`Start(services, ...)` は `InvalidOperationException` になります — その処理は `OnViewAttached` / `OnInitialize` に移してください。

```csharp
var messages = new FakeMessageService();                 // パッケージのフェイクでも自作でもよい
var services = TestServices.Create();                    // ILoggerFactory (無出力) だけ登録済み
services.RegisterInstance<IMessageService>(messages);
using (var host = PresenterTestHost.Start(services, new OrderPresenter(repo), view)) { ... }
```

---

## シナリオ別のレシピ

### メッセージダイアログの呼び出しを検証する

`fakes.Messages.Calls` は呼び出しの記録 (`Kind` = `Info` / `Warning` / `Error` / `YesNo` / `OkCancel` / `YesNoCancel`、`Text`、`Caption`、`IsConfirmation`) です。

```csharp
[Fact]
public void OnSave_WhenSaveFails_ShowsError()
{
    _view.UserName = "John";
    _repository.SaveShouldThrow = true;   // 失敗をシミュレート (コンストラクタ注入した mock repo)

    _view.Binder.Dispatch(StandardActions.Save);

    Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Error && m.Text.Contains("Failed to save"));
}

[Fact]
public void OnDelete_WithoutSelection_ShowsWarning()
{
    _view.SelectedId = null;   // 未選択

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.Equal(MessageKind.Warning, Assert.Single(_fakes.Messages.Calls).Kind);
}
```

### ConfirmYesNo の分岐をテストする

答えは **キュー → ハンドラ → 未スクリプト時の既定 (例外)** の順で決まります。

```csharp
[Fact]
public void OnDelete_UserConfirms_RemovesItem()
{
    _view.SelectedId = 42;
    _fakes.Messages.EnqueueYesNo(true);            // 次の ConfirmYesNo に Yes と答える

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.True(_repository.WasDeleted(42));
}

[Fact]
public void OnDelete_UserDeclines_DoesNotRemove()
{
    _view.SelectedId = 42;
    _fakes.Messages.YesNoHandler = m => !m.Text.Contains("削除");   // 質問の内容で答える

    _view.Binder.Dispatch(StandardActions.Delete);

    Assert.Contains(_fakes.Messages.Calls, m => m.IsConfirmation);
    Assert.False(_repository.WasDeleted(42));
}
```

`EnqueueOkCancel` / `OkCancelHandler`、`EnqueueYesNoCancel(ConfirmResult.Yes)` / `YesNoCancelHandler` も同じです。答えを用意していない確認は `UnscriptedInteractionException` になります (`AnswerUnscripted(yes: true)` で既定の答えにできます)。

### ファイル選択ダイアログの戻り値を制御する

```csharp
[Fact]
public void OnImport_WhenUserSelectsFile_ImportsContent()
{
    _fakes.Dialogs.EnqueueOpenFile(InteractionResult<string>.Ok(@"C:\data\users.csv"));
    _fakes.Files.SeedFile(@"C:\data\users.csv", "id,name\r\n1,John");   // 親フォルダも作られる

    _view.Binder.Dispatch(ImportActions.Import);

    Assert.Equal(1, _view.Users.Count);
    Assert.Equal("John", _view.Users[0].Name);
}

[Fact]
public void OnImport_WhenUserCancels_DoesNothing()
{
    _fakes.Dialogs.EnqueueOpenFile(InteractionResult<string>.Cancel());

    _view.Binder.Dispatch(ImportActions.Import);

    Assert.Equal(0, _view.Users.Count);   // キャンセル時は取り込みが起きない
}
```

ハンドラ (`OpenFileHandler = o => ...`、`SaveFileHandler`、`FolderBrowserHandler`、`ColorHandler`) は渡されたオプション (`Filter`、`FileName` など) を見て答えられます。`fakes.Dialogs.Calls` には要求ごとの `Kind` と渡されたオプションが残ります。

### ファイル I/O をフェイクにする

`InMemoryFileService` はメモリ上のファイルシステムです。**どの OS で動かしても Windows のパス規則** (大文字小文字を区別しない、`\` と `/`、`.` / `..`、相対パスは `C:\test` 基準) に従い、`System.IO` と同じ例外を投げます (親フォルダがなければ `DirectoryNotFoundException`、上書き不可なら `IOException`、ファイルがなければ `FileNotFoundException`)。

```csharp
[Fact]
public void OnLoadConfig_ReadsFromFileSystem()
{
    _fakes.Files.SeedFile(@"C:\app\config.json", "{ \"theme\": \"dark\" }");

    _view.Binder.Dispatch(ConfigActions.Load);

    Assert.Equal("dark", _view.Theme);
}

[Fact]
public void OnExport_WhenTheFileIsLocked_ShowsError()
{
    _fakes.Dialogs.EnqueueSaveFile(InteractionResult<string>.Ok(@"C:\test\out.csv"));
    _fakes.Files.Failure = op => op.Operation == FileOperation.Write ? new IOException("Excel で開かれています") : null;

    _view.Binder.Dispatch(ExportActions.Export);

    Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Error);
    Assert.Empty(_fakes.Files.FilePaths);
}
```

テキストは指定したエンコーディングでバイト列として保存されます (既定は BOM なし UTF-8、BOM は読み込み時に解釈)。`FilePaths` / `DirectoryPaths` で中身を確認できます。

### 子ウィンドウの表示を検証する

`FakeWindowNavigator` は本番の `WindowNavigator` と同じ規則 (結果の型変換、キーの重複判定、Presenter の破棄、パラメータ必須の Presenter をパラメータなしで開くと例外) で動きます。渡された子 Presenter はアタッチも初期化もされません — 子画面自体の振る舞いは、別のインスタンスを `PresenterTestHost` でテストします。

**モーダル** — Presenter の型ごとに答えを登録します:

```csharp
[Fact]
public void OnEditUser_OpensEditorAndReloadsOnSuccess()
{
    _view.SelectedUserId = 1;
    _fakes.Navigator.RespondTo<EditUserPresenter, User>(r => InteractionResult<User>.Ok(new User("John")));

    _view.Binder.Dispatch(MainActions.EditUser);

    Assert.Equal(1, _fakes.Navigator.LastModalParameters<int>());   // WithParam で渡した値
    Assert.True(_view.ReloadCalled);
}

[Fact]
public void OnEditUser_WhenCancelled_DoesNotReload()
{
    _view.SelectedUserId = 1;
    _fakes.Navigator.RespondTo<EditUserPresenter, User>(_ => InteractionResult<User>.Cancel());

    _view.Binder.Dispatch(MainActions.EditUser);

    Assert.False(_view.ReloadCalled);
}
```

`RespondTo` の関数は `ModalRequest` (`Presenter`、`Parameters`、`Owner`) を受け取ります。呼び出し側が期待する型と違う値を返すと、本番と同じく `Error` になります。

開いた画面のパラメータで答えを変えるときは、パラメータの型を書く `RespondTo<TPresenter, TParam, TResult>` を使います。関数はパラメータそのものを受け取るので、`(PickerParameters)r.Parameters` のようなキャストが要りません。`TParam` は Presenter が受け取る型でなければコンパイルエラーになります (`IInitializable<TParam>`)。

```csharp
// 選択画面を用途ごとに答え分ける: 渡された候補から目的の項目を選ぶ
_fakes.Navigator.RespondTo<PickerPresenter, PickerParameters, int>(
    p => InteractionResult<int>.Ok(p.Items.First(i => i.Text == "毎食後").Id));
```

**非モーダル** — 開いたウィンドウが `fakes.Navigator.Windows` に残るので、画面が閉じたときの結果をテストから渡します:

```csharp
_view.Binder.Dispatch(MainActions.OpenOrder);                 // ShowWindow(..., keySelector: _ => id, onClosed: ...)

var window = Assert.Single(_fakes.Navigator.Windows);
window.CloseWithResult(42);                                   // onClosed に Ok(42) が届き、その後 Presenter が破棄される
// window.Close() はユーザーが X で閉じた場合 (Cancel)、CloseWithError("...") はエラー終了
```

同じキーで再度開くと既存のウィンドウがアクティブになり (`window.Activations`)、新しい Presenter は破棄されます。すべての要求は `fakes.Navigator.Calls` に記録されます。

### 検証エラーを検証する

View が `IValidatableView` を実装している画面では、モック View が `Kibinago.Testing` の `FakeValidationErrorDisplay` を `ValidationErrors` として返します。Presenter の `Validation` が送ったエラーが記録されます。

```csharp
private sealed class MockCustomerEditView : ICustomerEditView
{
    public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
    public IViewActionBinder ActionBinder => Binder;
    public FakeValidationErrorDisplay Errors { get; } = new FakeValidationErrorDisplay();
    public IValidationErrorDisplay ValidationErrors => Errors;   // IValidatableView
    public string FocusedField { get; private set; }
    public void FocusError(string field) => FocusedField = field;
    // ... 入力値のプロパティ
}

[Fact]
public void Save_WithoutName_ShowsTheFirstErrorAndFocusesIt()
{
    _view.CustomerName = "";
    _fakes.Messages.ClearCalls();

    _view.Binder.Dispatch(CustomerEditActions.Save);

    Assert.True(_view.Errors.RevealAll);                                   // 送信後: すべてのエラーを表示
    Assert.True(_view.Errors.HasError(nameof(Customer.Name)));
    Assert.Equal(MessageKind.Warning, Assert.Single(_fakes.Messages.Calls).Kind);
    Assert.Equal(nameof(Customer.Name), _view.FocusedField);
    Assert.False(_host.CloseRequested);
}
```

`ErrorsFor(field)` でメッセージ、`ShowCount` / `ResetCount` で通知の回数も確認できます。

### ロギングを検証する

`fakes.Logs` (`RecordingLoggerFactory`) はすべてのレベルを記録します。

```csharp
[Fact]
public void OnSave_LogsSuccess()
{
    _view.Binder.Dispatch(StandardActions.Save);

    Assert.Contains(_fakes.Logs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

`LogEntry` は `Category`、`Level`、`Exception`、`Message` (プレースホルダー展開済み)、`Template`、`Args` を持ちます。`TestServices.Create()` だけで組む場合のロガーは無出力 (`NullLoggerFactory`) です。

### 非同期処理をテストする

非同期アクションハンドラ、`OnInitializeAsync`、`CanCloseAsync` は、それぞれの完了を `await` してから検証します。`Task.Delay` で待つ必要はありません。xUnit のテストメソッドは `async Task` にします。

```csharp
[Fact]
public async Task Refresh_LoadsCustomers()
{
    _repository.Customers = new[] { new Customer("A"), new Customer("B") };

    // 非同期ハンドラ: Dispatch は完了を待たずに戻るので DispatchAsync を使う
    await _view.Binder.DispatchAsync(CustomerActions.Refresh);

    Assert.Equal(2, _view.Customers.Count);
}

[Fact]
public async Task Start_LoadsDataInOnInitializeAsync()
{
    using (var host = PresenterTestHost.Start(new CustomerListPresenter(_repository), _view))
    {
        await host.Initialized;            // OnInitializeAsync の完了を待つ
        Assert.Equal("完了", _view.StatusMessage);
    }
}

[Fact]
public async Task Close_WithUnsavedChanges_SavesBeforeClosing()
{
    _view.SimulateEdit("new value");
    _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.Yes);

    bool allow = await _host.CanCloseAsync();   // CanCloseAsync の判定 (保存) を待つ

    Assert.True(allow);
}

[Fact]
public async Task CloseAction_WithUnsavedChanges_SavesAndCloses()
{
    _view.SimulateEdit("new value");
    _fakes.Messages.EnqueueYesNoCancel(ConfirmResult.Yes);

    _view.Binder.Dispatch(StandardActions.Close);   // RequestClose → CanCloseAsync
    await _host.WaitForCloseDecisionAsync();        // DispatchAsync はゲートの判定を待たない

    Assert.True(_host.CloseRequested);
}
```

- `RequestClose` は `CanClose` / `CanCloseAsync` を通るので、非同期ゲートの答えはハンドラの完了より後に出ることがあります。`await host.WaitForCloseDecisionAsync()` は、`RequestClose` が始めたクローズの判定を待ちます (判定待ちがなければすぐ完了)。`DispatchAsync` はハンドラの完了しか待ちません。
- `DispatchAsync` はハンドラの例外で fault します (`await Assert.ThrowsAsync<...>(() => view.Binder.DispatchAsync(...))`)。`Initialized` は `OnInitializeAsync` が例外を投げても fault しません (本番と同じくログに出るだけ)。想定内の失敗は、ハンドラ内で表示したメッセージ (`fakes.Messages.Calls`) や View の状態で検証します。
- 実行中のアクションは `CanDispatch == false` です。「実行中は無効」を検証するなら、完了しない `TaskCompletionSource` をモックリポジトリから返し、`_view.Binder.Dispatch(...)` の直後に `Assert.False(_view.Binder.CanDispatch(...))` とします。
- 実例: [`tests/Kibinago.Samples.Tests/Presenters/ComposeEmailPresenterTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/Kibinago.Samples.Tests/Presenters/ComposeEmailPresenterTests.cs)、`MainEmailPresenterTests.cs`、`ViewActions/AsyncActionTests.cs`。

### 子 Presenter を含む画面のテスト

親 Presenter が `AttachChild` で子パネルを組み立てる画面 ([HowTo: マスター/詳細 § 動的なパネル](HowTo-Implement-Master-Detail#動的なパネル-行の追加削除タブ)) は、**親だけ** を `PresenterTestHost` で起動します。親の本物の `AttachChild` が子を初期化するので、組み立て (どの子に何を渡すか) も含めてテストできます。

- フェイクの親 View は、子の View インターフェイスとして **フェイクの子 View** を返します (固定パネルはプロパティ、動的パネルは `AddLine()` が新しいものを作って記録)。子 View にも `CapturingActionBinder` を持たせます。
- 子のアクションは **子 View の `Binder`** で Dispatch します。本物の子 Presenter が動きます。
- 子は親のサービスを引き継ぐので、`Start(services, …)` に渡したフェイクが子でも使われます (グローバルには落ちない)。
- 取り除かれたパネルは不活性です: その `Binder.Dispatch` は何もせず、`Binder.IsBound` は `false`。

```csharp
public sealed class FakeOrderLineView : IOrderLineView
{
    public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
    public IViewActionBinder ActionBinder => Binder;
    public IReadOnlyList<Product> Products { set { } }
    public int Quantity { set { } }
}

public sealed class FakeOrderEditView : IOrderEditView
{
    public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
    public IViewActionBinder ActionBinder => Binder;
    public List<FakeOrderLineView> Lines { get; } = new List<FakeOrderLineView>();
    public event EventHandler AddLineRequested;
    public event EventHandler<IOrderLineView> RemoveLineRequested;

    public IOrderLineView AddLine() { var line = new FakeOrderLineView(); Lines.Add(line); return line; }
    public void RemoveLine(IOrderLineView line) => Lines.Remove((FakeOrderLineView)line);

    public void RaiseAddLine() => AddLineRequested?.Invoke(this, EventArgs.Empty);
    public void RaiseRemoveLine(FakeOrderLineView line) => RemoveLineRequested?.Invoke(this, line);
}

[Fact]
public void RemovedLine_IsGoneFromModelAndInert()
{
    var fakes = TestServices.CreateFakes();
    fakes.Messages.EnqueueYesNo(true);          // 削除確認に「はい」
    var view = new FakeOrderEditView();
    var input = new OrderEditInput();

    using (PresenterTestHost.Start(fakes.Provider, new OrderEditPresenter(_products), view, input))   // _products: フェイク Repository
    {
        view.RaiseAddLine();
        var line = Assert.Single(view.Lines);
        line.Binder.Dispatch(OrderLineActions.ChangeQuantity, 3);   // 本物の子 Presenter が動く
        Assert.Equal(3, input.Lines.Single().Quantity);

        view.RaiseRemoveLine(line);

        Assert.Empty(view.Lines);
        Assert.Empty(input.Lines);
        Assert.False(line.Binder.IsBound);                          // 子 Presenter は破棄済み
        line.Binder.Dispatch(OrderLineActions.ChangeQuantity, 9);   // 遅れたクリック: 何もしない
    }
}
```

`CapturingActionBinder` の `Unbind()` は Presenter の破棄時にフレームワークが呼び、以後の `Dispatch` / `DispatchAsync` は何もせず (`CanDispatch` は `false`)、`IsBound` は `false` になります。Bind 前の `Dispatch` は従来どおり例外 (Presenter の起動忘れ) です。親の `PresenterTestHost.Initialized` が待つのは親の `OnInitializeAsync` だけです。実例: [`tests/Kibinago.Samples.Tests/Composition/AttachChildDynamicTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/Kibinago.Samples.Tests/Composition/AttachChildDynamicTests.cs)、`AttachChildTests.cs`。

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

### PresenterTestHost でホストする

`PresenterTestHost` (`Kibinago.Testing` パッケージ) は、本番の `WindowNavigator` / `Connect` と同じ初期化をモック View に対して行うテスト用ホストです。Form は不要です。

| メンバー | 用途 |
|---|---|
| `PresenterTestHost.Start(presenter, view)` / `Start(presenter, view, parameters)` | View をアタッチし、`RegisterViewActions` → binder 自動バインド → `OnInitialize` を実行 (`OnInitializeAsync` は開始のみ)。初期化が例外を投げた場合は Presenter を破棄して再スロー |
| `CloseRequested` / `CloseCount` / `CloseResult` / `CloseStatus` | **受理された** クローズの記録。`Start` 後の `RequestClose` は本番と同じく `CanClose` / `CanCloseAsync` (`CloseReason.Normal`) を通り、ゲートが許可したときだけ記録される (拒否されれば記録なし・結果は破棄。ゲート内の `RequestClose` は結果を置き換える)。`Start` の同期部分 (`OnInitialize` など) の中の `RequestClose` は表示のキャンセルとしてゲートなしで記録され、何回呼んでも 1 回 (最後の結果) |
| `WaitForCloseDecisionAsync()` | `RequestClose` が始めたクローズの判定 (非同期の `CanCloseAsync`) を待つ `Task`。判定待ちがなければすぐ完了。`DispatchAsync` はゲートを待たないので、こちらを `await` してから `CloseRequested` を検証する |
| `Initialized` | `OnInitializeAsync` の完了で完了する `Task`。`await host.Initialized;` してから非同期ロードの結果を検証する (失敗はログに出るだけで fault しない) |
| `CanClose(reason = CloseReason.Normal)` | X / Alt+F4 と同じゲートを実行し `bool?` を返す (何度でも呼べる判定の確認で、クローズは記録しない)。`CanCloseAsync` が未回答なら `null`。コントロール Presenter ではクローズゲートがないので `InvalidOperationException` |
| `CanCloseAsync(reason = CloseReason.Normal)` | 同じゲートを実行し、`CanCloseAsync` の判定を `await` できる `Task<bool>` を返す。コントロール Presenter では `InvalidOperationException` |
| `Dispose()` | Presenter を破棄 (`Cleanup` と `Disposables` の解放) |

### ウィンドウクローズのテスト

`RequestClose` を呼ぶアクションも、ゲート (`CanClose`) そのものも `PresenterTestHost` で検証します。`RequestClose` は本番と同じくゲートを通るので、ゲートが確認ダイアログを出すならフェイクに答えを用意します。`ICloseParticipant` (internal) にキャストしたり、自前の `ICloseSink` を束縛したりする必要はありません:

```csharp
public class UserEditorClosingTests
{
    [Fact]
    public void Save_ClosesWithOk()
    {
        var view = new MockUserEditorView { UserName = "John", Email = "john@example.com" };
        using (var host = PresenterTestHost.Start(new UserEditorPresenter(new MockUserRepository()), view))
        {
            view.Binder.Dispatch(StandardActions.Save);

            Assert.True(host.CloseRequested);
            Assert.Equal(1, host.CloseCount);
            Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
        }
    }

    [Fact]
    public void CanClose_WhenDirty_BlocksNormalCloseButNotShutdown()
    {
        var view = new MockUserEditorView();
        using (var host = PresenterTestHost.Start(new UserEditorPresenter(new MockUserRepository()), view))
        {
            view.UserName = "Edited";
            view.RaiseInputChanged();                      // ChangeTracker が dirty になる

            Assert.False(host.CanClose());                 // CloseReason.Normal — dirty なのでブロック
            Assert.True(host.CanClose(CloseReason.SystemShutdown));   // シャットダウンは決してブロックしない
            Assert.False(host.CloseRequested);             // host.CanClose はゲートの確認だけで、クローズを記録しない
        }
    }

    // 「閉じる」ボタン (StandardActions.Close → RequestClose(InteractionStatus.Cancel)) は X と同じゲートを通る
    [Fact]
    public void CloseAction_WhenDirty_AsksAndNoKeepsTheWindowOpen()
    {
        var fakes = TestServices.CreateFakes();
        var view = new MockUserEditorView();
        using (var host = PresenterTestHost.Start(fakes.Provider, new UserEditorPresenter(new MockUserRepository()), view))
        {
            view.UserName = "Edited";
            view.RaiseInputChanged();
            fakes.Messages.EnqueueYesNo(false);            // 「変更を破棄しますか？」→ いいえ

            view.Binder.Dispatch(StandardActions.Close);

            Assert.Contains(fakes.Messages.Calls, m => m.IsConfirmation);
            Assert.False(host.CloseRequested);             // ゲートが拒否: 記録されない

            fakes.Messages.EnqueueYesNo(true);             // もう一度 → はい
            view.Binder.Dispatch(StandardActions.Close);

            Assert.True(host.CloseRequested);
            Assert.Equal(InteractionStatus.Cancel, host.CloseStatus);
        }
    }
}
```

`CanCloseAsync` のタスクが未完了の間、`host.CanClose()` は `null` を返します。判定を待つには `await host.CanCloseAsync()` を使います。アクションの `RequestClose` が始めた非同期の判定は `await host.WaitForCloseDecisionAsync()` で待ちます ([非同期処理をテストする](#非同期処理をテストする))。

`private` だったメソッドを `internal` に変えてテストすると、CanExecute スキップ等の不具合が production で発覚するまで隠れます。

完全な実例: [`tests/Kibinago.Samples.Tests/Presenters/PresenterTestHostTests.cs`](https://github.com/VoidAxon/winforms-mvp/blob/master/tests/Kibinago.Samples.Tests/Presenters/PresenterTestHostTests.cs)、`WindowClosingDemoPresenterTests.cs` および `Composition/RequestCloseGateHostTests.cs` (ゲートを通る `RequestClose`、表示のキャンセル、`WaitForCloseDecisionAsync`)。(`CanCloseTests.cs` はフレームワーク内部の契約テストで、`ICloseParticipant` を直接使います。)

---

## ベストプラクティス

### 1. 各テストで独立にフェイクを作る

```csharp
// ✅ フィールド初期化子で毎回新規生成 (xUnit はテストごとにクラスを new する)
public class MyTests
{
    private readonly FakeServices _fakes = TestServices.CreateFakes();
}

// ❌ static フィールドで共有するとテスト間で状態が汚染される
private static FakeServices _fakes = TestServices.CreateFakes();
```

### 2. AAA パターンで書く

```csharp
[Fact]
public void TestName()
{
    // Arrange — テストデータ・モック設定
    _view.TaskText = "Test";
    _fakes.Messages.EnqueueYesNo(true);

    // Act — テスト対象のアクション
    _view.Binder.Dispatch(MyActions.DoSomething);

    // Assert — 期待値の検証
    Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Info);
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
    Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Info);
    Assert.Equal(1, _view.SavedCount);
}

// ❌ 関連の薄い検証を 1 テストに詰め込まない
[Fact]
public void TestEverything()
{
    // Save、Delete、Refresh、Load、Export を全部検証... → 失敗時に原因特定が困難
}
```

### 5. 初期化時の記録をクリアする

```csharp
public MyTests()
{
    _presenter = new MyPresenter();
    PresenterTestHost.Start(_fakes.Provider, _presenter, _view);

    // 初期化で発生した呼び出し記録をクリア (スクリプトした答えは残る)
    _fakes.Messages.ClearCalls();
    _fakes.Toasts.ClearCalls();
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

- [Platform Services](Reference-Platform-Services) — フレームワークサービスの API 一覧
- [ViewAction システム](Reference-ViewAction-System) — `Dispatcher.Dispatch` の動作
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — クローズ動作のテスト
- [Reference-Logging#テストでの利用](Reference-Logging#テストでの利用) — ログの検証
- サンプル:
  - [`tests/Kibinago.Samples.Tests/Presenters/`](https://github.com/VoidAxon/winforms-mvp/tree/master/tests/Kibinago.Samples.Tests/Presenters) — Presenter 単体テストの実例
  - [`tests/Kibinago.Samples.Tests/Mocks/`](https://github.com/VoidAxon/winforms-mvp/tree/master/tests/Kibinago.Samples.Tests/Mocks) — View / Repository のモック実装 (サービスのフェイクは `Kibinago.Testing`)
  - `src/Kibinago.Testing/` — `PresenterTestHost` とフェイクの実装
