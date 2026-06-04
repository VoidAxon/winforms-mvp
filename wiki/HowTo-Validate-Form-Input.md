# HowTo: フォーム入力を検証する

このページでは、フォーム入力の検証 (バリデーション) を実装するパターンを示します。
原則: **検証ロジックは Model または Presenter に置き、View はエラー表示の "見せ方" だけを持つ**。

検証の書き方は 2 通りあります:

- **手書きパターン** — Presenter 内に `if` チェーンで書く。シンプルで読みやすいが、フィールドが増えると冗長になる
- **ModelValidator パターン** (推奨) — Model に `[Required]` 等の DataAnnotations 属性を付け、`IModelValidator` で一括検証。再利用性とテスト性が高い

どちらも MVP 違反ではありません。**規模と再利用性で選んでください**。同じプロジェクト内で混在させても問題ありません (例: 単純な検索フォームは手書き、ユーザー編集のような複雑なフォームは ModelValidator)。

---

## 目次

- [基本パターン: 手書き](#基本パターン-手書き)
- [推奨パターン: ModelValidator (DataAnnotations 連携)](#推奨パターン-modelvalidator-dataannotations-連携)
- [`[ValidationOrder]` でフィールド順序を制御](#validationorder-でフィールド順序を制御)
- [ChangeTracker と組み合わせる](#changetracker-と組み合わせる)
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

## 基本パターン: 手書き

最もシンプルな書き方。Presenter で検証を実行し、エラーを View に渡します。

```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    string Phone { get; set; }

    bool IsValid { get; }                                      // 検証結果フラグ
    void ShowValidationErrors(IReadOnlyList<string> errors);   // 一覧表示
    void ShowFieldError(string fieldName, string error);       // 個別フィールド表示
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

このパターンはフィールドが少なく、検証ロジックがシンプルなとき有効です。次節の ModelValidator パターンでは同じことを宣言的・再利用可能な形で書けます。

---

## 推奨パターン: ModelValidator (DataAnnotations 連携)

フレームワークは標準 `System.ComponentModel.DataAnnotations` と統合した検証フレームワーク (`WinformsMVP.Common.Validation` 名前空間) を提供します。**Model に検証ルールを宣言** し、**Presenter は `IModelValidator` を通じて検証** する設計です。

### Model に DataAnnotations を付ける

```csharp
using System.ComponentModel.DataAnnotations;
using WinformsMVP.Common.Validation.Attributes;

public class UserModel
{
    [ValidationOrder(1)]
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(50, MinimumLength = 2,
        ErrorMessage = "Name must be 2-50 characters.")]
    public string UserName { get; set; }

    [ValidationOrder(2)]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }

    [ValidationOrder(3)]
    [RegularExpression(@"^0\d{1,4}-\d{1,4}-\d{4}$",
        ErrorMessage = "Invalid Japanese phone format.")]
    public string Phone { get; set; }
}
```

ここで使えるのは標準 `DataAnnotations` 属性 (`Required`, `StringLength`, `EmailAddress`, `RegularExpression`, `Range`, etc.) すべてと、フレームワーク独自の `ValidationOrderAttribute` です。

### Presenter で IModelValidator を使う

```csharp
using System.Linq;
using WinformsMVP.Common.Validation.Core;

public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();

    private void OnSave()
    {
        var model = View.GetModel();
        var errors = _validator.ValidateAll(model);

        if (errors.Any())
        {
            View.ClearFieldErrors();
            foreach (var err in errors)
            {
                var field = err.MemberNames.FirstOrDefault();
                if (!string.IsNullOrEmpty(field))
                    View.ShowFieldError(field, err.ErrorMessage);
            }
            View.ShowValidationErrors(errors.Select(e => e.ErrorMessage).ToList());
            return;
        }

        SaveUser(model);
        Messages.ShowInfo("Saved.");
    }
}
```

### ValidateAll vs ValidateSequential

`IModelValidator<T>` には 3 つのメソッドがあり、用途で使い分けます。

| メソッド | 戻り値 | 使いどころ |
|---|---|---|
| `ValidateAll(model)` | `ReadOnlyCollection<ModelValidationResult>` (全エラー) | 保存ボタン押下時の最終チェック (全エラーを一覧表示) |
| `ValidateSequential(model)` | `ModelValidationResult` (最初の 1 件のみ) | リアルタイム検証 (`[ValidationOrder]` 順、最初のエラーで停止 — 高速 + UX 良) |
| `IsValid(model)` | `bool` | `CanExecute` 述語向け (内部で `ValidateSequential` 使用) |

> 💡 **パフォーマンス**: `ModelValidator.For<T>()` は **型ごとに singleton** です。リフレクションキャッシュにより、初回 ~1-2ms / 2 回目以降 ~0.1-0.5ms で動作します。Presenter のフィールド (`private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();`) として保持して問題ありません。

### IValidatableObject によるクラスレベル検証

複数フィールドにまたがる検証 (例: パスワードと確認パスワードが一致するか) は `IValidatableObject` インターフェイスを実装します。

```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class RegistrationModel : IValidatableObject
{
    [Required] public string Password { get; set; }
    [Required] public string ConfirmPassword { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Password != ConfirmPassword)
            yield return new ValidationResult(
                "Passwords do not match.",
                new[] { nameof(ConfirmPassword) });
    }
}
```

> ⚠️ **検証の「失敗 1 件」を表す型が 2 つあります。場面で使い分けてください。**
>
> | 場面 | 使う型 | 特徴 |
> |---|---|---|
> | `IValidatableObject.Validate` の戻り値 (上の例) | **`System.ComponentModel.DataAnnotations.ValidationResult`** (BCL) | `IsValid` プロパティは **ない**。成功は `ValidationResult.Success` (= `null`) を返す/何も `yield` しない |
> | `IModelValidator<T>.ValidateAll` / `ValidateSequential` の戻り値 (前節) | **`WinformsMVP.Common.Validation.Core.ModelValidationResult`** (フレームワーク) | `IsValid` プロパティが **ある** |
>
> フレームワーク型は `ModelValidationResult` に改名され、BCL の `ValidationResult` と **名前衝突しなくなりました**。それでも役割が違うので取り違えに注意:`IValidatableObject.Validate` を実装するファイルでは **`using System.ComponentModel.DataAnnotations;`** を入れ、戻り値は BCL の `ValidationResult` を `yield` してください (フレームワークの `ModelValidationResult` を返すと契約を満たさずコンパイルエラー)。

`Validator.TryValidateObject` が `IValidatableObject.Validate` を自動的に呼ぶため、`_validator.ValidateAll(model)` だけで属性ベース検証 + クロスフィールド検証の両方が走ります。

---

## `[ValidationOrder]` でフィールド順序を制御

`[ValidationOrder(N)]` をプロパティに付けると、検証の評価順序を制御できます。`ValidateSequential` と組み合わせると **最初のエラーで停止** するため、UX とパフォーマンスの両方が改善します。

```csharp
public class UserModel
{
    [ValidationOrder(1)]
    [Required]
    public string UserName { get; set; }

    [ValidationOrder(2)]
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [ValidationOrder(3)]
    [Range(0, 200)]
    public int Age { get; set; }
}
```

検証順:

1. `UserName.Required`
2. `Email.Required` → `Email.EmailAddress` (同プロパティ内は宣言順)
3. `Age.Range`

### なぜ順序が重要か

順序を制御することで、**意味のないカスケードエラーを防げます**。たとえば「Email が空」のときに「Email 形式が不正」と二重に出さない、というケース:

| 状況 | `[ValidationOrder]` なし + `ValidateAll` | `[ValidationOrder]` あり + `ValidateSequential` |
|---|---|---|
| Email が空 | 「Email is required」と「Invalid email format」を両方出す (混乱) | 「Email is required」だけ (明快) |
| Age が範囲外、Email も無効 | 全部表示 (網羅的だが冗長) | Order 順で最重要のものだけ |

`ValidateAll` は最終チェック (Save 押下時) で全エラーを出すために使い、`ValidateSequential` はリアルタイムフィードバックで「次に直すべき 1 件」を示すために使う、と使い分けます。

### 順序を指定しない場合

`[ValidationOrder]` を付けないプロパティは `Order = 0` 扱いです。同じ Order 内では宣言順 (リフレクションで読まれた順) で評価されます。**明示的な制御が必要なフィールドだけ** 属性を付けてください。

---

## ChangeTracker と検証を組み合わせる

[ChangeTracker](Reference-ChangeTracker) (変更追跡) と `IModelValidator<T>` (検証) は **独立した別々の関心事**です。フレームワークは両者を融合するヘルパーを **あえて提供しません**。`ChangeTracker` は検証を一切知らないまま保ち、合成は **Presenter がユースケースとして明示的に編成**します ([単一責任](Concept-MVP-Pattern))。「検証 → 通れば確定 → 保存」という流れが Presenter のハンドラにそのまま見えるのが正しい形です。

### 典型パターン: 保存時の検証 + CanExecute 連動

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private ChangeTracker<UserModel> _tracker;
    private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();

    protected override void OnInitialize()
    {
        _tracker = new ChangeTracker<UserModel>(LoadUser(_userId));
        View.Bind(_tracker.CurrentValue);

        // 編集のたびに CanExecute を再評価させる。canExecute 述語は「RaiseCanExecuteChanged が
        // 呼ばれたとき」だけ再計算されるので、これが無いと Save ボタンの活性状態が更新されない。
        // (CurrentValue はデータバインドのため INotifyPropertyChanged。その変更通知に乗る)
        ((INotifyPropertyChanged)_tracker.CurrentValue).PropertyChanged +=
            (s, e) => Dispatcher.RaiseCanExecuteChanged();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            CommonActions.Save,
            OnSave,
            // 2 つのプリミティブを述語で組み合わせるだけ:「変更がある」かつ「妥当」のとき Save 可能。
            // CurrentValue をその場編集するため IsChangedWith で実時比較する
            // (理由は [ChangeTracker](Reference-ChangeTracker) の「パフォーマンス最適化」の注記を参照)。
            canExecute: () => _tracker.IsChangedWith(_tracker.CurrentValue)
                           && _validator.IsValid(_tracker.CurrentValue));
    }

    private void OnSave()
    {
        var model = _tracker.CurrentValue;

        // 1. 検証
        var errors = _validator.ValidateAll(model);
        if (errors.Any())
        {
            View.ShowValidationErrors(errors.Select(e => e.ErrorMessage).ToList());
            return;   // tracker は dirty のまま
        }

        // 2. 通ったら確定して保存
        _tracker.AcceptChanges();
        SaveUser(model);
        Messages.ShowInfo("Saved.");
    }
}
```

> **CanExecute を連動させるには再評価のトリガが要ります。** `canExecute` 述語は登録時と `RaiseCanExecuteChanged()` 呼び出し時にしか再計算されません ([アクション外で状態が変わったら呼ぶ](Reference-ViewAction-System))。上記は編集通知 (`PropertyChanged`) に乗せて呼んでいます。
>
> もう 1 つの定番は **キャッシュ `IsChanged` を活かす**形です:View 変更時に `_tracker.UpdateCurrentValue(viewの値)` を呼ぶと、キャッシュが更新され `IsChangedChanged` が発火するので、そのハンドラで `RaiseCanExecuteChanged()` を呼ぶ。実例は `samples/.../EmailDemo/ComposeEmailPresenter.cs`。

### 入力が無効になったら巻き戻す

「入力中に妥当でなくなったら元の値に戻す」UX も、同じく Presenter が明示的に編成します。`RejectChanges()` は新しい現在値を作り直すので、戻したあとは View を再バインドします。

```csharp
private void OnInputChanged(object sender, EventArgs e)
{
    if (!_validator.IsValid(_tracker.CurrentValue))
    {
        _tracker.RejectChanges();
        View.Bind(_tracker.CurrentValue);   // 巻き戻した値を再バインド
        Messages.ShowWarning("Invalid input — reverted to last valid value.");
    }
}
```

検証と変更追跡をそれぞれ単体でテストでき、依存も増えません。

---

## シナリオ 1: フィールド単位の検証

### 手書き

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

### DataAnnotations

```csharp
public class UserModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(50, MinimumLength = 2,
        ErrorMessage = "Name must be 2-50 characters.")]
    public string UserName { get; set; }
}
```

```csharp
// Presenter
var result = _validator.ValidateSequential(model);
if (!result.IsValid)
    View.ShowFieldError(result.MemberNames.First(), result.ErrorMessage);
```

---

## シナリオ 2: クロスフィールド検証

「パスワードと確認パスワードが一致するか」のような複数フィールド横断の検証。

### 手書き

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

### IValidatableObject

Model 側に検証ロジックを置けるため、Presenter の負担が減ります。

```csharp
public class RegistrationModel : IValidatableObject
{
    [Required] public string Password { get; set; }
    [Required] public string ConfirmPassword { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Password != ConfirmPassword)
            yield return new ValidationResult(
                "Passwords do not match.",
                new[] { nameof(ConfirmPassword) });

        if (StartDate >= EndDate)
            yield return new ValidationResult(
                "End date must be after start date.",
                new[] { nameof(EndDate) });
    }
}
```

`_validator.ValidateAll(model)` を呼ぶだけで `IValidatableObject.Validate` が自動的に実行されます。

---

## シナリオ 3: パターン照合 (Email / 電話番号)

### 手書き (正規表現ヘルパー)

```csharp
internal static class RegexHelpers
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
    if (!RegexHelpers.IsValidEmail(View.Email))
    {
        View.ShowFieldError(nameof(View.Email), "Invalid email format.");
        return false;
    }
    return true;
}
```

### DataAnnotations

```csharp
public class ContactModel
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; }

    [RegularExpression(@"^0\d{1,4}-\d{1,4}-\d{4}$",
        ErrorMessage = "Invalid Japanese phone format.")]
    public string Phone { get; set; }
}
```

`[EmailAddress]` は標準で広く使われる Email 形式チェックを内蔵します。複雑なドメインルールが必要な場合だけカスタム正規表現を `[RegularExpression]` で書きます。

---

## シナリオ 4: ビジネスルール検証

「年齢が 18 歳以上」「在庫数が下限を下回らない」等のドメインルール。

### 手書き

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

### カスタム ValidationAttribute

ドメインルールを再利用したい場合は、`ValidationAttribute` を継承したカスタム属性を作れます。

```csharp
public class MinimumAgeAttribute : ValidationAttribute
{
    public int MinimumAge { get; }

    public MinimumAgeAttribute(int minimumAge)
    {
        MinimumAge = minimumAge;
    }

    protected override ValidationResult IsValid(object value, ValidationContext context)
    {
        if (value is DateTime birthDate)
        {
            var age = DateTime.Today.Year - birthDate.Year;
            if (birthDate > DateTime.Today.AddYears(-age)) age--;

            if (age < MinimumAge)
                return new ValidationResult(
                    $"User must be at least {MinimumAge} years old.",
                    new[] { context.MemberName });
        }
        return ValidationResult.Success;
    }
}
```

```csharp
public class UserModel
{
    [MinimumAge(18)]
    public DateTime BirthDate { get; set; }
}
```

複雑なルール (DB 参照を伴う場合等) は `IValidatableObject.Validate` または Presenter 内で個別に書いた方がよい場合もあります。**属性は宣言的に書けて再利用性が高い** が、**外部リソース (DB・サービス) に触れない** という制約があります。

---

## シナリオ 5: リアルタイム検証

入力中に検証して即座にフィードバック。View の `InputChanged` イベントを Presenter で購読します。

### 手書き

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void OnViewAttached()
    {
        View.InputChanged += OnInputChanged;
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        Validate();
        Dispatcher.RaiseCanExecuteChanged();
    }

    private bool Validate()
    {
        // ... 全フィールドを手書きで検証
    }
}
```

### ModelValidator (ValidateSequential)

`ValidateSequential` は最初のエラーで停止するため、リアルタイムフィードバックに最適です。

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();
    private ModelValidationResult _lastError = ModelValidationResult.Success;

    protected override void OnViewAttached()
    {
        View.InputChanged += OnInputChanged;
    }

    private void OnInputChanged(object sender, EventArgs e)
    {
        var model = View.GetModel();
        _lastError = _validator.ValidateSequential(model);

        View.ClearFieldErrors();
        if (!_lastError.IsValid)
        {
            var field = _lastError.MemberNames.FirstOrDefault();
            if (!string.IsNullOrEmpty(field))
                View.ShowFieldError(field, _lastError.ErrorMessage);
        }

        Dispatcher.RaiseCanExecuteChanged();   // Save ボタンの有効/無効を更新
    }
}
```

View 側は各 TextBox 等の `TextChanged` を共通の `InputChanged` イベントに集約します。

```csharp
// Form 側
private void InitializeComponent()
{
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

検証結果に応じて Save ボタンを自動的に有効化/無効化します。

### 手書き

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

### ModelValidator

`IModelValidator<T>.IsValid()` は内部で `ValidateSequential` を使うため、最初のエラーで停止します。`CanExecute` 述語に直接呼んで問題ありません。

```csharp
private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();

protected override void RegisterViewActions()
{
    Dispatcher.Register(
        CommonActions.Save,
        OnSave,
        canExecute: () => _validator.IsValid(View.GetModel()));
}

protected override void OnViewAttached()
{
    // 入力が変わったら CanExecute を再評価
    View.InputChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
}
```

ChangeTracker と組み合わせる場合は前述の [ChangeTracker と組み合わせる](#changetracker-と組み合わせる) 節を参照してください。

---

## 視覚的なエラー表示

Form 実装で、エラーを赤枠やラベル等で示します。**View 内部のディテールなので Presenter は関与しません**。

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

### Presenter のテスト

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

### Model 自体のテスト (ModelValidator パターン)

ModelValidator 経由なら、Presenter を介さずに Model 単体で検証ロジックをテストできます。

```csharp
[Fact]
public void UserModel_WithEmptyName_FailsValidation()
{
    var model = new UserModel { UserName = "", Email = "john@example.com" };
    var validator = ModelValidator.For<UserModel>();

    var errors = validator.ValidateAll(model);

    Assert.NotEmpty(errors);
    Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserModel.UserName)));
}

[Fact]
public void UserModel_ValidateSequential_StopsAtFirstError()
{
    var model = new UserModel { UserName = "", Email = "bad", Phone = "999" };
    var validator = ModelValidator.For<UserModel>();

    var error = validator.ValidateSequential(model);

    // [ValidationOrder(1)] の UserName から最初のエラーが返る
    Assert.False(error.IsValid);
    Assert.Contains(nameof(UserModel.UserName), error.MemberNames);
}
```

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — `CanExecute` の使い方
- [ChangeTracker](Reference-ChangeTracker) — 「未保存の変更」と「妥当性」を組み合わせる
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テストパターン詳細
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `View.InputChanged` 等のイベント購読
- サンプル:
  - `samples/WinformsMVP.Samples/ValidationDemo/` — `ValidationDemoPresenter` / `ValidationDemoForm` で手書きと ModelValidator の両方を扱う完全実装
