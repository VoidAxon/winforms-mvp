# HowTo: 非同期処理を扱う

このページでは、Presenter での `async/await`、進捗表示、キャンセル処理の実装パターンを示します。
原則:**長時間処理は必ず非同期にして UI フリーズを避ける**。**UI スレッドへの戻り**は `await` の継続コンテキストが自動で処理してくれる。

---

## 目次

- [基本パターン](#基本パターン)
- [シナリオ 1: シンプルな async/await](#シナリオ-1-シンプルな-asyncawait)
- [シナリオ 2: 進捗バーで進行状況を表示する](#シナリオ-2-進捗バーで進行状況を表示する)
- [シナリオ 3: キャンセルをサポートする](#シナリオ-3-キャンセルをサポートする)
- [シナリオ 4: 複数の async 操作を並行実行](#シナリオ-4-複数の-async-操作を並行実行)
- [`async void` の取り扱い](#async-void-の取り扱い)
- [UI スレッド注意点](#ui-スレッド注意点)
- [ベストプラクティス](#ベストプラクティス)
- [テストパターン](#テストパターン)
- [関連ページ](#関連ページ)

---

## 基本パターン

ViewAction ハンドラは `private async void` で OK (詳細は [後述](#async-void-の取り扱い))。Presenter は WinForms に依存しないので、`SynchronizationContext` を使った `Invoke` 等は不要です。`await` の後の継続は自動で UI スレッドに戻ります (`PlatformServices` が UI スレッドで構築されている限り)。

```csharp
private async void OnRefresh()
{
    View.ShowLoadingIndicator(true);

    try
    {
        var data = await _repository.GetDataAsync();
        View.Data = data;                         // ← UI スレッドで実行される
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed: {ex.Message}", "Error");
        Logger.LogError(ex, "Refresh failed");
    }
    finally
    {
        View.ShowLoadingIndicator(false);
    }
}
```

---

## シナリオ 1: シンプルな async/await

```csharp
public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private readonly ICustomerRepository _repository;

    public CustomerListPresenter(ICustomerRepository repository)
    {
        _repository = repository;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Refresh, OnRefresh);
    }

    private async void OnRefresh()
    {
        View.IsLoading = true;
        Dispatcher.RaiseCanExecuteChanged();

        try
        {
            var customers = await _repository.GetAllAsync();
            View.Customers = customers;
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Failed to load customers: {ex.Message}", "Error");
            Logger.LogError(ex, "Failed to load customers");
        }
        finally
        {
            View.IsLoading = false;
            Dispatcher.RaiseCanExecuteChanged();
        }
    }
}
```

ポイント:

- `View.IsLoading = true` で UI をローディング状態に
- `Dispatcher.RaiseCanExecuteChanged()` で他のアクションを無効化 (Refresh 中は Save も Delete も押させない等)
- `finally` で必ず状態を戻す
- 例外はログに記録 + ユーザー向けメッセージ

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
    private async void OnImport()
    {
        var fileResult = Dialogs.ShowOpenFileDialog(/* ... */);
        if (!fileResult.IsSuccess) return;

        View.IsLoading = true;

        try
        {
            var progress = new Progress<ImportProgress>(p =>
            {
                View.UpdateProgress(p.CurrentItem, p.TotalItems);
            });

            await _importer.ImportAsync(fileResult.Value, progress);

            Messages.ShowInfo("Import complete!", "Success");
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Import failed: {ex.Message}", "Error");
            Logger.LogError(ex, "Import failed");
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
    public int CurrentItem { get; init; }
    public int TotalItems { get; init; }
}
```

業務側 (`ICustomerImporter`) の例:

```csharp
public async Task ImportAsync(string filePath, IProgress<ImportProgress> progress)
{
    var rows = await File.ReadAllLinesAsync(filePath);

    for (int i = 0; i < rows.Length; i++)
    {
        await ProcessRowAsync(rows[i]);
        progress?.Report(new ImportProgress { CurrentItem = i + 1, TotalItems = rows.Length });
    }
}
```

`IProgress<T>` を `Progress<T>` で実装すると、`Report` 呼び出しは **自動的に UI スレッドにマーシャル** されます (`Progress<T>` は構築時に `SynchronizationContext` をキャプチャするため)。

---

## シナリオ 3: キャンセルをサポートする

`CancellationToken` で長時間処理をユーザーがキャンセルできるようにします。

```csharp
public class ImportPresenter : WindowPresenterBase<IImportView>
{
    private CancellationTokenSource _cts;

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(ImportActions.Start, OnStart,
            canExecute: () => _cts == null);     // 実行中は無効化
        Dispatcher.Register(ImportActions.Cancel, OnCancel,
            canExecute: () => _cts != null);     // 実行中だけ有効化
    }

    private async void OnStart()
    {
        _cts = new CancellationTokenSource();
        Dispatcher.RaiseCanExecuteChanged();

        try
        {
            await _importer.ImportAsync(filePath, _cts.Token);
            Messages.ShowInfo("Completed!", "Success");
        }
        catch (OperationCanceledException)
        {
            // ユーザーがキャンセルした — 静かに何もしない
            View.UpdateStatus("Cancelled by user.");
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Failed: {ex.Message}", "Error");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            Dispatcher.RaiseCanExecuteChanged();
        }
    }

    private void OnCancel()
    {
        _cts?.Cancel();
    }
}
```

業務側はトークンをチェックします:

```csharp
public async Task ImportAsync(string filePath, CancellationToken cancellationToken)
{
    var rows = await File.ReadAllLinesAsync(filePath, cancellationToken);

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
private async void OnLoadAll()
{
    View.IsLoading = true;

    try
    {
        var customersTask = _customerRepo.GetAllAsync();
        var ordersTask    = _orderRepo.GetAllAsync();
        var productsTask  = _productRepo.GetAllAsync();

        await Task.WhenAll(customersTask, ordersTask, productsTask);

        View.Customers = customersTask.Result;
        View.Orders    = ordersTask.Result;
        View.Products  = productsTask.Result;
    }
    catch (Exception ex)
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

## `async void` の取り扱い

通常、`async void` は危険とされますが、**ViewAction ハンドラと UI イベントハンドラだけは許容** です。

| 形式 | 許容 | 理由 |
|------|----|----|
| `async void OnSave()` (ViewAction ハンドラ) | ✅ | イベントハンドラと同じパターン。Dispatcher が呼ぶ |
| `async void OnButtonClick(...)` (View 内) | ✅ | WinForms イベントハンドラの慣習 |
| `async void DoWork()` (業務メソッド) | ❌ | 例外を捕捉できない、テスト不可能 |
| `async Task DoWorkAsync()` (業務メソッド) | ✅ | 標準 |

`async void` ハンドラ内では **必ず try/catch** で例外を捕まえてください。捕まえないと AppDomain.UnhandledException 経由でアプリがクラッシュします。

```csharp
private async void OnSave()
{
    try
    {
        await SaveAsync();
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed: {ex.Message}", "Error");
        Logger.LogError(ex, "Save failed");
    }
}
```

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
private async void OnSave()
{
    await Task.Delay(1000);
    View.Status = "Done";        // ← UI スレッドで実行 OK
}

// ❌ ConfigureAwait(false) を使うと UI スレッドに戻らない
private async void OnSave()
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

// ✅ Good — async/await を使う
private async void OnRefresh()
{
    var data = await _repository.GetAllAsync();
    View.Data = data;
}
```

### 2. 必ず try/catch で囲む

`async void` は例外が呼び出し側に伝わらないため、ハンドラ内で必ず捕まえる。

### 3. `finally` で UI 状態を戻す

```csharp
try
{
    View.IsLoading = true;
    await DoWorkAsync();
}
catch (Exception ex) { /* ... */ }
finally
{
    View.IsLoading = false;   // ← 例外時も必ず実行
}
```

### 4. 実行中は他のアクションを無効化

```csharp
private bool _isRunning;

protected override void RegisterViewActions()
{
    Dispatcher.Register(CommonActions.Refresh, OnRefresh,
        canExecute: () => !_isRunning);
}

private async void OnRefresh()
{
    _isRunning = true;
    Dispatcher.RaiseCanExecuteChanged();
    try { await DoWorkAsync(); }
    finally
    {
        _isRunning = false;
        Dispatcher.RaiseCanExecuteChanged();
    }
}
```

### 5. キャンセル可能な長時間処理は `CancellationToken` を受け取れるようにする

ユーザーフレンドリーで、テストも書きやすい。

### 6. 業務サービス側は `async Task` を返す

`async void` は ViewAction ハンドラ等の最終層だけ。中間層は `async Task` で書いてテスト可能にする。

---

## テストパターン

```csharp
[Fact]
public async Task OnRefresh_LoadsData()
{
    _platform.CustomerRepository.SetupGetAllAsync(
        new[] { new Customer { Name = "Alice" } });

    _presenter.Dispatcher.Dispatch(CommonActions.Refresh);

    // 非同期完了を待つ
    await Task.Yield();
    await _presenter.LastAsyncOperation;   // 実装上のヘルパー

    Assert.Equal(1, _view.Customers.Count);
    Assert.Equal("Alice", _view.Customers[0].Name);
}

[Fact]
public async Task OnRefresh_WhenRepositoryThrows_ShowsError()
{
    _platform.CustomerRepository.GetAllAsyncShouldThrow = true;

    _presenter.Dispatcher.Dispatch(CommonActions.Refresh);
    await Task.Yield();

    Assert.True(_platform.MessageService.ErrorMessageShown);
}
```

`async void` ハンドラを直接 await できないので、テスト容易にするには内部で `Task` を公開するヘルパーを用意するか、業務メソッドを `internal` にしてテストアセンブリから呼ぶ等の工夫が必要です。

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — `CanExecute` で実行状態を制御
- [HowTo: エラー処理戦略](HowTo-Handle-Errors) — async での例外処理
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — async テスト
- [EventAggregator](Reference-EventAggregator) — バックグラウンドからの UI 通知 (自動マーシャリング)
- サンプル:
  - `samples/WinformsMVP.Samples/AsyncDemo/` — 完全な async/Progress/Cancellation 実装
