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
        => View.EditChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();

    protected override void OnInitialize(EditUserParameters parameters)
    {
        var user = LoadUser(parameters.UserId);
        _changeTracker = new ChangeTracker<UserModel>(user);
        View.Bind(_changeTracker.CurrentValue);

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

クローズ判断にサーバー呼び出しやコンティニュエーションが必要な場合は、`CanClose` の 2 引数形式をオーバーライドします。net40 でコンパイルできるよう、`Task` ではなく `Action<bool>` を使います:

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

フレームワークは `proceed` が同期的に呼ばれなかったことを検出してクローズを抑制し、コンティニュエーションから `proceed(true)` が届いたときにゲートをバイパスした再クローズをトリガーします。

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

### Pull 方向 — `CanClose` をテストする

`ICloseParticipant.CanCloseGate` 経由で Pull ゲートに到達します (内部 API。テストプロジェクトは `InternalsVisibleTo` でアクセスできます):

```csharp
[Fact]
public void CanClose_WithUnsavedChanges_UserDeclines_BlocksClose()
{
    _view.SimulateEdit("new value");
    _platform.MessageService.ConfirmYesNoResult = false;   // user keeps editing

    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);

    Assert.False(allow);
    Assert.True(_platform.MessageService.ConfirmDialogShown);
}

[Fact]
public void CanClose_SystemShutdown_DoesNotPrompt()
{
    _view.SimulateEdit("new value");

    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.SystemShutdown, ok => allow = ok);

    Assert.True(allow);
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

### Push 方向 — `RequestClose` をテストする

テストの前に記録用の `ICloseSink` を束縛してからアクションを Dispatch します:

```csharp
private sealed class RecordingSink : ICloseSink
{
    public readonly List<(object result, InteractionStatus status)> Closed
        = new List<(object, InteractionStatus)>();
    public void Close(object result, InteractionStatus status)
        => Closed.Add((result, status));
}

[Fact]
public void Save_PushesResultWithOkStatus()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _view.SimulateEdit("hello");
    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Save);

    Assert.Single(sink.Closed);
    Assert.Equal("hello", sink.Closed[0].result);
    Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
}

[Fact]
public void Cancel_PushesCancelStatus()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Cancel);

    Assert.Equal(InteractionStatus.Cancel, sink.Closed[0].status);
}
```

### 単一正本の不変条件テスト

Push (Save) の後に続く `CanClose` 呼び出しが再プロンプトを出さないことを検証します:

```csharp
[Fact]
public void Save_ThenCanClose_DoesNotPrompt()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _view.SimulateEdit("hello");
    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Save);

    // Simulate the framework re-running the Pull gate after the Push close.
    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);

    Assert.True(allow);
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

完全なテスト例: `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs` および `WindowClosingDemoPresenterTests.cs`。
テスト駆動の完全なガイド: [HowTo: Presenter をテストする](HowTo-Test-A-Presenter)。

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — 設計の意図と内部構造
- [WindowNavigator](Reference-WindowNavigator) — Navigator API、Fluent 形式、`InteractionResult<T>`
- [ChangeTracker](Reference-ChangeTracker) — ダーティ状態の追跡
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `CanClose`、`RequestClose`、`Connect` の位置づけ
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — 完全なテストパターン
