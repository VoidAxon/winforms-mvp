# HowTo: 非同期処理を扱う

このページでは、Presenter での `async/await`、進捗表示、キャンセル処理の実装パターンを示します。
原則:**長時間処理は必ず非同期にして UI フリーズを避ける**。**UI スレッドへの戻り**は `await` の継続コンテキストが自動で処理してくれる。

フレームワークが用意している非同期の入口は 3 つです:

| 入口 | 用途 | 詳細 |
|---|---|---|
| 非同期アクションハンドラ (`Dispatcher.Register(key, async ct => ...)`) | ボタン等から起動する非同期処理 | [ViewAction § 非同期アクション](Reference-ViewAction-System#非同期アクション) |
| `protected override Task OnInitializeAsync(CancellationToken)` | ウィンドウを開いたときの初期データロード | [Presenter 基底クラス](Reference-Presenter-Base-Classes#oninitializeasync-と-lifetime) |
| `protected override Task<bool> CanCloseAsync(CloseReason)` | 閉じる前の保存・確認 | [HowTo: ウィンドウクローズ § シナリオ 6](HowTo-Handle-Window-Closing#シナリオ-6-非同期クローズ判断) |

どれも Presenter の `Lifetime` トークン (Presenter の破棄 = ウィンドウを閉じるとキャンセル) を受け取ります。

---

## 目次

- [基本パターン](#基本パターン)
- [シナリオ 1: シンプルな async/await](#シナリオ-1-シンプルな-asyncawait)
- [シナリオ 2: 進捗バーで進行状況を表示する](#シナリオ-2-進捗バーで進行状況を表示する)
- [シナリオ 3: キャンセルをサポートする](#シナリオ-3-キャンセルをサポートする)
- [シナリオ 4: 複数の async 操作を並行実行](#シナリオ-4-複数の-async-操作を並行実行)
- [シナリオ 5: ウィンドウを開いたときに読み込む](#シナリオ-5-ウィンドウを開いたときに読み込む)
- [処理中にウィンドウを閉じる](#処理中にウィンドウを閉じる)
- [`async void` を書かない](#async-void-を書かない)
- [UI スレッド注意点](#ui-スレッド注意点)
- [ベストプラクティス](#ベストプラクティス)
- [テストパターン](#テストパターン)
- [関連ページ](#関連ページ)

---

## 基本パターン

ViewAction ハンドラは `private async Task` で書き、そのまま `Dispatcher.Register` に渡します (非同期オーバーロードに束縛されます)。Presenter は WinForms に依存しないので、`SynchronizationContext` を使った `Invoke` 等は不要です。`await` の後の継続は自動で UI スレッドに戻ります (`ServiceLocator` が UI スレッドで構築されている限り)。

```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(StandardActions.Refresh, OnRefreshAsync);
}

private async Task OnRefreshAsync(CancellationToken ct)
{
    View.ShowLoadingIndicator(true);

    try
    {
        var data = await _repository.GetDataAsync(ct);
        View.Data = data;                         // ← UI スレッドで実行される
    }
    catch (IOException ex)
    {
        Messages.ShowError($"Failed: {ex.Message}", "Error");   // 想定内のエラーはここで伝える
    }
    finally
    {
        View.ShowLoadingIndicator(false);
    }
}
```

ハンドラの実行中、フレームワークは:

- そのアクションを無効化します (`CanDispatch == false`、バインド済みボタンはグレーアウト、連打は無視)。ビジーフラグや `RaiseCanExecuteChanged()` は不要です。
- 開始時と終了時に全 `CanExecute` を再評価します。
- 捕まえなかった例外 (`await` の後のものも含む) をログ出力し、アクションを再び有効にします。アプリはクラッシュしません。
- `ct` は Presenter の `Lifetime` です。ウィンドウが閉じられるとキャンセルされ、それによる `OperationCanceledException` は静かに処理されます。

---

## シナリオ 1: シンプルな async/await

```csharp
public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private readonly ICustomerRepository _repository;
    private bool _isLoading;

    public CustomerListPresenter(ICustomerRepository repository)
    {
        _repository = repository;
    }

    protected override void RegisterViewActions()
    {
        // Refresh 自身は実行中に自動で無効化される
        Dispatcher.Register(StandardActions.Refresh, OnRefreshAsync);
        // 読み込み中は Delete も押させない
        Dispatcher.Register(StandardActions.Delete, OnDelete,
            canExecute: () => !_isLoading && View.HasSelection);
    }

    private async Task OnRefreshAsync(CancellationToken ct)
    {
        _isLoading = true;
        View.IsLoading = true;

        try
        {
            View.Customers = await _repository.GetAllAsync(ct);
        }
        catch (DataAccessException ex)
        {
            Messages.ShowError($"Failed to load customers: {ex.Message}", "Error");
        }
        finally
        {
            _isLoading = false;
            View.IsLoading = false;
        }
    }
}
```

ポイント:

- 実行中のアクション自身の無効化はフレームワークが行う
- **他の**アクションも止めたいときだけ、`canExecute` が参照するフラグを持つ。最初の `await` より前にフラグを立てれば、開始時・終了時の自動再評価に反映されるので `RaiseCanExecuteChanged()` は不要
- `finally` で必ず状態を戻す
- ユーザーに見せるべきエラーはハンドラ内で `Messages` を使って伝える (想定外の例外はフレームワークがログに出す)

---

## シナリオ 2: 進捗バーで進行状況を表示する

`IProgress<T>` パターンで進捗を UI に伝えます。

```csharp
public interface ICustomerListView : IWindowView
{
    void UpdateProgress(int current, int total);
    bool IsLoading { get; set; }
}

public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private async Task OnImportAsync(CancellationToken ct)
    {
        var fileResult = Dialogs.ShowOpenFileDialog(/* ... */);
        if (!fileResult.IsOk) return;

        View.IsLoading = true;

        try
        {
            var progress = new Progress<ImportProgress>(p =>
            {
                View.UpdateProgress(p.CurrentItem, p.TotalItems);
            });

            await _importer.ImportAsync(fileResult.Value, progress, ct);

            Messages.ShowInfo("Import complete!", "Success");
        }
        catch (ImportException ex)
        {
            Messages.ShowError($"Import failed: {ex.Message}", "Error");
        }
        finally
        {
            View.IsLoading = false;
            View.UpdateProgress(0, 0);
        }
    }
}

public class ImportProgress
{
    public int CurrentItem { get; set; }
    public int TotalItems { get; set; }
}
```

業務側 (`ICustomerImporter`) の例:

```csharp
public async Task ImportAsync(string filePath, IProgress<ImportProgress> progress, CancellationToken ct)
{
    var rows = await Task.Run(() => File.ReadAllLines(filePath), ct);

    for (int i = 0; i < rows.Length; i++)
    {
        await ProcessRowAsync(rows[i], ct);
        progress?.Report(new ImportProgress { CurrentItem = i + 1, TotalItems = rows.Length });
    }
}
```

`IProgress<T>` を `Progress<T>` で実装すると、`Report` 呼び出しは **自動的に UI スレッドにマーシャル** されます (`Progress<T>` は構築時に `SynchronizationContext` をキャプチャするため)。

---

## シナリオ 3: キャンセルをサポートする

ハンドラが受け取るトークンはウィンドウを閉じたときにキャンセルされます。ユーザーの「キャンセル」ボタンも付けたい場合は、そのトークンにリンクした `CancellationTokenSource` を作ります (`samples/WinformsMVP.Samples/AsyncDemo/AsyncDemoPresenter.cs` と同じ形)。

```csharp
public class ImportPresenter : WindowPresenterBase<IImportView>
{
    private CancellationTokenSource _cts;   // 実行中だけ非 null

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(ImportActions.Start, OnStartAsync);     // 実行中は自動で無効化
        Dispatcher.Register(ImportActions.Cancel, OnCancel,
            canExecute: () => _cts != null);                         // 実行中だけ有効化
    }

    private async Task OnStartAsync(CancellationToken ct)
    {
        // ct (ウィンドウを閉じる) と Cancel ボタンのどちらでも止まる
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _importer.ImportAsync(filePath, _cts.Token);
            Messages.ShowInfo("Completed!", "Success");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // ユーザーが Cancel を押した — エラーダイアログは出さない。
            // (ウィンドウが閉じられた場合は例外がそのまま抜け、フレームワークが静かに処理する)
            View.UpdateStatus("Cancelled by user.");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnCancel()
    {
        _cts?.Cancel();
    }
}
```

`_cts` は最初の `await` より前に設定され、`finally` で戻されるので、Cancel ボタンの有効/無効はフレームワークの開始時・終了時の再評価で追従します (`RaiseCanExecuteChanged()` は不要)。

業務側はトークンをチェックします:

```csharp
public async Task ImportAsync(string filePath, CancellationToken cancellationToken)
{
    var rows = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);

    foreach (var row in rows)
    {
        cancellationToken.ThrowIfCancellationRequested();    // ← キャンセル検出
        await ProcessRowAsync(row, cancellationToken);
    }
}
```

`ThrowIfCancellationRequested()` は `OperationCanceledException` を投げます。これは「エラー」ではなく「ユーザーの意志」なので、Presenter 側で別途 catch して **エラーダイアログを出さない** のが推奨。

---

## シナリオ 4: 複数の async 操作を並行実行

複数のソースから同時にデータを取得する場合、`Task.WhenAll` で並行化:

```csharp
private async Task OnLoadAllAsync(CancellationToken ct)
{
    View.IsLoading = true;

    try
    {
        var customersTask = _customerRepo.GetAllAsync(ct);
        var ordersTask    = _orderRepo.GetAllAsync(ct);
        var productsTask  = _productRepo.GetAllAsync(ct);

        await Task.WhenAll(customersTask, ordersTask, productsTask);

        View.Customers = customersTask.Result;
        View.Orders    = ordersTask.Result;
        View.Products  = productsTask.Result;
    }
    catch (DataAccessException ex)
    {
        Messages.ShowError($"Failed: {ex.Message}", "Error");
    }
    finally
    {
        View.IsLoading = false;
    }
}
```

`Task.WhenAll` は **すべての Task が完了 (成功 or 失敗) するまで待ち**、いずれかが失敗すると最初の例外を再 throw します。

---

## シナリオ 5: ウィンドウを開いたときに読み込む

初期データのロードは `OnInitializeAsync` に置きます。同期の `OnInitialize` の直後に開始され、ウィンドウはその完了を待たずに表示されます。

```csharp
protected override void OnInitialize()
{
    View.IsLoading = true;                       // すぐに見せたい初期状態
}

protected override async Task OnInitializeAsync(CancellationToken ct)
{
    try
    {
        View.Customers = await _repository.GetAllAsync(ct);
    }
    catch (DataAccessException ex)
    {
        Messages.ShowError($"Failed to load customers: {ex.Message}", "Error");
    }
    finally
    {
        View.IsLoading = false;
    }
}
```

未処理の例外はログ出力されます。`ct` は `Lifetime` なので、読み込み中にウィンドウを閉じると静かに止まります。

---

## 処理中にウィンドウを閉じる

時間のかかる処理 (一括変換、取り込み) の途中で ✕ / 「閉じる」が押されたら、`CanCloseAsync` で「中止して閉じますか？」と聞き、はい なら止めて **処理が本当に終わるのを待ってから** 閉じます。止める指示だけ出してすぐ閉じると、処理の続きが破棄済みの View に触ります。

```csharp
private CancellationTokenSource _stop;   // 実行中だけ非 null (シナリオ 3 と同じ)
private Task _run = Task.CompletedTask;
private bool _closing;

protected override async Task<bool> CanCloseAsync(CloseReason reason)
{
    if (_stop == null) return true;
    var yes = Messages.ConfirmYesNo("処理中です。中止して閉じますか？");
    if (_stop == null) return yes;            // 質問の間に処理が終わった (下の注意 2)
    if (!yes) return false;
    _closing = true;                           // 処理の終わりに「中止しました」を出さない
    _stop.Cancel();
    await _run;                                // 今の 1 件が終わるまで待つ (待っている間の ✕ は聞き直されない)
    return true;
}
```

注意:

1. **`await` の後は `Lifetime` を見てから View に触る。** システムのシャットダウンはクローズ判断を通らずに閉じる (Presenter が破棄され `Lifetime` がキャンセルされる) ので、処理の続きが破棄済みの View を触ることがあります。フレームワークは Dispatcher を止めますが、View のメソッド呼び出しは止められません。

   ```csharp
   var result = await _converter.ConvertAsync(item);
   if (Lifetime.IsCancellationRequested) return;   // ウィンドウはもうない
   View.ShowItemState(item.Id, ItemState.Done);
   ```

2. **確認の後は状態を見直す。** `Messages.ConfirmYesNo` はモーダルで、表示中もメッセージループが回るため、`await` の続きが進んで処理が終わることがあります。質問から戻ったら「まだ処理中か」を確かめます。質問中に終わった処理の完了メッセージは、質問に重ねず、いいえ と答えた後に出します。テストでは `FakeMessageService.YesNoHandler` の中で処理を進めると再現できます。

---

## `async void` を書かない

非同期の ViewAction ハンドラは `async Task` で書き、`Dispatcher.Register` に渡します。`async` ラムダ (`async () => ...` / `async ct => ...`) も非同期オーバーロードに束縛されるので、`async void` にはなりません。

| 形式 | 可否 | 理由 |
|------|----|----|
| `async Task OnSaveAsync([CancellationToken])` (ViewAction ハンドラ) | ✅ | 実行中の無効化・完了時の `ActionExecuted`・失敗のログ・`DispatchAsync` での待機をフレームワークが提供 |
| `async void OnSave()` (ViewAction ハンドラ) | ❌ | `Action` オーバーロードに束縛され、フレームワークは完了を知らない (実行中も有効、例外はアプリをクラッシュさせ得る、テストで待てない) |
| `async void OnButtonClick(...)` (View 内の WinForms イベントハンドラ) | △ | WinForms の慣習上やむを得ない場合のみ。必ず try/catch。通常は ViewAction で Presenter に渡す |
| `async Task DoWorkAsync()` (業務メソッド) | ✅ | 標準 |

Presenter が View のイベント (例: `SelectionChanged`) に反応して非同期処理をしたい場合も、イベントハンドラを `async void` にしません。ユーザー操作ならその処理を非同期アクションとして登録して View から Dispatch させるのが基本です。イベントから直接始める場合は `_ = LoadAsync();` のように `async Task` メソッドを開始し、例外をすべてその中で処理して、トークンに `Lifetime` を渡します ([Master-Detail § async 化の例](HowTo-Implement-Master-Detail#async-化の例))。

---

## UI スレッド注意点

`Progress<T>` や `await` の継続は **自動で UI スレッドに戻る** のが基本ですが、以下のケースは注意:

| ケース | 動作 |
|------|----|
| `Progress<T>.Report` | ✅ コンストラクタ時の `SynchronizationContext` 上で実行される (UI スレッド上で `new` していれば OK) |
| `await someTask` | ✅ デフォルトでキャプチャされた `SynchronizationContext` に戻る |
| `await someTask.ConfigureAwait(false)` | ❌ UI スレッドに戻らない (UI を触ってはいけない) |
| `Task.Run(() => UpdateView())` | ❌ UI スレッドではない |

```csharp
// ✅ デフォルト挙動 — await の後は UI スレッド
private async Task OnSaveAsync()
{
    await Task.Delay(1000);
    View.Status = "Done";        // ← UI スレッドで実行 OK
}

// ❌ ConfigureAwait(false) を使うと UI スレッドに戻らない
private async Task OnSaveAsync()
{
    await Task.Delay(1000).ConfigureAwait(false);
    View.Status = "Done";        // ← クロススレッド例外！
}
```

**業務サービス側** (`async Task` メソッド) では `ConfigureAwait(false)` を使ってもよい (UI スレッドに戻る必要がないライブラリコード)。
**Presenter のハンドラ** では `ConfigureAwait(false)` を使わない。

---

## ベストプラクティス

### 1. UI 凍結を絶対に避ける

```csharp
// ❌ Bad — UI スレッドを長時間ブロックする
private void OnRefresh()
{
    var data = _repository.GetAll();   // 同期呼び出し — UI が固まる
    View.Data = data;
}

// ✅ Good — 非同期ハンドラとして登録する
private async Task OnRefreshAsync(CancellationToken ct)
{
    var data = await _repository.GetAllAsync(ct);
    View.Data = data;
}
```

### 2. 想定内のエラーはハンドラ内で捕まえて伝える

捕まえなかった例外はフレームワークがログに出してアクションを再有効化するだけで、ユーザーには何も表示されません。ユーザーに知らせるべき失敗 (通信エラー、検証エラー等) は `try`/`catch` して `Messages` で伝えます。

### 3. `finally` で UI 状態を戻す

```csharp
try
{
    View.IsLoading = true;
    await DoWorkAsync(ct);
}
catch (DataAccessException ex) { /* ... */ }
finally
{
    View.IsLoading = false;   // ← 例外時も必ず実行
}
```

### 4. 実行中の無効化はフレームワークに任せる

実行中のアクション自身はフレームワークが無効化します。手動のビジーフラグ + `RaiseCanExecuteChanged()` は不要です。**他の**アクションも止めたい場合だけフラグを持ち、`canExecute` から参照します ([シナリオ 1](#シナリオ-1-シンプルな-asyncawait))。

### 5. トークンを下まで渡す

ハンドラが受け取る `CancellationToken` (= `Lifetime`) を業務サービスまで渡せば、ウィンドウを閉じたときに処理が止まります。それ以外の非同期処理 (タイマー、ポーリング等) には Presenter の `Lifetime` プロパティを渡します。

### 6. 業務サービス側は `async Task` を返す

中間層も `async Task` で書いてテスト可能にする。

---

## テストパターン

非同期ハンドラの完了は `DispatchAsync` で、`OnInitializeAsync` は `host.Initialized` で待ちます (`Task.Delay` やテスト用ヘルパーは不要)。

```csharp
[Fact]
public async Task Refresh_LoadsData()
{
    _repository.Customers = new[] { new Customer { Name = "Alice" } };

    await _view.Binder.DispatchAsync(StandardActions.Refresh);   // ハンドラの完了を待つ

    Assert.Equal(1, _view.Customers.Count);
    Assert.Equal("Alice", _view.Customers[0].Name);
}

[Fact]
public async Task Refresh_WhenRepositoryThrows_ShowsError()
{
    _repository.GetAllAsyncShouldThrow = true;

    await _view.Binder.DispatchAsync(StandardActions.Refresh);

    Assert.True(_platform.MessageService.ErrorMessageShown);
}

[Fact]
public async Task Opening_LoadsCustomers()
{
    using (var host = PresenterTestHost.Start(new CustomerListPresenter(_repository), _view))
    {
        await host.Initialized;                                  // OnInitializeAsync の完了を待つ
        Assert.False(_view.IsLoading);
    }
}
```

`DispatchAsync` / `Initialized` はハンドラの例外で fault しません (本番と同じくログに出るだけ)。詳しくは [HowTo: Presenter をテストする § 非同期処理をテストする](HowTo-Test-A-Presenter#非同期処理をテストする)。

---

## 関連ページ

- [ViewAction システム § 非同期アクション](Reference-ViewAction-System#非同期アクション) — 非同期ハンドラの仕様、`DispatchAsync`、ミドルウェアとの関係
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `OnInitializeAsync`、`Lifetime`
- [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) — `CanCloseAsync` (閉じる前の保存)
- [HowTo: エラー処理戦略](HowTo-Handle-Errors) — async での例外処理
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — async テスト
- [EventAggregator](Reference-EventAggregator) — バックグラウンドからの UI 通知 (自動マーシャリング)
- サンプル:
  - `samples/WinformsMVP.Samples/AsyncDemo/` — 非同期ハンドラ / Progress / キャンセルの完全な実装
  - `samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs` — 非同期送信・下書き保存と `CanCloseAsync`
