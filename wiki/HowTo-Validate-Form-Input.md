# HowTo: フォーム入力を検証する

このページでは、フォーム入力の検証 (バリデーション) とエラー表示の標準的な実装方法を示します。
原則: **検証ルールは Model (DataAnnotations) と Presenter に置き、View はエラーを「どこに・どう」見せるかだけを持つ**。

標準の流れは次のとおりです:

- **ルール** — Model の DataAnnotations (`Validation.Validate(model)` がプロパティ属性・クラスレベル属性・`IValidatableObject` をすべて評価) + Presenter のルール (`Validation.SetError(field, msg)` / `ClearError`。クロスフィールドや DB 照会など)
- **状態** — 全 Presenter が持つ `protected ValidationState Validation`。フィールド名 (= Model のプロパティ名) ごとの現在のエラー
- **表示** — View が `IValidatableView` を実装して `IValidationErrorDisplay ValidationErrors` を公開。Form では `ErrorProviderDisplay` でフィールド名をコントロールに対応付ける。接続はフレームワークが自動で行う
- **タイミング** — フィールドのエラーは、ユーザーがそのコントロールを離れた (フォーカスが外れた) 後、または送信 (`Validation.Check(model)`) の後に表示される

完全な実装例は `samples/WinformsMVP.Samples/ValidationDemo/` (`RegistrationModel` / `ValidationDemoPresenter` / `ValidationDemoForm`) です。

---

## 目次

- [推奨パターン: Validation + ErrorProviderDisplay](#推奨パターン-validation--errorproviderdisplay)
  - [1. Model にルールを書く](#1-model-にルールを書く)
  - [2. View インターフェイス: IValidatableView](#2-view-インターフェイス-ivalidatableview)
  - [3. Form: ErrorProviderDisplay でフィールドとコントロールを対応付ける](#3-form-errorproviderdisplay-でフィールドとコントロールを対応付ける)
  - [4. Presenter: Validate / Check / Reset](#4-presenter-validate--check--reset)
  - [エラーが表示されるタイミング](#エラーが表示されるタイミング)
  - [送信ボタンは無効化しない](#送信ボタンは無効化しない)
  - [Presenter のルール: SetError / ClearError](#presenter-のルール-seterror--clearerror)
  - [ValidationState API](#validationstate-api)
- [クロスフィールド検証の注意点](#クロスフィールド検証の注意点)
- [視覚的なエラー表示をカスタマイズする](#視覚的なエラー表示をカスタマイズする)
- [ChangeTracker と検証を組み合わせる](#changetracker-と検証を組み合わせる)
- [リファレンス: ModelValidator (DataAnnotations 連携)](#リファレンス-modelvalidator-dataannotations-連携)
- [`[ValidationOrder]` でフィールド順序を制御](#validationorder-でフィールド順序を制御)
- [テストパターン](#テストパターン)
- [関連ページ](#関連ページ)

---

## 推奨パターン: Validation + ErrorProviderDisplay

### 1. Model にルールを書く

ルールは Model の DataAnnotations として宣言します。標準属性 (`Required`, `StringLength`, `MinLength`, `MaxLength`, `EmailAddress`, `RegularExpression`, `Range`, `CustomValidation`, ...) とクラスレベル属性・`IValidatableObject` がすべて使えます。

```csharp
using System.ComponentModel.DataAnnotations;

public class RegistrationModel
{
    [Required(ErrorMessage = "User name is required.")]
    [MinLength(3, ErrorMessage = "User name must be at least 3 characters.")]
    public string UserName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; }

    [Required(ErrorMessage = "Password confirmation is required.")]
    [CustomValidation(typeof(RegistrationModel), nameof(MatchesPassword))]
    public string ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Age is required.")]
    [Range(18, 120, ErrorMessage = "Age must be between 18 and 120.")]
    public int? Age { get; set; }

    // [CustomValidation] のメソッドは public static。エラーは context.MemberName で返す
    public static ValidationResult MatchesPassword(string value, ValidationContext context)
    {
        var model = (RegistrationModel)context.ObjectInstance;
        return value == model.Password
            ? ValidationResult.Success
            : new ValidationResult("Passwords do not match.", new[] { context.MemberName });
    }
}
```

エラーは `ValidationResult.MemberNames` のプロパティ名の下に登録されます。メンバー名を持たないエラー (クラスレベルの規則など) は `ValidationState.FormLevel` (空文字列) の下に入ります。

> 💡 **フィールド名 = Model のプロパティ名。** Presenter の `SetError` でも Form の `Map` でも `nameof(RegistrationModel.UserName)` のように書きます。文字列リテラルは使いません。

### 2. View インターフェイス: IValidatableView

View インターフェイスに `IValidatableView` を追加するだけです。エラー表示用のメソッド (`ShowFieldError` 等) を自前で定義する必要はありません。

```csharp
public interface IRegistrationView : IWindowView, IValidatableView
{
    string UserName { get; set; }
    string Email { get; set; }
    string Password { get; set; }
    string ConfirmPassword { get; set; }
    int Age { get; set; }               // 0 = 未入力

    event EventHandler DataChanged;     // どれかの値が変わった
}
```

```csharp
// WinformsMVP.MVP.Views (WinformsMVP.Core)
public interface IValidatableView : IViewBase
{
    IValidationErrorDisplay ValidationErrors { get; }
}
```

`IValidationErrorDisplay` (`WinformsMVP.Common.Validation.Core`) は View 側のエラーの受け口で、WinForms 型を含みません。`ShowErrors(errors, revealAll)` で全エラーを受け取り、`Reset()` で「訪問済みフィールド」を忘れます。通常は自分で実装せず、WinForms パッケージの `ErrorProviderDisplay` を使います。

### 3. Form: ErrorProviderDisplay でフィールドとコントロールを対応付ける

```csharp
using WinformsMVP.Common.Validation;        // ErrorProviderDisplay / ControlErrorDisplay
using WinformsMVP.Common.Validation.Core;   // ValidationState / IValidationErrorDisplay

public partial class RegistrationForm : Form, IRegistrationView
{
    private readonly ErrorProvider _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly ErrorProviderDisplay _errors;

    public RegistrationForm()
    {
        InitializeComponent();
        Disposed += (s, e) => _errorProvider.Dispose();

        _errors = new ErrorProviderDisplay(_errorProvider)
            .Map(nameof(RegistrationModel.UserName), _userNameTextBox)
            .Map(nameof(RegistrationModel.Email), _emailTextBox)
            .Map(nameof(RegistrationModel.Password), _passwordTextBox)
            .Map(nameof(RegistrationModel.ConfirmPassword), _confirmPasswordTextBox)
            .Map(nameof(RegistrationModel.Age), _ageNumeric)
            .Map(ValidationState.FormLevel, _submitButton);   // フォーム全体 / 未マップのエラー
    }

    public IValidationErrorDisplay ValidationErrors => _errors;

    // ... UserName などのプロパティ、DataChanged、ActionBinder
}
```

- 各フィールドのエラーは、ユーザーがそのコントロールを離れた (`Leave`) 後、または送信の後に表示されます。
- `ValidationState.FormLevel` にマップしたコントロール (例: 送信ボタン) には、**メンバー名のないエラーとマップされていないフィールドのエラー** が、送信の後に表示されます。
- 同じフィールドを 2 回 `Map` すると例外です。Form にはフィールドごとのエラー処理コードも検証ロジックも書きません。

接続はフレームワークが行います。View が `IValidatableView` を実装していれば、Presenter の `Validation` の変更はすべて `ValidationErrors` に送られ、同時に全アクションの `canExecute` が再評価されます (`ActionBinder` の自動 Bind と同じ扱い)。

### 4. Presenter: Validate / Check / Reset

```csharp
public class RegistrationPresenter : WindowPresenterBase<IRegistrationView>
{
    protected override void OnViewAttached()
    {
        EventHandler onDataChanged = (s, e) => Validation.Validate(ReadModel());
        View.DataChanged += onDataChanged;
        Disposable.Create(() => View.DataChanged -= onDataChanged).DisposeWith(Disposables);
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(RegistrationActions.Submit, OnSubmit);   // canExecute で検証結果を見ない
        Dispatcher.Register(RegistrationActions.Reset, OnReset);
    }

    protected override void OnInitialize()
    {
        // 最初からエラーを持たせておく: 空の必須欄を Tab で通過しただけでも表示される
        Validation.Validate(ReadModel());
    }

    private void OnSubmit()
    {
        var model = ReadModel();
        if (!Validation.Check(model)) return;   // 全エラーを表示して中断

        Save(model);
        Messages.ShowInfo("Registered.");
    }

    private void OnReset()
    {
        View.UserName = string.Empty;
        // ... 他のフィールドもクリア

        Validation.Reset();                     // エラーと「訪問済み」を忘れる
        Validation.Validate(ReadModel());
    }

    private RegistrationModel ReadModel() => new RegistrationModel
    {
        UserName = View.UserName,
        Email = View.Email,
        Password = View.Password,
        ConfirmPassword = View.ConfirmPassword,
        Age = View.Age > 0 ? View.Age : (int?)null
    };
}
```

Presenter はコントロールにもエラー表示部品にも触れません。`Validation` は最初のアクセス時に作られ、View のアタッチ時 (またはその後の最初のアクセス時) に自動で接続されます。購読解除も `Disposables` 経由で自動です。

### エラーが表示されるタイミング

| 操作 | Presenter | 表示 |
|---|---|---|
| 入力の変更 | `Validation.Validate(model)` | ユーザーが **一度離れた** フィールドのエラーだけ |
| 送信 (Save / Submit) | `if (!Validation.Check(model)) return;` | **全フィールド** のエラー + `FormLevel` コントロールにフォーム全体 / 未マップのエラー |
| 新規 / クリア | `Validation.Reset()` (続けて `Validate`) | エラーを消去し、訪問済みフィールドを忘れる。再び「離れたフィールドだけ」に戻る |

`Check` は `Validate` + `ShowAll()` で、エラーがなければ `true` を返します。一度 `Check` すると `Reset` まで全エラー表示 (`RevealAll`) が続くので、送信後の修正はその場で反映されます。

### 送信ボタンは無効化しない

**エラーがある間も送信アクションは有効のままにしてください。** 送信ボタンを押すことが、ユーザーがまだ触れていないフィールドのエラーを見せる唯一の手段です。無効化された Save ボタンは何も教えてくれません。

```csharp
// ❌ 未訪問フィールドのエラーを見せる手段がなくなる
Dispatcher.Register(StandardActions.Save, OnSave, canExecute: () => !Validation.HasErrors);

// ✅ 送信時に Check する
Dispatcher.Register(StandardActions.Save, OnSave);
private void OnSave()
{
    if (!Validation.Check(View.GetModel())) return;
    // ...
}
```

`Validation.HasErrors` (表示済みかどうかに関係なく、どれかのルールが失敗していれば `true`) は読めますが、送信の `canExecute` には使いません。検証以外の条件 (変更があるか、処理中か等) で `canExecute` を使うのは問題ありません。`Validation` が変わるたびに `canExecute` は自動で再評価されます。

### Presenter のルール: SetError / ClearError

Model の属性で表せないルール (DB での重複チェック、外部サービス、画面状態に依存する条件) は Presenter から追加します。Model のエラーと同じフィールド名で合成され、同じタイミングで表示されます。

```csharp
private void CheckUserNameAvailable(RegistrationModel model)
{
    if (_users.Exists(model.UserName))
        Validation.SetError(nameof(RegistrationModel.UserName), $"'{model.UserName}' is already taken.");
    else
        Validation.ClearError(nameof(RegistrationModel.UserName));
}
```

- `SetError(field, msg)` はそのフィールドの Presenter ルールのエラーを置き換えます (空メッセージは `ClearError` と同じ)。
- `Validate(model)` は Model のエラーだけを置き換え、`SetError` のエラーは残します。条件が解消したら `ClearError` で消します。
- フィールドに属さないエラーは `Validation.SetError(ValidationState.FormLevel, msg)` で設定します。

### ValidationState API

`WinformsMVP.Common.Validation.Core.ValidationState` (`WinformsMVP.Core`)。Presenter では基底クラスの `Validation` プロパティで使います。

| メンバー | 説明 |
|---|---|
| `Validate<T>(model)` | Model の DataAnnotations (プロパティ・クラスレベル・`IValidatableObject`) を再評価し、Model 由来のエラーを置き換える |
| `Check<T>(model)` | `Validate` + `ShowAll`。エラーがなければ `true` (送信時に使う) |
| `SetError(field, message)` / `ClearError(field)` | Presenter ルールのエラーを設定 / 消去 |
| `ShowAll()` | 未訪問のフィールドも含めて全エラーを表示する |
| `Reset()` | 全エラーを消去し、訪問済みフィールドを忘れ、`RevealAll` を解除 |
| `HasErrors` | どれかのルールが失敗していれば `true` (表示の有無とは無関係) |
| `RevealAll` | `Check` / `ShowAll` 後、`Reset` まで `true` |
| `Errors` / `GetErrors(field)` | 現在のエラー (フィールド名 → メッセージ一覧。Model のルールが先、Presenter のルールが後) |
| `FormLevel` | フィールドに属さないエラーのキー (`""`) |
| `ErrorsChanged` / `WasReset` | 変更通知 (フレームワークが View への接続に使う) |

---

## クロスフィールド検証の注意点

「確認用パスワードが一致するか」のような複数フィールドにまたがるルールは、**エラーを出したいフィールドのプロパティ規則** として書くのが確実です。

- **`[Compare]` は使わない (.NET Framework)。** .NET Framework の `CompareAttribute` はメンバー名なしでエラーを返すため、エラーが `ConfirmPassword` の横ではなく `FormLevel` のコントロール (送信ボタン) に、しかも送信後にしか出ません。`[CustomValidation]` で `context.MemberName` を付けて返すか (上の `MatchesPassword`)、Presenter ルール (`SetError`) にします。
- **`IValidatableObject.Validate` はすべてのプロパティ規則が通った後にしか実行されません** (DataAnnotations の仕様)。他の欄に 1 つでもエラーがあると、クラスレベルのエラーは出ません。入力中から見せたいルールは `[CustomValidation]` のプロパティ規則にします。
- `IValidatableObject` を使う場合も、`new ValidationResult(msg, new[] { nameof(EndDate) })` のようにメンバー名を付ければそのフィールドに表示されます。メンバー名がなければ `FormLevel` 扱いです。
- 1 つのプロパティに複数のメッセージ (パスワードの大文字・数字・記号の各条件など) を出したい場合は、条件ごとに `[CustomValidation]` を並べます。同じフィールドのメッセージは改行区切りで表示されます。

```csharp
public class Period : IValidatableObject
{
    [Required] public DateTime? StartDate { get; set; }
    [Required] public DateTime? EndDate { get; set; }

    // StartDate / EndDate の [Required] が両方通ったときだけ実行される
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDate >= EndDate)
            yield return new ValidationResult(
                "End date must be after start date.",
                new[] { nameof(EndDate) });   // EndDate の横に表示
    }
}
```

---

## 視覚的なエラー表示をカスタマイズする

`ErrorProviderDisplay` (アイコン + ツールチップ) 以外の見せ方 (ラベル、背景色など) にしたい場合は、`ControlErrorDisplay` を継承して `SetControlError` を 1 つ実装します。「いつ・どのフィールドを表示するか」(訪問済み判定、送信後の全表示、`FormLevel` への集約、`Reset`) は基底クラスが扱います。

```csharp
public sealed class BackColorErrorDisplay : ControlErrorDisplay
{
    private readonly ToolTip _toolTip;

    public BackColorErrorDisplay(ToolTip toolTip) { _toolTip = toolTip; }

    // message が空ならエラー解除。同じコントロールの複数メッセージは改行区切りで渡される
    protected override void SetControlError(Control control, string message)
    {
        control.BackColor = string.IsNullOrEmpty(message) ? SystemColors.Window : Color.MistyRose;
        _toolTip.SetToolTip(control, message);
    }
}
```

```csharp
_errors = new BackColorErrorDisplay(_toolTip)
    .Map(nameof(UserModel.UserName), _nameTextBox)
    .Map(ValidationState.FormLevel, _summaryLabel);
```

WinForms 以外の表示 (エラー一覧パネルなど) が必要なら `IValidationErrorDisplay` を直接実装することもできます。いずれも View 内部の詳細なので Presenter は関与しません。

---

## ChangeTracker と検証を組み合わせる

[ChangeTracker](Reference-ChangeTracker) (変更追跡) と `Validation` (検証) は **無関係な別々の関心事**です。

- **`ChangeTracker`** はベースラインから「**変わったか**」を答える検出ツール。**データの所有者ではありません**。
- **`Validation`** は「**妥当か**」を答え、エラーを View に届けるだけ。

データは **View(が保持・バインドする編集モデル)** が持ち、Presenter が 2 つのツールを **それぞれ別々に** 呼んで編成します。融合ヘルパーはフレームワークでは提供しません ([単一責任](Concept-MVP-Pattern))。

### 典型パターン: 変更有無で Save を有効化 + 保存時に検証

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private ChangeTracker<UserModel> _tracker;

    protected override void OnInitialize()
    {
        var user = LoadUser(_userId);
        _tracker = new ChangeTracker<UserModel>(user);   // ベースラインのスナップショット(変更検出専用)
        View.ShowUser(user);                              // 表示は意味のある View メソッドで(View が編集モデルを保持)
        Validation.Validate(user);
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            StandardActions.Save,
            OnSave,
            // 「変わったか」だけで有効化する。妥当性では無効化しない (未訪問フィールドのエラーを見せるため)
            canExecute: () => _tracker.IsChangedWith(View.GetModel()));
    }

    // 入力が変わったら再検証する。Validation の変更で canExecute も自動で再評価される
    // (エラーが変わらないときは再評価されないので、変更検出のために RaiseCanExecuteChanged も呼ぶ)
    private void OnInputChanged(object sender, EventArgs e)
    {
        Validation.Validate(View.GetModel());
        Dispatcher.RaiseCanExecuteChanged();
    }

    private void OnSave()
    {
        var model = View.GetModel();           // データは View から取得(tracker からではない)
        if (!Validation.Check(model)) return;  // エラーを見せて Save をブロックするだけ。何も巻き戻さない

        SaveUser(model);
        _tracker.AcceptChanges(model);         // 保存できた値を新しいベースラインに(dirty 解消)
        Messages.ShowInfo("Saved.");
    }
}
```

> **CanExecute の再評価にはトリガが要ります。** `canExecute` 述語は登録時、`RaiseCanExecuteChanged()` 呼び出し時、そして `Validation` のエラーが変わったときにしか再計算されません ([アクション外で状態が変わったら呼ぶ](Reference-ViewAction-System))。
>
> キャッシュ `IsChanged` を使いたい場合は、View 変更時に `_tracker.UpdateCurrentValue(View.GetModel())` を呼んで tracker にも現在値を反映し、`IsChangedChanged` ハンドラで `RaiseCanExecuteChanged()` を呼ぶ形にできます (実例 `samples/.../EmailDemo/ComposeEmailPresenter.cs`)。それでもデータの源は View です。

### 検証は「巻き戻し」と無関係

「入力が無効になったら自動で元に戻す」を `ChangeTracker.RejectChanges()` で実装しては **いけません**。`RejectChanges()` は **スナップショット全体をベースラインに戻す** 操作です。3 項目編集して 1 項目だけ無効なときに呼ぶと、**妥当だった残り 2 項目まで巻き戻ってしまいます**。検証はフィールド単位、`RejectChanges` はセッション全体——粒度が噛み合いません。

- **検証失敗時**:エラーを表示して Save をブロックするだけ。`ChangeTracker` には一切触れない。
- **`RejectChanges()` の正しい用途**:ユーザーが明示的に「キャンセル/破棄」したときに、編集セッション**全体**を捨てる操作 (個々のフィールド単位ではない)。破棄後に表示を初期状態へ戻すなら `Validation.Reset()` も呼びます。

検証と変更追跡はそれぞれ単体でテストでき、互いに依存しません。

---

## リファレンス: ModelValidator (DataAnnotations 連携)

`Validation.Validate` / `Check` は内部で `ModelValidator.For<T>().ValidateAll(model)` を使います。画面のエラー表示とは関係なく Model を検証したい場合 (インポート処理、サービス層、Model の単体テストなど) は `IModelValidator<T>` (`WinformsMVP.Common.Validation.Core`) を直接使えます。

```csharp
private readonly IModelValidator<UserModel> _validator = ModelValidator.For<UserModel>();

var errors = _validator.ValidateAll(model);
foreach (var err in errors)
    Logger.LogWarning("{Field}: {Message}", err.MemberNames.FirstOrDefault(), err.ErrorMessage);
```

### ValidateAll vs ValidateSequential

`IModelValidator<T>` には 3 つのメソッドがあります。

| メソッド | 戻り値 | 使いどころ |
|---|---|---|
| `ValidateAll(model)` | `ReadOnlyCollection<ModelValidationResult>` (全エラー) | 全エラーの取得 (`ValidationState` もこれを使う) |
| `ValidateSequential(model)` | `ModelValidationResult` (最初の 1 件のみ) | `[ValidationOrder]` 順に評価し、最初のエラーで停止 |
| `IsValid(model)` | `bool` | 妥当かどうかだけ知りたいとき (内部で `ValidateSequential` 使用) |

> 💡 **パフォーマンス**: `ModelValidator.For<T>()` は **型ごとに singleton** です。リフレクションキャッシュにより、初回 ~1-2ms / 2 回目以降 ~0.1-0.5ms で動作します。入力のたびに `Validation.Validate(model)` を呼んでも問題ありません。

`ValidateSequential` / `IsValid` も、プロパティ規則がすべて通った後にクラスレベル属性と `IValidatableObject.Validate` を評価し、渡されたインスタンスが派生型ならその型の規則も使います。そのため `IsValid(model)` と `ValidateAll(model)` の結果は常に一致します。

### IValidatableObject と 2 つの「結果」型

> ⚠️ **検証の「失敗 1 件」を表す型が 2 つあります。場面で使い分けてください。**
>
> | 場面 | 使う型 | 特徴 |
> |---|---|---|
> | `IValidatableObject.Validate` / `[CustomValidation]` メソッドの戻り値 | **`System.ComponentModel.DataAnnotations.ValidationResult`** (BCL) | `IsValid` プロパティは **ない**。成功は `ValidationResult.Success` (= `null`) を返す/何も `yield` しない |
> | `IModelValidator<T>.ValidateAll` / `ValidateSequential` の戻り値 | **`WinformsMVP.Common.Validation.Core.ModelValidationResult`** (フレームワーク) | `IsValid` プロパティが **ある** |
>
> `IValidatableObject.Validate` を実装するファイルでは **`using System.ComponentModel.DataAnnotations;`** を入れ、戻り値は BCL の `ValidationResult` を `yield` してください (フレームワークの `ModelValidationResult` を返すと契約を満たさずコンパイルエラー)。

### カスタム ValidationAttribute

ドメインルールを複数の Model で再利用したい場合は、`ValidationAttribute` を継承します。メンバー名付きで返せば、そのフィールドに表示されます。

```csharp
public class MinimumAgeAttribute : ValidationAttribute
{
    public int MinimumAge { get; }

    public MinimumAgeAttribute(int minimumAge) { MinimumAge = minimumAge; }

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

属性は宣言的で再利用性が高い一方、**外部リソース (DB・サービス) に触れない** という制約があります。そうしたルールは Presenter の `Validation.SetError` で書きます。

---

## `[ValidationOrder]` でフィールド順序を制御

`[ValidationOrder(N)]` (`WinformsMVP.Common.Validation.Attributes`) をプロパティに付けると、検証の評価順序を制御できます。`ValidateSequential` と組み合わせると **最初のエラーで停止** します。`ValidateAll` (= `ValidationState`) では全エラーを返すので、評価順は同じフィールド内のメッセージの並び順にだけ影響します。

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

`[ValidationOrder]` を付けないプロパティは `Order = 0` 扱いです。同じ Order 内では宣言順 (リフレクションで読まれた順) で評価されます。**明示的な制御が必要なフィールドだけ** 属性を付けてください。

---

## テストパターン

### Presenter のテスト

モック View で `IValidatableView` を実装し、受け取ったエラーを記録する `IValidationErrorDisplay` を返します。アクションは本番と同じ経路 (`view.Binder.Dispatch`) で実行します。

```csharp
private sealed class RecordingDisplay : IValidationErrorDisplay
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; private set; }
    public bool RevealAll { get; private set; }
    public int ResetCount { get; private set; }

    public void ShowErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, bool revealAll)
    {
        Errors = errors;
        RevealAll = revealAll;
    }

    public void Reset() => ResetCount++;
}

// MockRegistrationView : IRegistrationView
//     public RecordingDisplay Display { get; } = new RecordingDisplay();
//     public IValidationErrorDisplay ValidationErrors => Display;

[Fact]
public void Submit_WithEmptyEmail_RevealsErrorAndDoesNotSave()
{
    _view.UserName = "john";
    _view.Email    = "";

    _view.Binder.Dispatch(RegistrationActions.Submit);

    Assert.True(_view.Display.RevealAll);
    Assert.Contains(nameof(RegistrationModel.Email), _view.Display.Errors.Keys);
    Assert.False(_repository.Saved);
}

[Fact]
public void Submit_StaysEnabled_WhileThereAreErrors()
{
    _view.UserName = "";
    _view.RaiseDataChanged();

    Assert.True(_view.Binder.CanDispatch(RegistrationActions.Submit));
}
```

### Model 自体のテスト

検証ルールは Presenter を介さずに Model 単体でテストできます。

```csharp
[Fact]
public void UserModel_WithEmptyName_FailsValidation()
{
    var model = new UserModel { UserName = "", Email = "john@example.com" };

    var errors = ModelValidator.For<UserModel>().ValidateAll(model);

    Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UserModel.UserName)));
}

[Fact]
public void ValidationState_GroupsErrorsByField()
{
    var state = new ValidationState();

    state.Validate(new UserModel { UserName = "", Email = "bad" });

    Assert.NotEmpty(state.GetErrors(nameof(UserModel.UserName)));
    Assert.NotEmpty(state.GetErrors(nameof(UserModel.Email)));
}
```

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — `CanExecute` と `RaiseCanExecuteChanged`
- [ChangeTracker](Reference-ChangeTracker) — 「未保存の変更」と「妥当性」を組み合わせる
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — テストパターン詳細
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — `Validation` を含む基底クラスのメンバー
- [HowTo: エラーを扱う](HowTo-Handle-Errors) — 検証以外のエラー処理
- サンプル:
  - `samples/WinformsMVP.Samples/ValidationDemo/` — `RegistrationModel` (DataAnnotations + `[CustomValidation]`)、`ValidationDemoPresenter` (`Validate` / `Check` / `Reset` / `SetError`)、`ValidationDemoForm` (`ErrorProviderDisplay`) の完全実装
