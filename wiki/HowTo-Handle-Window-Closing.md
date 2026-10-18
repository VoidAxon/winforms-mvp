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
        // Push the view's current snapshot into the tracker so IsChangedWith
        // always compares against the freshest user input, then re-evaluate
        // CanExecute so the Save button reflects the updated dirty state.
        View.EditChanged += (s, e) =>
        {
            _changeTracker.UpdateCurrentValue(View.GetModel());
            Dispatcher.RaiseCanExecuteChanged();
        };
    }

    protected override void OnInitialize(EditUserParameters parameters)
    {
        var user = LoadUser(parameters.UserId);
        _changeTracker = new ChangeTracker<UserModel>(user);
        // Populate sets field values without establishing a live data-binding,
        // so the tracker owns the snapshot and WinForms does not mutate it in place.
        View.Populate(_changeTracker.CurrentValue);

        // IsChangedChanged fires after each UpdateCurrentValue call above; keep
        // the subscription so CanExecute is also refreshed on snapshot acceptance.
        _changeTracker.IsChangedChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => _changeTracker.IsChanged);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    // ── Pull direction: X / Alt+F4 ───────────────────────────────────────────
    protected override bool CanClose(CloseReason reason)
    {
        // Never block system-level shutdowns.
        if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
            return true;

        if (!_changeTracker.IsChanged) return true;

        return Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    }

    // ── Push direction: Save / Cancel buttons ────────────────────────────────
    private void OnSave()
    {
        // Sync the final view state before persisting; EditChanged may not have
        // fired if the control with focus has not yet committed its value.
        _changeTracker.UpdateCurrentValue(View.GetModel());
        SaveUser(_changeTracker.CurrentValue);
        _changeTracker.AcceptChanges();   // commit model state; CanClose will see clean state
        RequestClose(BuildResult(), InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();
        RequestClose(InteractionStatus.Cancel);
    }

    private UserResult BuildResult() => new UserResult { /* ... */ };
}
```

### 二重プロンプトが起きない理由

`WindowLifecycleController` は `RequestClose` が呼ばれると内部の抑制フラグをセットするため、続いて実行される `form.Close()` が発火する `FormClosing` イベントでは `CanClose` が完全にスキップされます。これは構造的な保証であり、`AcceptChanges` を `RequestClose` より前に呼ぶかどうかには依存しません。

```
OnSave()
    ├─ AcceptChanges()         ← model state only
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

## シナリオ 5: システムシャットダウンを絶対にブロックしない

モーダルダイアログが開いていると Windows がシャットダウンできません。`SystemShutdown` と `TaskManager` は必ず即座に許可してください:

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
        return true;   // must not block

    if (reason == CloseReason.ParentClosing)
        return true;   // usually allow owner-propagated close

    // Only Normal (X / Alt+F4) gets the dirty-check prompt.
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo("Discard changes?", "Confirm"))
        return false;

    return true;
}
```

---

## シナリオ 6: 非同期クローズ判断

クローズ判断にサーバー呼び出しやコンティニュエーションが必要な場合は、`CanClose` の 2 引数形式をオーバーライドします。クローズ API を `Task` に依存させないため、`Task` ではなく `Action<bool>` を使います:

```csharp
protected override void CanClose(CloseReason reason, Action<bool> proceed)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
    {
        proceed(true);
        return;
    }

    if (!_changeTracker.IsChanged)
    {
        proceed(true);
        return;
    }

    // Ask the server whether there are pending locks before closing.
    _repository.HasActiveLockAsync(_userId, hasLock =>
    {
        if (hasLock)
        {
            Messages.ShowWarning("Another user has a lock. Cannot close now.", "Warning");
            proceed(false);
        }
        else
        {
            proceed(Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"));
        }
    });
    // proceed will be called from the continuation — return without calling it now.
}
```

フレームワークは `proceed` が同期的に呼ばれなかったことを検出してクローズを抑制し、コンティニュエーションから `proceed(true)` が届いたときにゲートをバイパスした再クローズをトリガーします。`proceed` はワーカースレッドから呼んでも UI スレッドへマーシャリングされます。判定待ちの間の再クローズ (X の連打など) は再質問せずに保留されますが、`SystemShutdown` / `TaskManager` によるクローズは通ります。owner / MDI 親 / `Application.Exit` から来たクローズが非同期に許可された場合は、そのクローズが再発行されます。

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

テストでは `PresenterTestHost` (`WinformsMVP.Testing`、`WinformsMVP.Core` に同梱) で Presenter をモック View にホストします。`Start` は本番と同じ初期化を実行し、`RequestClose` を記録し (Push)、`CanClose(reason)` で Pull ゲートを駆動します。Form も `InternalsVisibleTo` も不要です:

```csharp
private readonly MockServices _platform = new MockServices();
private readonly MockView _view = new MockView();
private readonly PresenterTestHost _host;

public WindowClosingDemoPresenterTests()
{
    var presenter = new WindowClosingDemoPresenter().WithServiceProvider(_platform.Provider);
    _host = PresenterTestHost.Start(presenter, _view);   // アタッチ + 初期化 + RequestClose の記録
}
```

### Pull 方向 — `CanClose` をテストする

`host.CanClose(reason)` は X / Alt+F4 と同じゲートを通り、`bool?` を返します (非同期 `CanClose` が未回答なら `null`):

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

### Push 方向 — `RequestClose` をテストする

アクションを Dispatch し、ホストが記録した結果とステータスを検証します:

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
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `CanClose`、`RequestClose`、`Connect` の位置づけ
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — 完全なテストパターン
