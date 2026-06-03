# HowTo: エラー処理戦略

このページでは、Presenter におけるエラー処理の **4 つのレイヤー** と、典型的なシナリオごとの実装パターンを示します。
原則は単純です: **ユーザーには分かりやすく見せ、内部にはログを残し、Presenter から WinForms 型を露出させない**。

---

## 目次

- [4 つのレイヤー](#4-つのレイヤー)
- [レイヤー 1: `IMessageService` でのユーザー通知](#レイヤー-1-imessageservice-でのユーザー通知)
- [レイヤー 2: `InteractionResult<T>` で失敗を畳む](#レイヤー-2-interactionresultt-で失敗を畳む)
- [レイヤー 3: `DialogDefaults` でメッセージを集約](#レイヤー-3-dialogdefaults-でメッセージを集約)
- [レイヤー 4: グローバル例外ハンドラ](#レイヤー-4-グローバル例外ハンドラ)
- [シナリオ別レシピ](#シナリオ別レシピ)
  - [バリデーションエラー](#バリデーションエラー)
  - [DB/ネットワーク操作](#dbネットワーク操作)
  - [ファイル操作](#ファイル操作)
  - [長時間処理の途中エラー](#長時間処理の途中エラー)
- [エラー処理のベストプラクティス](#エラー処理のベストプラクティス)
- [関連ページ](#関連ページ)

---

## 4 つのレイヤー

| レイヤー | 担当 | 主な API |
|---------|------|--------|
| 1. ユーザー通知 | エラーを画面に表示 | `IMessageService` |
| 2. 操作の戻り値 | 成功・キャンセル・失敗を畳んで返す | `InteractionResult<T>` |
| 3. メッセージの集約 | 共通エラー文言を 1 箇所で管理 | `DialogDefaults` |
| 4. 最終防衛線 | 想定外の未捕捉例外を拾う | `Application.ThreadException` 等 |

---

## レイヤー 1: `IMessageService` でのユーザー通知

Presenter は **必ず** `Messages` プロパティ越しに `IMessageService` を使います。`MessageBox.Show()` を直接呼ぶのは [鉄則 2](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) 違反。

### 基本パターン

```csharp
private void OnSave()
{
    try
    {
        ValidateInput();
        SaveData();
        Messages.ShowInfo("Saved successfully!", "Success");
    }
    catch (ValidationException ex)
    {
        Messages.ShowWarning(ex.Message, "Validation Failed");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to save: {ex.Message}", "Error");
        Logger.LogError(ex, "Save operation failed");
    }
}
```

### 確認ダイアログを破壊的操作の前に

```csharp
private void OnDelete()
{
    if (!Messages.ConfirmYesNo("Are you sure you want to delete this item?", "Confirm Delete"))
        return;       // ユーザーがキャンセル — 何もしない

    try
    {
        DeleteItem();
        Messages.ShowInfo("Item deleted.", "Success");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to delete: {ex.Message}", "Error");
        Logger.LogError(ex, "Delete operation failed");
    }
}
```

### 利用可能なメソッド

| メソッド | 用途 |
|---------|----|
| `Messages.ShowInfo(text, caption)` | 情報通知 |
| `Messages.ShowWarning(text, caption)` | 警告 (フロー継続可能) |
| `Messages.ShowError(text, caption)` | エラー (フロー停止) |
| `Messages.ConfirmYesNo(text, caption)` | Yes/No 確認 (戻り値 `bool`) |
| `Messages.ConfirmYesNoCancel(text, caption)` | Yes/No/Cancel (戻り値 `ConfirmResult`) |
| `Messages.ShowToast(text, type, duration)` | 非ブロッキング通知 (実装が対応する場合) |

詳細は [Platform Services#imessageservice](Reference-Platform-Services#imessageservice--メッセージダイアログ)。

---

## レイヤー 2: `InteractionResult<T>` で失敗を畳む

ファイル選択・ウィンドウ表示等、**3 つの結果** (成功・キャンセル・失敗) を持つ操作には例外ベースの制御フローを避け、`InteractionResult<T>` で明示的に分岐します。

### ファイル操作の典型例

```csharp
private void OnOpenFile()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
        Title = "Select a file to open",
    });

    if (result.IsOk)
    {
        LoadFile(result.Value);
    }
    else if (result.IsCancelled)
    {
        // ユーザーが Cancel — 静かに何もしない
    }
    else if (result.IsError)
    {
        Messages.ShowError(result.ErrorMessage, "Error");
    }
}
```

### カスタム操作で `InteractionResult<T>` を返す

業務側でも `InteractionResult<T>` を使うと、呼び出し側が分岐しやすくなります。

```csharp
public InteractionResult<Customer> LoadCustomer(int customerId)
{
    try
    {
        var customer = _repository.GetById(customerId);
        if (customer == null)
            return InteractionResult<Customer>.Error("Customer not found");

        return InteractionResult<Customer>.Ok(customer);
    }
    catch (Exception ex)
    {
        return InteractionResult<Customer>.Error("Failed to load customer", ex);
    }
}

// 呼び出し側
private void OnEditCustomer(int id)
{
    var result = LoadCustomer(id);
    if (result.IsOk)
        View.Customer = result.Value;
    else
        Messages.ShowError(result.ErrorMessage, "Error");
}
```

### メリット

✅ 例外ベースの制御フローを避けられる
✅ 成功・キャンセル・失敗が明示的
✅ 例外オブジェクトも保持できる (必要なら)
✅ チェイン・組み合わせが容易

---

## レイヤー 3: `DialogDefaults` でメッセージを集約

共通のエラーメッセージを `DialogDefaults` でアプリ起動時に設定すると、全画面で統一感が出ます。

```csharp
// Program.cs
DialogDefaults.FileOpenErrorMessage    = "ファイルを開くに失敗しました";
DialogDefaults.FileSaveErrorMessage    = "ファイルの保存に失敗しました";
DialogDefaults.FolderBrowseErrorMessage = "フォルダの選択に失敗しました";
DialogDefaults.DefaultErrorCaption      = "エラー";
```

### メリット

✅ メッセージの一貫性
✅ ローカライズが容易
✅ 修正が 1 箇所

---

## レイヤー 4: グローバル例外ハンドラ

未捕捉例外をアプリ全体で拾う最後の防衛線。**本番アプリでは推奨**、開発中は無効化して例外を即座に確認するのもアリ。

### `Program.cs` での設定

```csharp
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        => HandleUnhandledException(e.Exception);

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            HandleUnhandledException(ex);
    }

    private static void HandleUnhandledException(Exception ex)
    {
        // 例外をログに記録
        PlatformServices.Default.LoggerFactory
            .CreateLogger("UnhandledException")
            .LogCritical(ex, "Unhandled exception");

        // ユーザー向けメッセージ
        PlatformServices.Default.MessageService.ShowError(
            $"An unexpected error occurred:\n\n{ex.Message}\n\nThe application may be in an unstable state.",
            "Unhandled Error");

        // 必要なら終了
        // Application.Exit();
    }
}
```

### 注意点

- 例外を全部捕まえるとバグが隠れる → 開発中はオフ
- リリースビルドだけ有効にする手もある (`#if !DEBUG`)
- ロギングフレームワーク (Application Insights / Seq 等) と組み合わせて使う

---

## シナリオ別レシピ

### バリデーションエラー

早期に検出して、業務処理を呼ぶ前に止める。

```csharp
private void OnSave()
{
    var validationErrors = ValidateInput();
    if (validationErrors.Count > 0)
    {
        var errorMessage = string.Join("\n", validationErrors);
        Messages.ShowWarning(errorMessage, "Validation Failed");
        return;        // 保存処理に進まない
    }

    try
    {
        SaveData();
        Messages.ShowInfo("Saved!", "Success");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to save: {ex.Message}", "Error");
        Logger.LogError(ex, "Save failed");
    }
}
```

詳細は [HowTo: フォーム入力を検証する](HowTo-Validate-Form-Input) (作成予定) も参照。

### DB/ネットワーク操作

エラーの種類別に分岐し、ユーザーに具体的な対処法を伝える。

```csharp
private async void OnRefreshData()
{
    View.ShowLoadingIndicator(true);

    try
    {
        var data = await _repository.GetDataAsync();
        View.Data = data;
    }
    catch (TimeoutException)
    {
        Messages.ShowError("The operation timed out. Please check your network connection.", "Timeout");
    }
    catch (UnauthorizedAccessException)
    {
        Messages.ShowError("You don't have permission to access this data.", "Access Denied");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to load data: {ex.Message}", "Error");
        Logger.LogError(ex, "RefreshData failed");
    }
    finally
    {
        View.ShowLoadingIndicator(false);
    }
}
```

ポイント:

- 具体的な例外を **先に** catch (`UnauthorizedAccessException` → `Exception`)
- `finally` で UI 状態を必ず戻す (Loading インジケータ等)
- ロギングで詳細を残す (ユーザーに見せるメッセージとは別)

### ファイル操作

`InteractionResult<T>` で分岐し、I/O エラーは個別に catch。

```csharp
private void OnExport()
{
    var fileResult = Dialogs.ShowSaveFileDialog(new SaveFileDialogOptions
    {
        Filter = "CSV files (*.csv)|*.csv",
        DefaultExt = "csv",
        Title = "Export Data",
    });

    if (fileResult.IsCancelled) return;
    if (fileResult.IsError)
    {
        Messages.ShowError(fileResult.ErrorMessage, "Error");
        return;
    }

    try
    {
        ExportToCsv(fileResult.Value);
        Messages.ShowInfo(
            $"Data exported to {Path.GetFileName(fileResult.Value)}",
            "Success");
    }
    catch (IOException ex)
    {
        Messages.ShowError($"Failed to write file: {ex.Message}", "Export Failed");
        Logger.LogError(ex, "Export to {Path} failed", fileResult.Value);
    }
}
```

### 長時間処理の途中エラー

ループ内のエラーは catch して継続、最後にサマリを表示。

```csharp
private async void OnProcessBatch()
{
    if (View.SelectedItems.Count == 0)
    {
        Messages.ShowWarning("Please select items to process.", "No Selection");
        return;
    }

    View.ShowProgress(true);
    int success = 0, error = 0;

    foreach (var item in View.SelectedItems)
    {
        try
        {
            await ProcessItemAsync(item);
            success++;
            View.UpdateProgress(success, View.SelectedItems.Count);
        }
        catch (Exception ex)
        {
            error++;
            Logger.LogError(ex, "Failed to process item {ItemId}", item.Id);
            // ユーザーへの即時通知はしない — サマリで一括表示
        }
    }

    View.ShowProgress(false);

    var summary = $"Processing complete:\n\nSuccess: {success}\nFailed: {error}";
    if (error > 0)
        Messages.ShowWarning(summary, "Processing Complete");
    else
        Messages.ShowInfo(summary, "Success");
}
```

---

## エラー処理のベストプラクティス

### 1. `MessageBox.Show()` を Presenter で絶対に呼ばない

```csharp
// ❌ NG
MessageBox.Show("Error!");

// ✅ OK
Messages.ShowError("Error!");
```

### 2. `InteractionResult<T>` を例外より優先する

例外は本当に「想定外」な場合だけ。「ユーザーがキャンセル」は例外ではなく `InteractionResult<T>.Cancel()`。

### 3. 早期検証・早期リターン

```csharp
if (string.IsNullOrEmpty(View.UserName))
{
    Messages.ShowWarning("Name is required", "Validation");
    return;
}
```

### 4. 具体的な例外を先に catch

```csharp
try { /* ... */ }
catch (TimeoutException)        { /* タイムアウト固有 */ }
catch (UnauthorizedAccessException) { /* 認可固有 */ }
catch (Exception ex)            { /* 汎用 */ }
```

### 5. ユーザー向けメッセージと内部ログを分ける

```csharp
catch (Exception ex)
{
    // ユーザー向け — 簡潔・対処法を含める
    Messages.ShowError("Failed to save data. Please try again.", "Error");

    // 内部 — スタックトレース・コンテキストを残す
    Logger.LogError(ex, "Save failed for user {UserId}", _userId);
}
```

### 6. 黙ってエラーを飲み込まない

```csharp
// ❌ Bad — 何が起きたか誰も知らない
try { SaveData(); } catch { }

// ✅ OK — 最低限ログには残す
try { SaveData(); }
catch (Exception ex) { Logger.LogError(ex, "Save failed silently"); }
```

### 7. エラーパスもテストする

```csharp
[Fact]
public void OnSave_WhenExceptionThrown_ShowsError()
{
    _platform.UserRepository.SaveShouldThrow = true;

    _presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.True(_platform.MessageService.ErrorMessageShown);
    Assert.Contains("Failed to save", _platform.MessageService.LastErrorMessage);
}
```

詳細は [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) 参照。

### 8. ユーザーフレンドリーなメッセージ

スタックトレースを直接見せない。技術用語を避ける。

```csharp
// ❌ Bad
Messages.ShowError(ex.ToString());

// ✅ OK
Messages.ShowError($"Failed to load data: {ex.Message}", "Error");
Logger.LogError(ex, "Data load failed");   // 詳細はログに
```

---

## 関連ページ

- [Platform Services](Reference-Platform-Services) — `IMessageService` / `IDialogProvider` / `InteractionResult<T>`
- [Logging](Reference-Logging) — エラーログの構造化
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — エラーパスのテスト
- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 2 (Presenter から UI 型を排除)
