# HowTo: ウィンドウクローズを扱う

このページでは、ウィンドウクローズに関わる典型シナリオの実装パターンを示します。
設計思想 (なぜ Push/Pull 二方向モデルなのか) は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を、`WindowNavigator` の API 一覧は [WindowNavigator](Reference-WindowNavigator) を参照してください。

---

## 目次

- [シナリオ 1: シンプルな OK/Cancel ダイアログ](#シナリオ-1-シンプルな-okcancel-ダイアログ)
- [シナリオ 2: ダーティチェック付き編集ダイアログ](#シナリオ-2-ダーティチェック付き編集ダイアログ)
- [シナリオ 3: 結果を返すダイアログ](#シナリオ-3-結果を返すダイアログ)
- [シナリオ 4: 親ウィンドウから結果を受け取る](#シナリオ-4-親ウィンドウから結果を受け取る)
- [シナリオ 5: システムシャットダウン時はブロックしない](#シナリオ-5-システムシャットダウン時はブロックしない)
- [Form 側のお決まりコード](#form-側のお決まりコード)
- [テストパターン](#テストパターン)

---

## シナリオ 1: シンプルな OK/Cancel ダイアログ

結果を返さない・ダーティ判定もない、最小のダイアログ。

```csharp
public class ConfirmDeleteDialogPresenter : WindowPresenterBase<IConfirmDeleteView>,
                                            IRequestClose<bool>
{
    public event EventHandler<CloseRequestedEventArgs<bool>> CloseRequested;

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Ok,     OnOk);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    private void OnOk()
        => RaiseClose(true, InteractionStatus.Ok);

    private void OnCancel()
        => RaiseClose(false, InteractionStatus.Cancel);

    private void RaiseClose(bool confirmed, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<bool>(confirmed, status));
}

// 呼び出し側
var result = Navigator.For(new ConfirmDeleteDialogPresenter()).ShowAsModal<bool>();
if (result.IsOk && result.Value)
{
    DeleteItem();
}
```

ポイント: ユーザーが × ボタンを押した場合も `result.IsCancelled` として処理されるため、特別な対応は不要です。

---

## シナリオ 2: ダーティチェック付き編集ダイアログ

ユーザーが変更を加えた状態で × / Cancel を押したとき、確認ダイアログを出すパターン。

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>,
                                  IRequestClose<UserResult>
{
    public event EventHandler<CloseRequestedEventArgs<UserResult>> CloseRequested;

    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnViewAttached()
    {
        View.Closing += OnViewClosing;       // ← Pull 方向を購読
    }

    protected override void OnInitialize(EditUserParameters parameters)
    {
        var user = LoadUser(parameters.UserId);
        _changeTracker = new ChangeTracker<UserModel>(user);
        View.Bind(_changeTracker.CurrentValue);

        // ChangeTracker の状態変化で CanExecute を再評価
        _changeTracker.IsChangedChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => _changeTracker.IsChanged);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    // ── Pull 方向: × ボタン / Alt+F4 ────────────────────────────
    private void OnViewClosing(object sender, WindowClosingEventArgs args)
    {
        if (args.Reason == CloseReason.SystemShutdown ||
            args.Reason == CloseReason.TaskManager)
            return;                          // システム終了はブロックしない

        if (_changeTracker.IsChanged)
        {
            if (!Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
                args.Cancel = true;          // ← クローズをブロック
        }
    }

    // ── Push 方向: Save / Cancel ボタン ──────────────────────────
    private void OnSave()
    {
        SaveUser(_changeTracker.CurrentValue);
        _changeTracker.AcceptChanges();     // モデル状態を確定 (クローズ抑止のためではない)
        RaiseClose(BuildResult(), InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();     // モデル状態を破棄
        RaiseClose(null, InteractionStatus.Cancel);
    }

    private void RaiseClose(UserResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserResult>(result, status));

    private UserResult BuildResult() => new UserResult { /* ... */ };
}
```

### なぜ二重確認にならないか

Push 起点の閉じでは、フレームワークが Pull 方向のゲート (`OnViewClosing`) を **そもそも呼びません**。`WindowNavigator` は `CloseRequested` を受けて `form.Close()` を呼ぶ直前に、その閉じを「Presenter 起点」として記録し (`WindowCloseCoordinator`)、後続の `FormClosing` ブリッジはその記録を見てゲートをスキップします。

```
Save ボタンクリック
   ↓
OnSave()
   ├─ SaveUser()
   ├─ AcceptChanges()           ← モデル状態を確定 (クローズ抑止には不要)
   └─ RaiseClose(result, Ok)
        ↓
   Navigator: この閉じを Presenter 起点として記録 (WindowCloseCoordinator)
        ↓
   Navigator が form.Close()
        ↓
   FormClosing 発火
        ↓
   Presenter 起点なのでブリッジは OnViewClosing を呼ばない
        └─ 確認ダイアログは構造的に出ない (AcceptChanges の順序に依存しない)
```

したがって、二重確認の回避は「`AcceptChanges` を `RaiseClose` の前に呼ぶ」という規約ではなく、フレームワークの構造によって保証されます。詳細は [単一情報源の不変条件](Concept-Window-Closing-Model#単一情報源-single-source-of-truth-の不変条件) を参照。

---

## シナリオ 3: 結果を返すダイアログ

業務データ (オブジェクト) を返す。

```csharp
public class CustomerResult
{
    public int Id { get; init; }
    public string Name { get; init; }
}

public class EditCustomerPresenter : WindowPresenterBase<IEditCustomerView>,
                                      IRequestClose<CustomerResult>
{
    public event EventHandler<CloseRequestedEventArgs<CustomerResult>> CloseRequested;

    private void OnSave()
    {
        var customer = new CustomerResult
        {
            Id = _customerId,
            Name = View.CustomerName,
        };
        RaiseClose(customer, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RaiseClose(null, InteractionStatus.Cancel);

    private void RaiseClose(CustomerResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<CustomerResult>(result, status));
}
```

---

## シナリオ 4: 親ウィンドウから結果を受け取る

`Navigator.For(...).ShowAsModal<TResult>()` で `InteractionResult<TResult>` を受け取ります。

```csharp
public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private void OnEditCustomer()
    {
        var selectedId = View.SelectedCustomerId;
        if (selectedId == null) return;

        var presenter = new EditCustomerPresenter();
        var parameters = new EditCustomerParameters { CustomerId = selectedId.Value };

        var result = Navigator.For(presenter)
                              .WithParam(parameters)
                              .ShowAsModal<CustomerResult>();

        if (result.IsOk)
        {
            // ユーザーが Save を押した
            ReloadCustomer(result.Value.Id);
        }
        else if (result.IsCancelled)
        {
            // ユーザーが Cancel か × を押した — 何もしない
        }
        else if (result.IsError)
        {
            Messages.ShowError(result.ErrorMessage, "Error");
        }
    }
}
```

ユーザーが Save ボタンで閉じた場合も、× ボタンで「変更を保持」を選んだ場合も、戻り値は **同じ `InteractionResult<TResult>`** に集約されます。呼び出し元は経路を意識する必要がありません。

---

## シナリオ 5: システムシャットダウン時はブロックしない

Windows がシャットダウン中なのにダーティ確認ダイアログを出すと、ユーザーは「終了」を選びにくくなり最悪フリーズします。`CloseReason` をチェックして適切に分岐してください。

```csharp
private void OnViewClosing(object sender, WindowClosingEventArgs args)
{
    // システム終了系はそのまま許可
    if (args.Reason == CloseReason.SystemShutdown ||
        args.Reason == CloseReason.TaskManager)
        return;

    // 親ウィンドウからの伝播もブロックしない (好み)
    if (args.Reason == CloseReason.ParentClosing)
        return;

    // 通常の × / Alt+F4 だけ確認 (Presenter 起点の Close はこのゲートに来ない)
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo("Discard changes?", "Confirm"))
        args.Cancel = true;
}
```

`CloseReason` の値一覧は [Concept-Window-Closing-Model#closereason-列挙](Concept-Window-Closing-Model#closereason-列挙) を参照。

---

## Form 側のお決まりコード

`IWindowView` を実装する Form には、以下の **2 行のボイラープレート** が必要です。
明示的インターフェイス実装にする理由は、`Form` 自身が既に非推奨の `Closing` イベントを持っているためです。

```csharp
public partial class EditUserForm : Form, IEditUserView
{
    private EventHandler<WindowClosingEventArgs> _closing;
    event EventHandler<WindowClosingEventArgs> IWindowView.Closing
    {
        add => _closing += value;
        remove => _closing -= value;
    }
    void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);

    // ... 他の View プロパティ・イベント
}
```

Form 自身が `FormClosing` を購読する必要はありません。`WindowNavigator` がクローズハンドラ登録時 (`WireCloseGate`) に自動的にブリッジします。

---

## テストパターン

### Pull 方向のテスト (フレームワークがクローズを発火するケース)

```csharp
[Fact]
public void Closing_WithUnsavedChanges_UserCancels_BlocksClose()
{
    // Arrange
    _view.SimulateEdit("new value");                       // ダーティ状態を作る
    _platform.MessageService.ConfirmYesNoResult = false;   // 「変更を保持」を選択

    // Act
    var args = _view.RaiseClosing(CloseReason.Normal);

    // Assert
    Assert.True(args.Cancel);                              // Presenter がブロックした
    Assert.True(_platform.MessageService.ConfirmDialogShown);
}

[Fact]
public void Closing_SystemShutdown_DoesNotPrompt()
{
    _view.SimulateEdit("new value");

    var args = _view.RaiseClosing(CloseReason.SystemShutdown);

    Assert.False(args.Cancel);                             // ブロックしない
    Assert.False(_platform.MessageService.ConfirmDialogShown);  // ダイアログも出さない
}
```

### Push 方向のテスト (Presenter が能動的にクローズを要求)

```csharp
[Fact]
public void Save_FiresCloseRequestedWithResult()
{
    // Arrange
    _view.SimulateEdit("hello");
    CloseRequestedEventArgs<UserResult> captured = null;
    _presenter.CloseRequested += (s, e) => captured = e;

    // Act
    _presenter.Dispatcher.Dispatch(StandardActions.Save);

    // Assert
    Assert.NotNull(captured);
    Assert.Equal("hello", captured.Result.Name);
    Assert.Equal(InteractionStatus.Ok, captured.Status);
}

[Fact]
public void Cancel_FiresCloseRequestedWithCancelStatus()
{
    CloseRequestedEventArgs<UserResult> captured = null;
    _presenter.CloseRequested += (s, e) => captured = e;

    _presenter.Dispatcher.Dispatch(StandardActions.Cancel);

    Assert.NotNull(captured);
    Assert.Null(captured.Result);
    Assert.Equal(InteractionStatus.Cancel, captured.Status);
}
```

完全なテストセットは `tests/WinformsMVP.Samples.Tests/Presenters/WindowClosingTests.cs` を、実行可能な最小サンプルは `samples/WinformsMVP.Samples/WindowClosingDemo/` を参照してください。

詳しいモック設定は [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) も合わせてどうぞ。

---

## 関連ページ

- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — 設計思想と内部の動き
- [WindowNavigator](Reference-WindowNavigator) — Navigator の API、Fluent 形式、`InteractionResult<T>`
- [ChangeTracker](Reference-ChangeTracker) — ダーティ追跡の実装
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `IRequestClose<TResult>` の実装方法
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テストパターン詳細
