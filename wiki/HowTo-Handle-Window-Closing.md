# HowTo: ウィンドウクローズを扱う

このページでは、典型的なウィンドウクローズシナリオの実装パターンを示します。
Push/Pull 二方向モデルの設計意図については [ウィンドウクローズモデル](Concept-Window-Closing-Model) を、`WindowNavigator` の API リファレンスは [WindowNavigator](Reference-WindowNavigator) を参照してください。

> **基本ルール**: Form はクローズコードを **まったく** 書きません。`IWindowView` にクローズ関連のメンバーはありません。すべてのクローズポリシーは Presenter に置きます。

---

## 目次

- [シナリオ 1: シンプルな OK/Cancel ダイアログ](#シナリオ-1-シンプルな-okcancel-ダイアログ)
- [シナリオ 2: クローズ時のダーティチェック](#シナリオ-2-クローズ時のダーティチェック)
- [シナリオ 3: 業務結果を返すダイアログ](#シナリオ-3-業務結果を返すダイアログ)
- [シナリオ 4: 親が結果を受け取る](#シナリオ-4-親が結果を受け取る)
- [シナリオ 5: システムシャットダウンを絶対にブロックしない](#シナリオ-5-システムシャットダウンを絶対にブロックしない)
- [シナリオ 6: 非同期クローズ判断](#シナリオ-6-非同期クローズ判断)
- [シナリオ 7: Adopted ホスティング (シェル / レガシー Form)](#シナリオ-7-adopted-ホスティング-シェル--レガシー-form)
- [テストパターン](#テストパターン)

---

## シナリオ 1: シンプルな OK/Cancel ダイアログ

ダーティ状態のない最小限のダイアログです。Save/Cancel ハンドラから結果を Push するだけです。X ボタンでいつでも閉じてよいウィンドウなら `CanClose` のオーバーライドは不要です。

```csharp
public class ConfirmDeletePresenter : WindowPresenterBase<IConfirmDeleteView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Ok,     OnOk);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    private void OnOk()     => RequestClose(true,  InteractionStatus.Ok);
    private void OnCancel() => RequestClose(false, InteractionStatus.Cancel);
}

// Caller
var result = Navigator.For(new ConfirmDeletePresenter()).ShowAsModal<bool>();
if (result.IsOk && result.Value)
    DeleteItem();
```

ユーザーが X を押した場合、ウィンドウは `result.IsCancelled` でクローズされ、特別な処理は不要です。

---

## シナリオ 2: クローズ時のダーティチェック

`CanClose` をオーバーライドして、未保存の変更がある場合にプロンプトを表示します。これがダーティチェックダイアログを持つ **唯一の場所** です。

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnViewAttached()
    {
        // The tracker raises no change notification: read the dirty state where the
        // view reports an edit, and re-evaluate CanExecute in the same place.
        View.EditChanged += (s, e) =>
        {
            View.IsDirty = HasUnsavedChanges;
            Dispatcher.RaiseCanExecuteChanged();
        };
    }

    protected override void OnInitialize(EditUserParameters parameters)
    {
        var user = LoadUser(parameters.UserId);
        View.Populate(user);
        // Style 2 ("a new model from the view each time"): the tracker only detects
        // changes. This first model is used just to take the first snapshot.
        _changeTracker = TrackChanges(View.GetModel());
        View.IsDirty = false;
    }

    private bool HasUnsavedChanges =>
        _changeTracker != null && _changeTracker.IsChangedWith(View.GetModel());

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => HasUnsavedChanges);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    // ── Pull direction: X / Alt+F4 ───────────────────────────────────────────
    protected override bool CanClose(CloseReason reason)
    {
        if (!HasUnsavedChanges) return true;

        return Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    }

    // ── Push direction: Save / Cancel buttons ────────────────────────────────
    private void OnSave()
    {
        // Read the final view state before persisting; EditChanged may not have
        // fired if the control with focus has not yet committed its value.
        var user = View.GetModel();
        SaveUser(user);
        _changeTracker.AcceptChanges(user);   // snapshot = saved value; CanClose sees clean state
        RequestClose(BuildResult(), InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        // Discard: stop tracking the discarded edits BEFORE RequestClose (this does not
        // mean they were saved), so CanClose would not ask again.
        _changeTracker.AcceptChanges(View.GetModel());
        RequestClose(InteractionStatus.Cancel);
    }

    private UserResult BuildResult() => new UserResult { /* ... */ };
}
```

### 二重プロンプトが起きない理由

Push ハンドラ (`OnSave` / `OnCancel`) は `RequestClose` を呼ぶ **前に** ダーティ状態を確定させます (保存なら保存した値で、破棄なら破棄する画面の値で `AcceptChanges`)。これで `CanClose` がその後いつ評価されてもクリーンな状態を見るため、再確認は出ません。ダーティ確認は `CanClose` (Pull) にだけ置くという約束の裏返しです。

なお現在の `WindowLifecycleController` は、`RequestClose` から自分で起こした `form.Close()` については `FormClosing` のゲートを 1 回省略します。これは実装の詳細であり、Push ハンドラでダーティ状態を確定させる理由を置き換えるものではありません (テストの `host.CanClose()` などはゲートを直接評価します)。

```
OnSave()
    ├─ AcceptChanges(user)     ← finalize the dirty state first
    └─ RequestClose(...)
          ↓
    WindowLifecycleController.Close():  _suppressGate = true, form.Close()
          ↓
    FormClosing — _suppressGate is true → CanClose NOT called
          ↓
    FormClosed → converge → InteractionResult(Ok, result)
```

---

## シナリオ 3: 業務結果を返すダイアログ

任意の型を結果として返せます。`RequestClose(result, status)` を呼びます — `TResult` は引数から推論されます:

```csharp
public class CustomerResult
{
    public int Id   { get; set; }
    public string Name { get; set; }
}

public class EditCustomerPresenter : WindowPresenterBase<IEditCustomerView>
{
    private int _customerId;

    private void OnSave()
    {
        var result = new CustomerResult { Id = _customerId, Name = View.CustomerName };
        RequestClose(result, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RequestClose(InteractionStatus.Cancel);
}
```

---

## シナリオ 4: 親が結果を受け取る

呼び出し元は `Navigator.For(...).ShowAsModal<TResult>()` を使って `InteractionResult<TResult>` を読み取ります。Push と Pull のどちらがクローズを引き起こしたかを知る必要はありません:

```csharp
public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private void OnEditCustomer()
    {
        var presenter = new EditCustomerPresenter();
        var param     = new EditCustomerParameters { CustomerId = View.SelectedCustomerId };

        var result = Navigator.For(presenter)
                              .WithParam(param)
                              .ShowAsModal<CustomerResult>();

        if (result.IsOk)
            ReloadCustomer(result.Value.Id);
        else if (result.IsError)
            Messages.ShowError(result.ErrorMessage, "Error");
        // result.IsCancelled: user cancelled or pressed X → no action needed
    }
}
```

> `result.IsError` になるのは、子が `RequestClose("メッセージ", InteractionStatus.Error)` / `RequestClose(exception, InteractionStatus.Error)` で閉じた場合 (メッセージ/例外が `ErrorMessage` / `Exception` に入る) のほか、`Ok` で閉じたのに結果の型が `TResult` と合わない場合や、`TResult` が null 非許容の値型なのに結果がない場合です (以前は `Ok(default)` になっていました)。

---

## シナリオ 5: システムシャットダウンはフレームワークが許可する

モーダルダイアログが開いていると Windows がシャットダウンできません。そのため `SystemShutdown`（Windows のシャットダウン・ログオフ）と `TaskManager`（タスク マネージャーからの終了）では、**フレームワークが Presenter に問い合わせずにクローズを許可します**。`CanClose` / `CanCloseAsync` は呼ばれないので、Presenter に理由の判定を書く必要はありません:

```csharp
protected override bool CanClose(CloseReason reason)
{
    // System shutdown / Task Manager never get here.
    if (!_changeTracker.IsChanged) return true;
    return Messages.ConfirmYesNo("Discard changes?", "Confirm");
}
```

それ以外の理由はすべて Presenter に届きます。`ParentClosing`（オーナー / MDI 親が閉じるので子も閉じる）でも未保存の確認が出るのが正しい動作です。以前のように `if (reason != CloseReason.Normal) return true;` と書くと、オーナーを閉じたときに子ウィンドウの編集内容が確認なしに失われます。

- オーナー付きの子ウィンドウ（`Navigator.ShowWindow(presenter, owner: View)`）でキャンセルが選ばれると、オーナーのクローズも取り消されます（フレームワークはオーナー側のゲートで子の拒否を上書きしません）。
- シャットダウン中は、WinForms がオーナー付きの子ウィンドウに `FormOwnerClosing` を渡しますが、フレームワークはこれもシャットダウンとして扱い、確認を出しません。
- 既知の制限: タスク マネージャーがオーナーを閉じた場合、子ウィンドウには `ParentClosing` として届き、確認が出ることがあります（WinForms にはこの閉じ方を見分ける公開 API がないため）。確認が出るだけで、編集内容は失われません。
- シャットダウン時の自動保存のような処理は `CanClose` に置かないでください（呼ばれません）。編集中に保存する設計にします。

---

## シナリオ 6: 非同期クローズ判断

クローズ判断に `await` が必要な場合 (閉じる前に保存する、サーバーに確認する) は `CanCloseAsync` をオーバーライドします。`true` で許可、`false` でブロックです。以前のコールバック形式 `CanClose(reason, Action<bool> proceed)` は削除されました (BREAKING) — `proceed(x)` は `return x;` に置き換えます。

典型例は「未保存の変更があれば下書き保存してから閉じる」です (`samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs` より):

```csharp
protected override async Task<bool> CanCloseAsync(CloseReason reason)
{
    if (!HasUnsavedChanges) return true;   // _changeTracker.IsChangedWith(ReadEditState())

    var result = Messages.ConfirmYesNoCancel(
        "You have unsaved changes. Do you want to save as draft?", "Unsaved Changes");

    if (result == ConfirmResult.Cancel) return false;   // keep editing
    if (result == ConfirmResult.Yes)
        return await SaveDraftAsync();                 // the window stays open while saving
    return true;                                       // No: discard and close
}

/// Shared by the SaveDraft action and the close prompt. False (after telling the user) blocks the close.
private async Task<bool> SaveDraftAsync()
{
    View.IsSaving = true;
    View.EnableInput = false;
    try
    {
        // Save a copy of the fields; edits made meanwhile are compared against it
        // afterwards and stay unsaved.
        var submitted = ReadEditState();
        await _repository.SaveDraftAsync(CreateEmailMessage(submitted));

        _changeTracker.AcceptChanges(submitted);   // snapshot = what was saved
        View.IsDirty = HasUnsavedChanges;          // still true if the user edited meanwhile
        Dispatcher.RaiseCanExecuteChanged();
        return !View.IsDirty;                      // unsaved edits remain: keep the window open
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Error saving draft: {ex.Message}", "Error");
        return false;                                  // failed save: don't lose the text
    }
    finally
    {
        View.IsSaving = false;
        View.EnableInput = true;
    }
}
```

フレームワークの動作:

- タスクが未完了の間、ウィンドウは開いたままです。`true` で完了するとゲートをバイパスした再クローズが行われ、`false` ならウィンドウは開いたままです。
- 判定待ちの間の再クローズ (X の連打など) は再質問せずに保留されますが、`SystemShutdown` / `TaskManager` によるクローズは通ります。owner / MDI 親 / `Application.Exit` から来たクローズが非同期に許可された場合は、そのクローズが再発行されます。
- タスクが fault / キャンセルされるとクローズはブロックされ、エラーはログ出力されます (システムレベルのクローズは許可)。想定内の失敗は上の例のように中で捕まえて `Messages` で伝え、`false` を返します。
- `SystemShutdown` / `TaskManager` では `await` せずに即座に `true` を返してください ([シナリオ 5](#シナリオ-5-システムシャットダウンを絶対にブロックしない))。

### クローズ時に保存した結果を呼び出し元へ返す

X → 「保存しますか？」→ はい → 保存、のあとで呼び出し元に「保存された」と伝えたい場合 (一覧を再表示する、入力ダイアログが編集結果を返す) は、ゲートの中で `RequestClose(result, status)` を呼んでから `true` を返します。ゲートが判定している間の `RequestClose` は 2 回目のクローズではなく、**判定中のクローズの結果**として扱われます:

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (!_tracker.IsChangedWith(ReadModel())) return true;

    var answer = Messages.ConfirmYesNoCancel("保存しますか？");
    if (answer == ConfirmResult.Cancel) return false;
    if (answer == ConfirmResult.Yes)
    {
        if (!TrySave(out var saved)) return false;   // 入力エラー / 保存失敗: 閉じない
        _tracker.AcceptChanges(saved);
        RequestClose(saved, InteractionStatus.Ok);   // このクローズの結果
    }
    return true;
}
```

- ゲートが `true` を返したときだけ、その結果でクローズします (呼び出し元は `InteractionStatus.Ok` と結果を受け取ります)。`false` を返すと結果は捨てられ、ウィンドウは開いたままです。次のクローズはまた既定 (`Cancel`) から始まります。
- `CanCloseAsync` でも同じです。`await` の後で `RequestClose` を呼び、`true` を返します。判定待ちの間に呼ばれた `RequestClose` (保存ボタンからのものも含む) は、ゲートの答えが出るまで保留されます。
- `RequestClose` を呼ばずに `true` を返した場合は、これまでどおり `Cancel` (結果なし) で閉じます。上の `ComposeEmailPresenter` は結果が「送信したか」を意味するため、下書き保存では `RequestClose` を呼びません。
- `PresenterTestHost` も同じ規則です: ゲート内の `RequestClose` は、`host.CanClose()` / `await host.CanCloseAsync()` が許可したときだけ `CloseRequested` / `CloseResult` に記録されます。

---

## シナリオ 7: Adopted ホスティング (シェル / レガシー Form)

自分で作成して表示する Form (アプリケーションシェル、`Application.Run`、レガシー移行) に対しては `presenter.Connect(form)` を呼びます。Presenter はアタッチ・初期化・クローズ配線を受け取り、`Show` の呼び出しは呼び出し元が行います:

```csharp
// No-result adoption (shell window)
public static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ServiceLocator.Configure(reg => { /* register view registry and other overrides here */ });

        var form      = new MainShellForm();
        var presenter = new MainShellPresenter();
        presenter.Connect(form);      // attach + initialize + wire close controller

        Application.Run(form);        // you own Show
    }
}

// Typed result callback
var form      = new SettingsForm();
var presenter = new SettingsPresenter();
presenter.Connect<ISettingsView, SettingsResult>(form, result =>
{
    if (result.IsOk) ApplySettings(result.Value);
});
form.ShowDialog();

// Parameterized adoption
var form      = new EditItemForm();
var presenter = new EditItemPresenter();
presenter.Connect(form, new EditItemParameters { ItemId = 42 });
form.Show();
```

**単一オーナーの規則**: この方法で接続した Form を `WindowNavigator` から表示することは厳禁です。Form ごとにホスティングモードを 1 つだけ選んでください。

---

## テストパターン

テストでは `PresenterTestHost` (`WinformsMVP.Testing`、`WinformsMVP.Core` に同梱) で Presenter をモック View にホストします。`Start` は本番と同じ初期化を実行し、`RequestClose` を記録し (Push)、`CanClose(reason)` / `CanCloseAsync(reason)` で Pull ゲートを駆動します。Form も `InternalsVisibleTo` も不要です:

```csharp
private readonly MockServices _platform = new MockServices();
private readonly MockView _view = new MockView();
private readonly PresenterTestHost _host;

public WindowClosingDemoPresenterTests()
{
    _host = PresenterTestHost.Start(_platform.Provider, new WindowClosingDemoPresenter(), _view);   // アタッチ + 初期化 + RequestClose の記録
}
```

### Pull 方向 — `CanClose` をテストする

`host.CanClose(reason)` は X / Alt+F4 と同じゲートを通り、`bool?` を返します (`CanCloseAsync` が未回答なら `null`)。`CanCloseAsync` をオーバーライドした Presenter では `await host.CanCloseAsync(reason)` で判定の完了を待ちます (例は後述):

```csharp
[Fact]
public void CanClose_WithUnsavedChanges_UserDeclines_BlocksClose()
{
    _view.SimulateEdit("new value");
    _platform.MessageService.ConfirmYesNoResult = false;   // user keeps editing

    Assert.False(_host.CanClose());                         // CloseReason.Normal
    Assert.True(_platform.MessageService.ConfirmDialogShown);
}

[Fact]
public void CanClose_SystemShutdown_DoesNotPrompt()
{
    _view.SimulateEdit("new value");

    Assert.True(_host.CanClose(CloseReason.SystemShutdown));
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

非同期の判定 (`CanCloseAsync`) は `await` してから検証します (`tests/.../Presenters/ComposeEmailPresenterTests.cs` より):

```csharp
[Fact]
public async Task Closing_WithDirtyState_UserSelectsYes_SavesDraftAndAllowsClose()
{
    _view.SimulateEmailDataChange();
    _platform.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Yes;

    bool allow = await _host.CanCloseAsync();   // awaits the draft save

    Assert.True(allow);
    Assert.Contains("SaveDraftAsync(Test)", _repository.MethodCalls);
}
```

### Push 方向 — `RequestClose` をテストする

アクションを Dispatch し、ホストが記録した結果とステータスを検証します (非同期ハンドラなら `await _view.Binder.DispatchAsync(...)` で完了を待つ):

```csharp
[Fact]
public void Save_PushesResultWithOkStatus()
{
    _view.SimulateEdit("hello");
    _view.Binder.Dispatch(WindowClosingDemoActions.Save);

    Assert.Equal(1, _host.CloseCount);
    Assert.Equal("hello", _host.CloseResult);
    Assert.Equal(InteractionStatus.Ok, _host.CloseStatus);
}

[Fact]
public void Cancel_PushesCancelStatus()
{
    _view.Binder.Dispatch(WindowClosingDemoActions.Cancel);

    Assert.Equal(InteractionStatus.Cancel, _host.CloseStatus);
}
```

### 単一正本の不変条件テスト

Push (Save) の後に続く `CanClose` 呼び出しが再プロンプトを出さないことを検証します:

```csharp
[Fact]
public void Save_ThenCanClose_DoesNotPrompt()
{
    _view.SimulateEdit("hello");
    _view.Binder.Dispatch(WindowClosingDemoActions.Save);

    // Simulate the framework re-running the Pull gate after the Push close.
    Assert.True(_host.CanClose());
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

完全なテスト例: `tests/WinformsMVP.Samples.Tests/Presenters/WindowClosingDemoPresenterTests.cs` および `PresenterTestHostTests.cs`。
テスト駆動の完全なガイド: [HowTo: Presenter をテストする](HowTo-Test-A-Presenter)。

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — 設計の意図と内部構造
- [WindowNavigator](Reference-WindowNavigator) — Navigator API、Fluent 形式、`InteractionResult<T>`
- [ChangeTracker](Reference-ChangeTracker) — ダーティ状態の追跡
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `CanClose` / `CanCloseAsync`、`RequestClose`、`Connect` の位置づけ
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — 完全なテストパターン
