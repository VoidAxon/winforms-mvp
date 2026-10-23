# HowTo: ウィンドウクローズを扱う

このページでは、典型的なウィンドウクローズシナリオの実装パターンを示します。
「1 つのゲート、2 つの起点」モデルの設計意図については [ウィンドウクローズモデル](Concept-Window-Closing-Model) を、`WindowNavigator` の API リファレンスは [WindowNavigator](Reference-WindowNavigator) を参照してください。

> **基本ルール**: Form はクローズコードを **まったく** 書きません (「閉じる」ボタンも `Close()` ではなく ViewAction)。`IWindowView` にクローズ関連のメンバーはありません。すべてのクローズポリシーは Presenter に置きます。
>
> **1 つのゲート**: ウィンドウが開いた後は、X / Alt+F4 / オーナーのクローズも Presenter の `RequestClose` も、同じ `CanClose` / `CanCloseAsync` を通ります (`RequestClose` は `CloseReason.Normal`)。ゲートを通らないのはシステムシャットダウン / タスクマネージャと、表示前の `RequestClose` (表示のキャンセル) だけです。

---

## 目次

- [シナリオ 1: シンプルな OK/Cancel ダイアログ](#シナリオ-1-シンプルな-okcancel-ダイアログ)
- [シナリオ 2: クローズ時のダーティチェック](#シナリオ-2-クローズ時のダーティチェック)
- [「閉じる」ボタン — X と同じ確認を出す](#閉じるボタン--x-と同じ確認を出す)
- [シナリオ 3: 業務結果を返すダイアログ](#シナリオ-3-業務結果を返すダイアログ)
- [シナリオ 4: 親が結果を受け取る](#シナリオ-4-親が結果を受け取る)
- [シナリオ 5: システムシャットダウンはフレームワークが許可する](#シナリオ-5-システムシャットダウンはフレームワークが許可する)
- [シナリオ 6: 非同期クローズ判断](#シナリオ-6-非同期クローズ判断)
- [シナリオ 7: Adopted ホスティング (シェル / レガシー Form)](#シナリオ-7-adopted-ホスティング-シェル--レガシー-form)
- [シナリオ 8: 表示前に開くのを取りやめる](#シナリオ-8-表示前に開くのを取りやめる)
- [テストパターン](#テストパターン)

---

## シナリオ 1: シンプルな OK/Cancel ダイアログ

ダーティ状態のない最小限のダイアログです。Save/Cancel ハンドラから結果を Push するだけです。X ボタンでいつでも閉じてよいウィンドウなら `CanClose` のオーバーライドは不要です (`RequestClose` もゲートを通りますが、既定の `CanClose` は常に `true` です)。

> モーダルダイアログでも、アクションにバインドしたボタンの `DialogResult` は `None` のままにします。`DialogResult.OK` などを設定すると、WinForms がハンドラやゲートの判断を待たずにダイアログを閉じてしまいます。

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
        Dispatcher.Register(StandardActions.Cancel, OnCancel);   // explicit "discard and close"
    }

    // ── The gate: X / Alt+F4 / owner closing / RequestClose ─────────────────
    protected override bool CanClose(CloseReason reason)
    {
        if (!HasUnsavedChanges) return true;

        return Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    }

    // ── Push direction: Save / Cancel buttons (they go through the gate too) ─
    private void OnSave()
    {
        // Read the final view state before persisting; EditChanged may not have
        // fired if the control with focus has not yet committed its value.
        var user = View.GetModel();
        SaveUser(user);
        _changeTracker.AcceptChanges(user);   // snapshot = saved value; the gate sees clean state
        RequestClose(BuildResult(), InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        // Discard: stop tracking the discarded edits BEFORE RequestClose (this does not
        // mean they were saved), so the gate this close goes through does not ask.
        _changeTracker.AcceptChanges(View.GetModel());
        RequestClose(InteractionStatus.Cancel);
    }

    private UserResult BuildResult() => new UserResult { /* ... */ };
}
```

### 保存 / 破棄でプロンプトが出ない理由

`RequestClose` も X と同じゲート (`CanClose`) を通ります。Push ハンドラ (`OnSave` / `OnCancel`) は `RequestClose` を呼ぶ **前に** ダーティ状態を確定させる (保存なら保存した値で、破棄なら破棄する画面の値で `AcceptChanges`) ので、ゲートはクリーンな状態を見て確認なしに `true` を返します。ダーティ確認は `CanClose` にだけ置くという約束の裏返しです。

```
OnSave()
    ├─ AcceptChanges(user)     ← finalize the dirty state first
    └─ RequestClose(result, Ok)          (candidate result of this close)
          ↓
    WindowLifecycleController.Close():  stores the candidate, form.Close()
          ↓
    FormClosing → CanClose(CloseReason.Normal) — clean → true, no prompt
          ↓
    FormClosed → converge → InteractionResult(Ok, result)
```

- ゲートが拒否した場合 (確定を忘れてダーティのまま `RequestClose` し、確認で「いいえ」が選ばれた場合など)、ウィンドウは開いたままで、渡した結果は捨てられます。後のクローズに持ち越されることはありません。
- 拒否されても、ハンドラがすでに行った処理 (破棄の `AcceptChanges`、保存) は巻き戻りません。
- 保存中フラグなどで「処理中は閉じない」と拒否するゲートは、ハンドラ自身の `RequestClose` もブロックします。フラグは `RequestClose` の **前に** 下ろしてください。

---

## 「閉じる」ボタン — X と同じ確認を出す

保存も破棄もしない「閉じる」ボタンは、X と同じ動作 (未保存の編集があれば確認) にしたいボタンです。`RequestClose` がゲートを通るので、ハンドラは `RequestClose` を呼ぶだけです。確認は `CanClose` / `CanCloseAsync` がそのまま出します:

```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(StandardActions.Save, OnSave, canExecute: () => HasUnsavedChanges);
    // Same as X: goes through CanClose, which asks when there are unsaved edits.
    Dispatcher.Register(StandardActions.Close, () => RequestClose(InteractionStatus.Cancel));
}
```

Form 側はボタンをアクションにバインドするだけで、`btnClose_Click => Close()` は書きません:

```csharp
_binder.Add(StandardActions.Close, btnClose);
```

| ボタン | ハンドラ | 未保存の編集があるとき |
|--------|----------|------------------------|
| 保存 | 保存 → `AcceptChanges` → `RequestClose(result, Ok)` | 確認なしで閉じる |
| 破棄して閉じる | `AcceptChanges` → `RequestClose(Cancel)` | 確認なしで閉じる |
| 閉じる | `RequestClose(Cancel)` | X と同じ確認 (ゲート) |

確認で「はい (保存)」を選んだときに結果を返したい場合は、[ゲートの中で `RequestClose`](#クローズ時に保存した結果を呼び出し元へ返す) を呼びます。「閉じる」ハンドラが渡した `Cancel` は置き換えられます。テスト例は [テストパターン](#閉じるアクションをテストする) を参照してください。

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

- タスクが未完了の間、ウィンドウは開いたままです。`true` で完了すると同じクローズが (再質問せずに) 再発行され、`false` ならウィンドウは開いたままです。
- 判定待ちの間の再クローズ (X の連打、「閉じる」ボタンの連打、`RequestClose`) は新しいゲートを開始せずに保留されます。`RequestClose` は判定待ちのクローズの結果を更新します。ただし `SystemShutdown` / `TaskManager` によるクローズは通ります。owner / MDI 親 / `Application.Exit` から来たクローズが非同期に許可された場合は、そのクローズが再発行されます。
- タスクが fault / キャンセルされるとクローズはブロックされ、エラーはログ出力されます (システムレベルのクローズは許可)。想定内の失敗は上の例のように中で捕まえて `Messages` で伝え、`false` を返します。
- `SystemShutdown` / `TaskManager` によるクローズは `CanCloseAsync` に届きません ([シナリオ 5](#シナリオ-5-システムシャットダウンはフレームワークが許可する))。

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
- `CanCloseAsync` でも同じです。`await` の後で `RequestClose` を呼び、`true` を返します。判定待ちの間に呼ばれた `RequestClose` (保存ボタンからのものも含む) は新しいクローズを始めず、判定中のクローズの結果を置き換えます (最後の呼び出しが勝ち、ゲートが拒否すれば捨てられます)。
- `RequestClose` を呼ばずに `true` を返した場合は、そのクローズを始めたもの が渡した結果で閉じます: X なら `Cancel` (結果なし)、「閉じる」ボタンの `RequestClose(InteractionStatus.Cancel)` なら `Cancel`。上の `ComposeEmailPresenter` は結果が「送信したか」を意味するため、下書き保存では `RequestClose` を呼びません。
- `PresenterTestHost` も同じ規則です: ゲート内の `RequestClose` は、ゲートが許可したときだけ `CloseRequested` / `CloseResult` に記録されます (`host.CanClose()` / `await host.CanCloseAsync()` からでも、ゲートを通る `RequestClose` からでも)。

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

## シナリオ 8: 表示前に開くのを取りやめる

`OnInitialize`、`OnInitializeAsync` の同期部分 (最初の `await` まで)、またはフレームワークが close sink をバインドする前に `RequestClose` を呼ぶと、ウィンドウを閉じるのではなく **開くのを取りやめます**。ユーザーはまだ何も編集できないのでゲート (`CanClose`) は呼ばれず、ウィンドウは表示されません。複数回呼んだ場合は最後の結果が使われます:

```csharp
protected override void OnInitialize(EditOrderParameters parameters)
{
    var order = _repository.Find(parameters.OrderId);
    if (order == null)
    {
        Messages.ShowWarning("注文が見つかりません。");
        RequestClose(InteractionStatus.Cancel);   // cancels the opening: the window is never shown
        return;
    }
    View.Order = order;
}
```

| 表示方法 | 結果 |
|----------|------|
| `ShowAsModal<TResult>()` | `ShowDialog` は呼ばれず、`RequestClose` の結果がそのまま返る |
| `ShowWindow(...)` (モードレス) | `Show` は呼ばれず、`null` が返る。キーを解放し、`onClosed` を 1 回呼んでから (ここで同じキーで開き直せる) Presenter と Form を破棄する |
| `presenter.Connect(form); form.Show();` (Adopted) | 呼び出し側のコードはそのまま。フレームワークが `Load` で Form を不可視化 (不透明度 0・全モニターの外へ移動) してから閉じるので何も表示されない (Windows 上で、モーダル・モードレスともちらつかないことを確認済み) |

Adopted の制限:

- MDI 子ウィンドウは不可視化できないため `Load` でそのまま閉じます (一瞬見える場合があります)。
- `Connect` より前に `Show` 済みの Form では、通常の (ゲートを通る) クローズになります。
- `Application.Run(form)` のメインフォームで取りやめると、アプリケーションが終了します。
- 他の `FormClosing` ハンドラがこのクローズを拒否した場合は、Form を元に戻して通常どおり表示します。

`OnInitializeAsync` の最初の `await` より後で `RequestClose` を呼ぶと、ウィンドウはすでに表示されているので通常の (ゲートを通る) クローズになります。

---

## テストパターン

テストでは `PresenterTestHost` (`WinformsMVP.Testing` パッケージ) で Presenter をモック View にホストします。`Start` は本番と同じ初期化を実行し、`RequestClose` を本番と同じくゲートに通して **受理されたクローズ** を記録し (`CloseRequested` / `CloseCount` / `CloseResult` / `CloseStatus`)、`CanClose(reason)` / `CanCloseAsync(reason)` でゲートを直接駆動します。`Start` の同期部分の中の `RequestClose` は表示のキャンセルとして (ゲートなしで) 記録されます。Form も `InternalsVisibleTo` も不要です:

```csharp
private readonly FakeServices _platform = TestServices.CreateFakes();   // フェイクのフレームワークサービス
private readonly MockView _view = new MockView();
private readonly PresenterTestHost _host;

public WindowClosingDemoPresenterTests()
{
    _host = PresenterTestHost.Start(_platform.Provider, new WindowClosingDemoPresenter(), _view);   // アタッチ + 初期化 + 受理されたクローズの記録
}
```

### ゲート — `CanClose` をテストする

`host.CanClose(reason)` は X / Alt+F4 と同じゲートを通り、`bool?` を返します (`CanCloseAsync` が未回答なら `null`)。`CanCloseAsync` をオーバーライドした Presenter では `await host.CanCloseAsync(reason)` で判定の完了を待ちます (例は後述):

```csharp
[Fact]
public void CanClose_WithUnsavedChanges_UserDeclines_BlocksClose()
{
    _view.SimulateEdit("new value");
    _platform.Messages.YesNoHandler = _ => false;          // user keeps editing

    Assert.False(_host.CanClose());                         // CloseReason.Normal
    Assert.Contains(_platform.Messages.Calls, c => c.IsConfirmation);
}

[Fact]
public void CanClose_SystemShutdown_DoesNotPrompt()
{
    _view.SimulateEdit("new value");

    Assert.True(_host.CanClose(CloseReason.SystemShutdown));
    Assert.DoesNotContain(_platform.Messages.Calls, c => c.IsConfirmation);
}
```

非同期の判定 (`CanCloseAsync`) は `await` してから検証します (`tests/.../Presenters/ComposeEmailPresenterTests.cs` より):

```csharp
[Fact]
public async Task Closing_WithDirtyState_UserSelectsYes_SavesDraftAndAllowsClose()
{
    _view.SimulateEmailDataChange();
    _platform.Messages.EnqueueYesNoCancel(ConfirmResult.Yes);

    bool allow = await _host.CanCloseAsync();   // awaits the draft save

    Assert.True(allow);
    Assert.Contains("SaveDraftAsync(Test)", _repository.MethodCalls);
}
```

### Push 方向 — `RequestClose` をテストする

アクションを Dispatch し、ホストが記録した結果とステータスを検証します (非同期ハンドラなら `await _view.Binder.DispatchAsync(...)` で完了を待つ)。`RequestClose` はゲートを通るので、保存 / 破棄のハンドラが `RequestClose` の前にダーティ状態を確定させていれば、確認なしに記録されます:

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

Save が確認を出さずに閉じる (ゲートがクリーンな状態を見る) ことを検証します:

```csharp
[Fact]
public void Save_DoesNotPrompt()
{
    _view.SimulateEdit("hello");
    _view.Binder.Dispatch(WindowClosingDemoActions.Save);

    Assert.True(_host.CloseRequested);
    Assert.DoesNotContain(_platform.Messages.Calls, c => c.IsConfirmation);
}
```

### 「閉じる」アクションをテストする

「閉じる」は X と同じゲートを通るので、確認の答えをフェイクに用意してから Dispatch します。拒否されたクローズは記録されません (上の [「閉じる」ボタン](#閉じるボタン--x-と同じ確認を出す) のように `StandardActions.Close` を登録した Presenter の例):

```csharp
[Fact]
public void Close_WithUnsavedChanges_No_KeepsTheWindowOpen()
{
    _view.SimulateEdit("hello");
    _platform.Messages.EnqueueYesNo(false);                 // "Discard unsaved changes?" → No

    _view.Binder.Dispatch(StandardActions.Close);

    Assert.Contains(_platform.Messages.Calls, c => c.IsConfirmation);
    Assert.False(_host.CloseRequested);                     // vetoed: not recorded
}

[Fact]
public void Close_WithUnsavedChanges_Yes_Closes()
{
    _view.SimulateEdit("hello");
    _platform.Messages.EnqueueYesNo(true);

    _view.Binder.Dispatch(StandardActions.Close);

    Assert.True(_host.CloseRequested);
    Assert.Equal(InteractionStatus.Cancel, _host.CloseStatus);
}
```

ゲートが `CanCloseAsync` で非同期に答える場合、Dispatch (`DispatchAsync` も) はゲートの判定を待ちません。`await _host.WaitForCloseDecisionAsync()` で判定を待ってから検証します:

```csharp
_view.Binder.Dispatch(StandardActions.Close);
await _host.WaitForCloseDecisionAsync();   // the async gate a RequestClose started has answered
Assert.True(_host.CloseRequested);
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
