# Data Binding 拡張メソッド

本フレームワークは、WinForms 標準の `Control.DataBindings.Add(...)` をラップした **型安全で簡潔な拡張メソッド群** を提供します。
これは **Supervising Controller パターン** で「View 側で UI コントロールとモデルプロパティをバインドする」ために使います。

> **位置づけ**: 本ページの API は **View 実装 (Form / UserControl) からのみ使用** します。Presenter からは使いません ([MVP 鉄則 2](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) — Presenter は UI コントロールを知らない)。

---

## 目次

- [なぜ拡張メソッドか](#なぜ拡張メソッドか)
- [モデル側の要件: `INotifyPropertyChanged`](#モデル側の要件-inotifypropertychanged)
- [汎用バインド: `BindProperty`](#汎用バインド-bindproperty)
- [標準コントロール用バインド](#標準コントロール用バインド)
  - [TextBox](#textbox)
  - [CheckBox / RadioButton](#checkbox--radiobutton)
  - [NumericUpDown](#numericupdown)
  - [Label](#label)
  - [ComboBox](#combobox)
  - [DateTimePicker](#datetimepicker)
  - [TrackBar](#trackbar)
  - [ProgressBar](#progressbar)
  - [ListBox](#listbox)
  - [RichTextBox](#richtextbox)
  - [MaskedTextBox](#maskedtextbox)
  - [PictureBox (画像 URL)](#picturebox-画像-url)
- [RadioButton グループバインド](#radiobutton-グループバインド)
- [双方向バインドと単方向バインド](#双方向バインドと単方向バインド)
- [更新タイミング (`DataSourceUpdateMode`)](#更新タイミング-datasourceupdatemode)
- [使用例: 完全な Form](#使用例-完全な-form)
- [トラブルシューティング](#トラブルシューティング)
- [関連ページ](#関連ページ)

---

## なぜ拡張メソッドか

WinForms 標準の Data Binding API は冗長で文字列ベース、リファクタリング耐性も低いです。

```csharp
// ❌ 標準 API — 冗長で typo に弱い
textBox.DataBindings.Add(
    "Text",                                     // 文字列で control プロパティ指定
    model,
    "UserName",                                 // 文字列でモデルプロパティ指定
    false,
    DataSourceUpdateMode.OnPropertyChanged);

// ✅ 拡張メソッド — 簡潔で型安全
textBox.Bind(model, m => m.UserName);
```

ラムダ式で書くので、

- リネーム時に IDE が追従する
- typo がコンパイル時に検出される
- バインド経路が一目で分かる

---

## モデル側の要件: `INotifyPropertyChanged`

双方向バインドが正しく機能するには、モデルが `INotifyPropertyChanged` を実装している必要があります。

```csharp
public class UserModel : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

C# 8.0+ の場合は record や `INotifyPropertyChanged` 自動生成ライブラリ ([Fody](https://github.com/Fody/PropertyChanged)、[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)) と併用すると省力化できます。

### フレームワーク提供の `BindableBase`

繰り返しの `INotifyPropertyChanged` 実装を省くため、フレームワークは `BindableBase` 基底クラスを提供します (`WinformsMVP.MVP.Models` 名前空間)。

```csharp
using WinformsMVP.MVP.Models;

public class UserModel : BindableBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _email;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }
}
```

`SetProperty<T>` は値の変化を `EqualityComparer<T>.Default.Equals` で比較し、変更があったときだけ `PropertyChanged` イベントを発火します。`[CallerMemberName]` 属性によりプロパティ名は自動取得されるので、文字列リテラルの typo を防げます。

外部ライブラリへの依存なしに `INotifyPropertyChanged` を一行で実装でき、net40 から net48 まで全ターゲットで使えます。

---

## 汎用バインド: `BindProperty`

任意のコントロールの任意のプロパティをバインドする汎用 API です。

```csharp
public static void BindProperty<TControl, TViewModel, TValue>(
    this TControl control,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression,
    string controlPropertyName)
    where TControl : Control
    where TViewModel : INotifyPropertyChanged
```

### 使用例

```csharp
// Label の ForeColor をモデルから決める
label.BindProperty(model, m => m.StatusColor, nameof(label.ForeColor));

// TextBox の ReadOnly をモデルから決める
textBox.BindProperty(model, m => m.IsReadOnly, nameof(textBox.ReadOnly));

// Button の Enabled をモデルから決める
button.BindProperty(model, m => m.CanSubmit, nameof(button.Enabled));
```

### こんなときに

- 専用 `Bind()` がない属性に対応したい
- 非標準のコントロールに対応したい
- コントロールの `Enabled` / `Visible` 等を動的に制御したい

---

## 標準コントロール用バインド

頻出パターンには、専用の `Bind()` メソッドが用意されています。

### TextBox

```csharp
textBox.Bind(model, m => m.UserName);
textBox.Bind(model, m => m.Email);
```

| | |
|---|---|
| バインド対象 | `Text` プロパティ |
| 双方向 | ✅ (ユーザー入力 → Model、Model → TextBox) |
| 型 | 任意 (内部で `ToString()`) |

### CheckBox / RadioButton

```csharp
checkBox.Bind(model, m => m.IsActive);
radioButton.Bind(model, m => m.IsSelected);
```

| | |
|---|---|
| バインド対象 | `Checked` プロパティ |
| 双方向 | ✅ |
| 型 | `bool` のみ |

### NumericUpDown

```csharp
numericUpDown.Bind(model, m => m.Quantity);
priceUpDown.Bind(model, m => m.Price);
```

| | |
|---|---|
| バインド対象 | `Value` プロパティ |
| 双方向 | ✅ |
| 型 | `decimal` (int の場合は明示キャスト: `m => (decimal)m.Age`) |

### Label

```csharp
label.Bind(model, m => m.Status);
totalLabel.Bind(model, m => m.Total);

// 表示形式をカスタマイズ
summaryLabel.Bind(model, m => $"Total: {m.ItemCount} items");
```

| | |
|---|---|
| バインド対象 | `Text` プロパティ |
| 双方向 | ❌ (Model → Label のみ、Label はユーザー編集不可) |
| 型 | 任意 (内部で `ToString()`) |

### ComboBox

ComboBox は **3 つの API** があります。用途に応じて使い分けます。

```csharp
// 1. SelectedValue (enum やキー値の選択に推奨)
comboBox.Bind(model, m => m.SelectedStatus);

// 2. SelectedItem (項目オブジェクトを選択)
comboBox.BindSelectedItem(model, m => m.SelectedUser);

// 3. SelectedIndex (インデックス値)
comboBox.BindSelectedIndex(model, m => m.SelectedTabIndex);
```

| API | バインド対象 | 用途 |
|---|---|---|
| `Bind` | `SelectedValue` | enum、キー値 |
| `BindSelectedItem` | `SelectedItem` | 項目オブジェクト |
| `BindSelectedIndex` | `SelectedIndex` | インデックス |

ComboBox の選択肢自体は `DataSource` か `Items.AddRange` で設定します。

```csharp
comboBox.DataSource = Enum.GetValues(typeof(Status));
comboBox.Bind(model, m => m.CurrentStatus);
```

### DateTimePicker

```csharp
datePicker.Bind(model, m => m.BirthDate);
```

| | |
|---|---|
| バインド対象 | `Value` プロパティ |
| 双方向 | ✅ |
| 型 | `DateTime` |

### TrackBar

```csharp
trackBar.Bind(model, m => m.Volume);
```

| | |
|---|---|
| バインド対象 | `Value` プロパティ |
| 双方向 | ✅ |
| 型 | `int` |

### ProgressBar

```csharp
progressBar.Bind(model, m => m.PercentComplete);
```

| | |
|---|---|
| バインド対象 | `Value` プロパティ |
| 双方向 | △ (ユーザー入力不能なため実質単方向) |
| 型 | `int` |

### ListBox

ListBox は **SelectedItem** と **SelectedIndex** の 2 種類のバインドがあります。

```csharp
// 選択中のオブジェクト (任意の型) を Model に同期
listBox.BindSelectedItem(model, m => m.SelectedTag);

// 選択中のインデックス (int) を Model に同期
listBox.BindSelectedIndex(model, m => m.SelectedRow);
```

| メソッド | バインド対象 | 型 |
|---|---|---|
| `BindSelectedItem` | `SelectedItem` | 任意 |
| `BindSelectedIndex` | `SelectedIndex` | `int` |

`Items` (リストの中身) 自体をバインドする場合は `BindProperty` で `nameof(listBox.DataSource)` を指定するか、`ListBox.DataSource` に直接コレクションを代入してください。

### RichTextBox

```csharp
richTextBox.Bind(model, m => m.Content);
```

| | |
|---|---|
| バインド対象 | `Text` プロパティ |
| 双方向 | ✅ |
| 型 | 任意 (内部で `ToString()`) |

書式 (色・フォント・段落) のバインドが必要な場合は `BindProperty` を使うか、RichTextBox を直接操作してください。

### MaskedTextBox

```csharp
maskedTextBox.Bind(model, m => m.PhoneNumber);
```

| | |
|---|---|
| バインド対象 | `Text` プロパティ |
| 双方向 | ✅ |
| 型 | 任意 (内部で `ToString()`) |

マスクパターン (例: `000-0000-0000`) は MaskedTextBox 側の `Mask` プロパティで設定します。バインドの対象は実際の入力値 (`Text`) のみです。

### PictureBox (画像 URL)

画像 URL を Model から指定する場合の専用メソッドです (`BindImageLocation`)。

```csharp
pictureBox.BindImageLocation(model, m => m.AvatarUrl);
```

| | |
|---|---|
| バインド対象 | `ImageLocation` プロパティ |
| 双方向 | OneWay (Model → PictureBox) |
| 型 | `string` (URL またはローカルパス) |

`Image` プロパティ (`Bitmap` インスタンス) を直接バインドする場合は `BindProperty` で `nameof(pictureBox.Image)` を指定してください。

---

## RadioButton グループバインド

複数の RadioButton を 1 つのプロパティに連動させる場合は、`BindRadioGroup` 拡張メソッドが便利です。各 RadioButton と「対応する値」のペアを列挙してバインドします。

```csharp
using WinformsMVP.MVP.Models;

public enum Priority { Low, Medium, High }

public class TaskModel : BindableBase
{
    private Priority _priority;
    public Priority Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }
}

// Form 側
var pairs = new Dictionary<RadioButton, Priority>
{
    { rbLow,    Priority.Low },
    { rbMedium, Priority.Medium },
    { rbHigh,   Priority.High },
};

pairs.BindRadioGroup(model, m => m.Priority);
```

| | |
|---|---|
| バインド対象 | 各 RadioButton の `Checked` プロパティ |
| 双方向 | ✅ (ラジオ選択 ↔ Model、Model → ラジオ) |
| 型 | 任意 (enum・int・string 等、`Equals` で比較可能なもの) |

個別の RadioButton に `.Bind(model, m => m.IsX)` を使うと「相互排他」の制約を Model 側で複数 `bool` プロパティに分けて書く必要があります。一方 `BindRadioGroup` は **「1 つのプロパティ + 複数の選択肢」** という自然な表現で、グループ内の選択を自動で同期します。**3 つ以上の選択肢から 1 つ選ぶ UI** で第一選択です。

---

## 双方向バインドと単方向バインド

| バインド方向 | 説明 | 該当コントロール |
|------------|----|---------------|
| **双方向 (TwoWay)** | UI と Model が相互に同期 | TextBox、CheckBox、RadioButton、NumericUpDown、ComboBox、DateTimePicker、TrackBar、ListBox、RichTextBox、MaskedTextBox |
| **単方向 (OneWay)** | Model → UI のみ | Label、ProgressBar、PictureBox |

双方向バインドは UI 編集が自動で Model に反映されるので、Presenter で「TextBox の値を Model に書き戻す」コードが不要になります。

---

## 更新タイミング (`DataSourceUpdateMode`)

WinForms のデフォルト更新タイミングは:

| モード | UI → Model の更新タイミング |
|--------|--------------------------|
| `OnValidation` | コントロールがフォーカスを失ったとき |
| `OnPropertyChanged` | UI が変化するたび (リアルタイム) |
| `Never` | 手動更新のみ |

本フレームワークの `Bind()` メソッドは **`OnPropertyChanged`** を既定値にしています (リアルタイム同期)。これにより、`CanExecute` 判定や状態駆動の処理が即座に反映されます。

---

## 使用例: 完全な Form

以下は ViewAction + Data Binding を組み合わせた典型例です。

```csharp
public class UserEditorForm : Form, IUserEditorView
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _emailTextBox;
    private readonly CheckBox _activeCheckBox;
    private readonly Label _statusLabel;
    private readonly Button _btnSave;
    private readonly ViewActionBinder _binder;

    private UserModel _model;

    public UserEditorForm()
    {
        // ... コントロール生成 + レイアウト

        _binder = new ViewActionBinder();
        _binder.Add(CommonActions.Save, _btnSave);
    }

    public ViewActionBinder ActionBinder => _binder;

    public void BindModel(UserModel model)
    {
        _model = model;

        _nameTextBox.Bind(model, m => m.Name);
        _emailTextBox.Bind(model, m => m.Email);
        _activeCheckBox.Bind(model, m => m.IsActive);
        _statusLabel.Bind(model, m => m.Status);
    }
}
```

```csharp
public interface IUserEditorView : IWindowView
{
    void BindModel(UserModel model);
    ViewActionBinder ActionBinder { get; }
}
```

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private UserModel _model;

    protected override void OnInitialize()
    {
        _model = LoadUser();
        View.BindModel(_model);                    // ← Form 側でバインド構成
    }

    private void OnSave()
    {
        // _model.Name / .Email 等は Data Binding で自動的に最新
        SaveUser(_model);
        Messages.ShowInfo("Saved", "Success");
    }
}
```

ポイント:

- Presenter が `View.BindModel(model)` 経由で「これをバインドして」と指示するだけ
- View 実装が **具体的なコントロールとプロパティの紐付け** を担う
- ユーザーが TextBox に入力すると `_model.Name` が自動で更新される (Presenter で取得する必要なし)

---

## トラブルシューティング

### Model 更新が UI に反映されない

**原因**: Model が `INotifyPropertyChanged` を実装していない、または `PropertyChanged` を発火していない。

**対処**: モデルの setter で `OnPropertyChanged()` を呼ぶ。

### UI 入力が Model に反映されない

**原因**: バインドが単方向になっている可能性 (Label は本来単方向)。または `DataSourceUpdateMode` が `Never`。

**対処**: TextBox / NumericUpDown 等の入力可能コントロールには `Bind()` を使う (内部で双方向構成)。

### NumericUpDown でビルドエラー

**原因**: モデルプロパティが `int` 等で `decimal` への暗黙変換ができない。

**対処**: ラムダ内で明示キャスト。

```csharp
numericUpDown.Bind(model, m => (decimal)m.Age);
```

### バインド先のプロパティ名が typo

**対処**: 拡張メソッドの第 1 形式 (`Bind(model, m => m.Xxx)`) を使えばコンパイル時に検出できます。`BindProperty` の `controlPropertyName` (文字列) は `nameof()` で安全に書く。

```csharp
button.BindProperty(model, m => m.CanSubmit, nameof(button.Enabled));
```

---

## 関連ページ

- [ViewAction システム](Reference-ViewAction-System) — アクション発火と組み合わせて使う
- [MVP パターンとは](Concept-MVP-Pattern) — View 実装の責務範囲
- [Reference-ChangeTracker](Reference-ChangeTracker) — バインドされたモデルの変更追跡
- サンプル:
  - `samples/WinformsMVP.Samples/ToDoDemo/` — Data Binding の実例
