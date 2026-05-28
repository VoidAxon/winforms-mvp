# HowTo: フォーム入力を検証する

このページでは、Presenter でフォーム入力の検証 (バリデーション) を実装するパターンを示します。
原則:**検証ロジックは Presenter (または Model) に置き、View はエラー表示の "見せ方" だけを持つ**。

---

## 目次

- [基本パターン](#基本パターン)
- [シナリオ 1: フィールド単位の検証](#シナリオ-1-フィールド単位の検証)
- [シナリオ 2: クロスフィールド検証](#シナリオ-2-クロスフィールド検証)
- [シナリオ 3: パターン照合 (Email / 電話番号)](#シナリオ-3-パターン照合-email--電話番号)
- [シナリオ 4: ビジネスルール検証](#シナリオ-4-ビジネスルール検証)
- [シナリオ 5: リアルタイム検証](#シナリオ-5-リアルタイム検証)
- [Save ボタンの有効/無効を CanExecute で制御](#save-ボタンの有効無効を-canexecute-で制御)
- [視覚的なエラー表示](#視覚的なエラー表示)
- [テストパターン](#テストパターン)
- [関連ページ](#関連ページ)

---

## 基本パターン

検証は **Presenter 側で実行し、結果を View に渡す**。View は受け取ったエラーをどう表示するか (赤枠・ラベル・ツールチップ等) を自分で決めます。

```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    string Phone { get; set; }

    bool IsValid { get; }                                  // 検証結果フラグ
    void ShowValidationErrors(IReadOnlyList<string> errors);   // 一覧表示
    void ShowFieldError(string fieldName, string error);   // 個別フィールド表示
    void ClearFieldErrors();

    ViewActionBinder ActionBinder { get; }
    event EventHandler InputChanged;
}
```

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void OnViewAttached()
    {
        View.InputChanged += (s, e) => Validate();
    }

    private bool Validate()
    {
        View.ClearFieldErrors();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(View.UserName))
        {
            errors.Add("Name is required.");
            View.ShowFieldError(nameof(View.UserName), "Required");
        }

        if (!IsValidEmail(View.Email))
        {
            errors.Add("Email is invalid.");
            View.ShowFieldError(nameof(View.Email), "Invalid email format");
        }

        View.ShowValidationErrors(errors);
        return errors.Count == 0;
    }
}
```

---

## シナリオ 1: フィールド単位の検証

```csharp
private bool ValidateUserName()
{
    if (string.IsNullOrWhiteSpace(View.UserName))
    {
        View.ShowFieldError(nameof(View.UserName), "Name is required.");
        return false;
    }

    if (View.UserName.Length < 2)
    {
        View.ShowFieldError(nameof(View.UserName), "Name must be at least 2 characters.");
        return false;
    }

    if (View.UserName.Length > 50)
    {
        View.ShowFieldError(nameof(View.UserName), "Name must be 50 characters or fewer.");
        return false;
    }

    return true;
}
```

---

## シナリオ 2: クロスフィールド検証

「パスワード と 確認パスワードが一致するか」のような複数フィールド横断の検証。

```csharp
private bool ValidatePasswordPair()
{
    if (View.Password != View.ConfirmPassword)
    {
        View.ShowFieldError(nameof(View.ConfirmPassword), "Passwords do not match.");
        return false;
    }
    return true;
}

private bool ValidateDateRange()
{
    if (View.StartDate >= View.EndDate)
    {
        View.ShowFieldError(nameof(View.EndDate), "End date must be after start date.");
        return false;
    }
    return true;
}
```

---

## シナリオ 3: パターン照合 (Email / 電話番号)

正規表現はヘルパークラスに切り出して再利用可能に。

```csharp
internal static class ValidationHelper
{
    private static readonly Regex EmailRegex = new Regex(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex JpPhoneRegex = new Regex(
        @"^0\d{1,4}-\d{1,4}-\d{4}$",
        RegexOptions.Compiled);

    public static bool IsValidEmail(string value)
        => !string.IsNullOrEmpty(value) && EmailRegex.IsMatch(value);

    public static bool IsValidJpPhone(string value)
        => !string.IsNullOrEmpty(value) && JpPhoneRegex.IsMatch(value);
}
```

```csharp
private bool ValidateEmail()
{
    if (!ValidationHelper.IsValidEmail(View.Email))
    {
        View.ShowFieldError(nameof(View.Email), "Invalid email format.");
        return false;
    }
    return true;
}
```

---

## シナリオ 4: ビジネスルール検証

「年齢が 18 歳以上」「在庫数が下限を下回らない」等のドメインルール。

```csharp
private bool ValidateAge()
{
    var age = CalculateAge(View.BirthDate, DateTime.Today);
    if (age < 18)
    {
        View.ShowFieldError(nameof(View.BirthDate),
            "Users under 18 cannot register.");
        return false;
    }
    return true;
}

private bool ValidateStock()
{
    if (View.Quantity > _product.AvailableStock)
    {
        View.ShowFieldError(nameof(View.Quantity),
            $"Only {_product.AvailableStock} items available.");
        return false;
    }
    return true;
}
```

---

## シナリオ 5: リアルタイム検証

入力中に検証して即座にフィードバック。View の `InputChanged` イベントを Presenter で購読します。

```csharp
public interface IUserEditorView : IWindowView
{
    // ... フィールドプロパティ
    event EventHandler InputChanged;
}

public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void OnViewAttached()
    {
        View.InputChanged += OnInputChanged;
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        Validate();
        Dispatcher.RaiseCanExecuteChanged();   // Save ボタンの有効/無効を更新
    }

    private bool Validate()
    {
        // ... 全フィールドを検証して View に伝える
    }
}
```

View 側は各 TextBox 等の `TextChanged` を共通の `InputChanged` イベントに集約します。

```csharp
// Form 側
private void InitializeComponent()
{
    // ...
    _nameTextBox.TextChanged   += RaiseInputChanged;
    _emailTextBox.TextChanged  += RaiseInputChanged;
    _phoneTextBox.TextChanged  += RaiseInputChanged;
}

private void RaiseInputChanged(object sender, EventArgs e)
    => InputChanged?.Invoke(this, EventArgs.Empty);

public event EventHandler InputChanged;
```

---

## Save ボタンの有効/無効を CanExecute で制御

検証結果に応じて Save ボタンを自動的に有効化/無効化:

```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(CommonActions.Save, OnSave,
        canExecute: () => Validate());          // ← 検証が通っているときだけ Enabled
}
```

毎回の `Validate()` 呼び出しを最小化するには、結果をキャッシュ:

```csharp
private bool _isValidCached;

private void OnInputChanged(object sender, EventArgs e)
{
    _isValidCached = Validate();
    Dispatcher.RaiseCanExecuteChanged();
}

protected override void RegisterViewActions()
{
    Dispatcher.Register(CommonActions.Save, OnSave,
        canExecute: () => _isValidCached);
}
```

---

## 視覚的なエラー表示

Form 実装で、エラーを赤枠やラベル等で示します。**View 内部のディテールなので Presenter は関与しない**。

```csharp
public partial class UserEditorForm : Form, IUserEditorView
{
    public void ShowFieldError(string fieldName, string error)
    {
        var textBox = GetTextBoxFor(fieldName);
        if (textBox == null) return;

        textBox.BackColor = Color.LightPink;     // 赤背景
        _errorProvider.SetError(textBox, error); // ErrorProvider 利用
    }

    public void ClearFieldErrors()
    {
        foreach (var tb in new[] { _nameTextBox, _emailTextBox, _phoneTextBox })
        {
            tb.BackColor = SystemColors.Window;
            _errorProvider.SetError(tb, "");
        }
    }

    public void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        _summaryLabel.Visible = errors.Count > 0;
        _summaryLabel.Text = errors.Count > 0
            ? "• " + string.Join("\n• ", errors)
            : "";
    }
}
```

`ErrorProvider` は WinForms の標準コンポーネントで、TextBox の横に赤いアイコン + ツールチップを出してくれます。

---

## テストパターン

```csharp
[Fact]
public void Save_WithInvalidEmail_ShowsFieldError()
{
    _view.UserName = "John";
    _view.Email    = "not-an-email";
    _view.RaiseInputChanged();

    _presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.Contains("Invalid email", _view.FieldErrors[nameof(_view.Email)]);
    Assert.False(_view.SavedCalled);
}

[Fact]
public void Save_WithAllValid_PersistsData()
{
    _view.UserName = "John";
    _view.Email    = "john@example.com";
    _view.Phone    = "090-1234-5678";
    _view.RaiseInputChanged();

    _presenter.Dispatcher.Dispatch(CommonActions.Save);

    Assert.True(_view.SavedCalled);
    Assert.True(_platform.MessageService.InfoMessageShown);
}

[Fact]
public void CanExecute_BecomesFalse_WhenAnyFieldInvalid()
{
    _view.UserName = "";    // 不正
    _view.RaiseInputChanged();

    Assert.False(_presenter.Dispatcher.CanExecute(CommonActions.Save));
}
```

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — `CanExecute` の使い方
- [ChangeTracker](Reference-ChangeTracker) — 「未保存の変更」と「妥当性」を組み合わせる
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テストパターン詳細
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `View.InputChanged` 等のイベント購読
- サンプル:
  - `src/WinformsMVP.Samples/ValidationDemo/` — 完全な検証実装
