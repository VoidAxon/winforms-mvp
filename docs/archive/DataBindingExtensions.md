# 数据绑定扩展方法使用指南

本文档介绍框架提供的所有数据绑定扩展方法，用于**Supervising Controller（控制监视）**模式。

## 📖 目录

- [核心概念](#核心概念)
- [通用绑定方法](#通用绑定方法)
- [常用控件绑定](#常用控件绑定)
- [高级绑定](#高级绑定)
- [完整示例](#完整示例)
- [常见问题](#常见问题)

---

## 核心概念

### 什么是数据绑定？

数据绑定是**自动同步UI控件和数据模型**的机制：

```
Model属性变化 → 自动更新 → UI控件
UI控件输入   → 自动更新 → Model属性
```

### 为什么需要扩展方法？

WinForms原生的数据绑定API比较繁琐：

```csharp
// ❌ 原生API - 繁琐
textBox.DataBindings.Add(
    "Text",                              // 控件属性名（字符串，容易出错）
    model,                               // 数据源
    "UserName",                          // 模型属性名（字符串，容易出错）
    false,
    DataSourceUpdateMode.OnPropertyChanged
);
```

扩展方法提供了**类型安全**和**简洁**的写法：

```csharp
// ✅ 扩展方法 - 简洁且类型安全
textBox.Bind(model, m => m.UserName);
```

---

## 通用绑定方法

### BindProperty<TControl, TViewModel, TValue>

**用途：** 绑定任意控件的任意属性到ViewModel属性

**签名：**
```csharp
public static void BindProperty<TControl, TViewModel, TValue>(
    this TControl control,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression,
    string controlPropertyName)
    where TControl : Control
    where TViewModel : INotifyPropertyChanged
```

**使用场景：**
- 绑定框架未提供专用扩展方法的控件
- 绑定控件的非标准属性

**示例：**

```csharp
// 绑定Label的ForeColor属性
label.BindProperty(model, m => m.StatusColor, nameof(label.ForeColor));

// 绑定TextBox的ReadOnly属性
textBox.BindProperty(model, m => m.IsReadOnly, nameof(textBox.ReadOnly));

// 绑定Button的Enabled属性
button.BindProperty(model, m => m.CanSubmit, nameof(button.Enabled));
```

---

## 常用控件绑定

以下是框架提供的所有专用绑定扩展方法：

### 1. TextBox - 文本输入框

**绑定Text属性：**

```csharp
public static void Bind<TViewModel>(
    this TextBox textBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, object>> propertyExpression)
```

**示例：**

```csharp
// 绑定用户名
_nameTextBox.Bind(model, m => m.Name);

// 绑定邮箱
_emailTextBox.Bind(model, m => m.Email);

// 绑定描述
_descriptionTextBox.Bind(model, m => m.Description);
```

**特点：**
- 双向绑定（用户输入自动更新Model，Model变化自动更新TextBox）
- 支持任意类型（会自动ToString()）

---

### 2. CheckBox - 复选框

**绑定Checked属性：**

```csharp
public static void Bind<TViewModel>(
    this CheckBox checkBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, bool>> propertyExpression)
```

**示例：**

```csharp
// 绑定是否活跃
_isActiveCheckBox.Bind(model, m => m.IsActive);

// 绑定是否同意条款
_agreeTermsCheckBox.Bind(model, m => m.AgreeToTerms);

// 绑定是否启用功能
_enableFeatureCheckBox.Bind(model, m => m.IsFeatureEnabled);
```

**特点：**
- 只能绑定bool类型
- 完全双向绑定

---

### 3. NumericUpDown - 数值输入框

**绑定Value属性：**

```csharp
public static void Bind<TViewModel>(
    this NumericUpDown numericUpDown,
    TViewModel viewModel,
    Expression<Func<TViewModel, decimal>> propertyExpression)
```

**示例：**

```csharp
// 绑定年龄（注意：需要转换为decimal）
_ageNumericUpDown.Bind(model, m => (decimal)m.Age);

// 绑定价格
_priceNumericUpDown.Bind(model, m => m.Price);

// 绑定数量
_quantityNumericUpDown.Bind(model, m => (decimal)m.Quantity);
```

**注意事项：**
- Model属性必须是decimal或能转换为decimal
- 如果Model属性是int，需要显式转换：`m => (decimal)m.Age`

---

### 4. Label - 标签（只读显示）

**绑定Text属性：**

```csharp
public static void Bind<TViewModel>(
    this Label label,
    TViewModel viewModel,
    Expression<Func<TViewModel, object>> propertyExpression)
```

**示例：**

```csharp
// 显示验证错误
_errorLabel.Bind(model, m => m.ValidationErrors);

// 显示状态
_statusLabel.Bind(model, m => m.Status);

// 显示计算结果
_totalLabel.Bind(model, m => m.Total);

// 显示格式化文本
_summaryLabel.Bind(model, m => $"Total: {m.ItemCount} items");
```

**特点：**
- 单向绑定（Model → Label，用户不能编辑）
- 适合显示状态、错误、计算结果

---

### 5. ComboBox - 下拉框

**绑定SelectedValue（推荐用于枚举）：**

```csharp
public static void Bind<TViewModel, TValue>(
    this ComboBox comboBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression)
```

**绑定SelectedItem（用于对象）：**

```csharp
public static void BindSelectedItem<TViewModel, TValue>(
    this ComboBox comboBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression)
```

**绑定SelectedIndex（用于索引）：**

```csharp
public static void BindSelectedIndex<TViewModel>(
    this ComboBox comboBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, int>> propertyExpression)
```

**示例：**

```csharp
// 方式1: 绑定枚举值
public enum UserRole { Admin, User, Guest }

_roleComboBox.DataSource = Enum.GetValues(typeof(UserRole));
_roleComboBox.Bind(model, m => m.Role);

// 方式2: 绑定选中的项（对象）
_countryComboBox.DataSource = countries; // List<Country>
_countryComboBox.BindSelectedItem(model, m => m.SelectedCountry);

// 方式3: 绑定索引
_categoryComboBox.BindSelectedIndex(model, m => m.CategoryIndex);
```

---

### 6. DateTimePicker - 日期时间选择器

**绑定Value属性：**

```csharp
public static void Bind<TViewModel>(
    this DateTimePicker dateTimePicker,
    TViewModel viewModel,
    Expression<Func<TViewModel, DateTime>> propertyExpression)
```

**示例：**

```csharp
// 绑定出生日期
_birthDatePicker.Bind(model, m => m.BirthDate);

// 绑定开始日期
_startDatePicker.Bind(model, m => m.StartDate);

// 绑定截止日期
_deadlinePicker.Bind(model, m => m.Deadline);
```

**特点：**
- 只能绑定DateTime类型
- 完全双向绑定

---

### 7. ListBox - 列表框

**绑定SelectedItem：**

```csharp
public static void BindSelectedItem<TViewModel, TValue>(
    this ListBox listBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression)
```

**绑定SelectedIndex：**

```csharp
public static void BindSelectedIndex<TViewModel>(
    this ListBox listBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, int>> propertyExpression)
```

**示例：**

```csharp
// 绑定选中的任务
_taskListBox.DataSource = tasks;
_taskListBox.BindSelectedItem(model, m => m.SelectedTask);

// 绑定选中的索引
_optionListBox.BindSelectedIndex(model, m => m.SelectedOptionIndex);
```

---

### 8. TrackBar - 滑块

**绑定Value属性：**

```csharp
public static void Bind<TViewModel>(
    this TrackBar trackBar,
    TViewModel viewModel,
    Expression<Func<TViewModel, int>> propertyExpression)
```

**示例：**

```csharp
// 绑定音量
_volumeTrackBar.Minimum = 0;
_volumeTrackBar.Maximum = 100;
_volumeTrackBar.Bind(model, m => m.Volume);

// 绑定亮度
_brightnessTrackBar.Bind(model, m => m.Brightness);
```

---

### 9. ProgressBar - 进度条

**绑定Value属性：**

```csharp
public static void Bind<TViewModel>(
    this ProgressBar progressBar,
    TViewModel viewModel,
    Expression<Func<TViewModel, int>> propertyExpression)
```

**示例：**

```csharp
// 绑定进度
_downloadProgressBar.Minimum = 0;
_downloadProgressBar.Maximum = 100;
_downloadProgressBar.Bind(model, m => m.DownloadProgress);
```

**特点：**
- 通常是单向绑定（Model → ProgressBar）
- 用于显示进度状态

---

### 10. RichTextBox - 富文本框

**绑定Text属性：**

```csharp
public static void Bind<TViewModel>(
    this RichTextBox richTextBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, object>> propertyExpression)
```

**示例：**

```csharp
// 绑定富文本内容
_contentRichTextBox.Bind(model, m => m.Content);

// 绑定日志
_logRichTextBox.Bind(model, m => m.LogText);
```

---

### 11. MaskedTextBox - 格式化文本框

**绑定Text属性：**

```csharp
public static void Bind<TViewModel>(
    this MaskedTextBox maskedTextBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, object>> propertyExpression)
```

**示例：**

```csharp
// 绑定电话号码
_phoneMaskedTextBox.Mask = "(000) 000-0000";
_phoneMaskedTextBox.Bind(model, m => m.PhoneNumber);

// 绑定邮编
_zipCodeMaskedTextBox.Mask = "00000";
_zipCodeMaskedTextBox.Bind(model, m => m.ZipCode);
```

---

### 12. PictureBox - 图片框

**绑定ImageLocation（图片URL）：**

```csharp
public static void BindImageLocation<TViewModel>(
    this PictureBox pictureBox,
    TViewModel viewModel,
    Expression<Func<TViewModel, object>> propertyExpression)
```

**示例：**

```csharp
// 绑定头像URL
_avatarPictureBox.BindImageLocation(model, m => m.AvatarUrl);

// 绑定产品图片URL
_productImagePictureBox.BindImageLocation(model, m => m.ProductImageUrl);
```

---

## 高级绑定

### RadioButton组绑定

**用途：** 将一组RadioButton绑定到Model的一个枚举或值类型属性

**签名：**

```csharp
public static void BindRadioGroup<TViewModel, TValue>(
    this IEnumerable<KeyValuePair<RadioButton, TValue>> radioPairs,
    TViewModel viewModel,
    Expression<Func<TViewModel, TValue>> propertyExpression)
```

**示例：**

```csharp
// 定义枚举
public enum Gender { Male, Female, Other }

// Model
public class UserModel : BindableBase
{
    private Gender _gender;
    public Gender Gender
    {
        get => _gender;
        set => SetProperty(ref _gender, value);
    }
}

// View中绑定
var genderRadios = new Dictionary<RadioButton, Gender>
{
    { _maleRadioButton, Gender.Male },
    { _femaleRadioButton, Gender.Female },
    { _otherRadioButton, Gender.Other }
};

genderRadios.BindRadioGroup(model, m => m.Gender);
```

**工作原理：**
1. 当用户选择RadioButton时，自动更新Model.Gender
2. 当Model.Gender变化时，自动选中对应的RadioButton

---

## 完整示例

### 示例1: 用户信息编辑表单

**Model:**

```csharp
using WinformsMVP.Core.Models;

public enum UserRole { Admin, User, Guest }

public class UserModel : BindableBase
{
    private string _name;
    private string _email;
    private int _age;
    private DateTime _birthDate;
    private bool _isActive;
    private UserRole _role;
    private string _description;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public DateTime BirthDate
    {
        get => _birthDate;
        set => SetProperty(ref _birthDate, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public UserRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }
}
```

**View (设置绑定):**

```csharp
using WinformsMVP.Common.Extensions;

private void SetupDataBindings()
{
    if (_model == null) return;

    // 清除旧绑定
    ClearAllBindings();

    // 基础文本输入
    _nameTextBox.Bind(_model, m => m.Name);
    _emailTextBox.Bind(_model, m => m.Email);
    _descriptionRichTextBox.Bind(_model, m => m.Description);

    // 数值输入
    _ageNumericUpDown.Bind(_model, m => (decimal)m.Age);

    // 日期输入
    _birthDatePicker.Bind(_model, m => m.BirthDate);

    // 复选框
    _isActiveCheckBox.Bind(_model, m => m.IsActive);

    // 下拉框（枚举）
    _roleComboBox.DataSource = Enum.GetValues(typeof(UserRole));
    _roleComboBox.Bind(_model, m => m.Role);

    // 显示标签（只读）
    _statusLabel.Bind(_model, m =>
        m.IsActive ? "Active User" : "Inactive User");
}

private void ClearAllBindings()
{
    _nameTextBox.DataBindings.Clear();
    _emailTextBox.DataBindings.Clear();
    _ageNumericUpDown.DataBindings.Clear();
    _birthDatePicker.DataBindings.Clear();
    _isActiveCheckBox.DataBindings.Clear();
    _roleComboBox.DataBindings.Clear();
    _statusLabel.DataBindings.Clear();
}
```

### 示例2: 设置面板（带RadioButton组）

**Model:**

```csharp
public enum Theme { Light, Dark, Auto }
public enum Language { English, Japanese, Chinese }

public class SettingsModel : BindableBase
{
    private Theme _theme;
    private Language _language;
    private bool _enableNotifications;
    private int _fontSize;

    public Theme Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public Language Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    public int FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }
}
```

**View:**

```csharp
private void SetupDataBindings()
{
    // 主题选择（RadioButton组）
    var themeRadios = new Dictionary<RadioButton, Theme>
    {
        { _lightThemeRadio, Theme.Light },
        { _darkThemeRadio, Theme.Dark },
        { _autoThemeRadio, Theme.Auto }
    };
    themeRadios.BindRadioGroup(_model, m => m.Theme);

    // 语言选择（RadioButton组）
    var languageRadios = new Dictionary<RadioButton, Language>
    {
        { _englishRadio, Language.English },
        { _japaneseRadio, Language.Japanese },
        { _chineseRadio, Language.Chinese }
    };
    languageRadios.BindRadioGroup(_model, m => m.Language);

    // 通知开关
    _notificationsCheckBox.Bind(_model, m => m.EnableNotifications);

    // 字体大小
    _fontSizeTrackBar.Minimum = 8;
    _fontSizeTrackBar.Maximum = 24;
    _fontSizeTrackBar.Bind(_model, m => m.FontSize);

    // 显示当前字体大小
    _fontSizeLabel.Bind(_model, m => $"Font Size: {m.FontSize}pt");
}
```

---

## 常见问题

### Q1: 绑定后为什么UI不更新？

**A:** 确保Model继承了`BindableBase`并使用`SetProperty`：

```csharp
// ❌ 错误 - 不会触发PropertyChanged
public string Name { get; set; }

// ✅ 正确 - 会触发PropertyChanged
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### Q2: 如何清除绑定？

**A:** 调用`DataBindings.Clear()`：

```csharp
_nameTextBox.DataBindings.Clear();
```

### Q3: 能绑定计算属性吗？

**A:** 可以，但需要手动触发PropertyChanged：

```csharp
private string _firstName;
private string _lastName;

public string FirstName
{
    get => _firstName;
    set
    {
        if (SetProperty(ref _firstName, value))
            OnPropertyChanged(nameof(FullName)); // 通知FullName也变了
    }
}

public string LastName
{
    get => _lastName;
    set
    {
        if (SetProperty(ref _lastName, value))
            OnPropertyChanged(nameof(FullName));
    }
}

public string FullName => $"{FirstName} {LastName}";

// 绑定
_fullNameLabel.Bind(model, m => m.FullName);
```

### Q4: NumericUpDown绑定int类型为什么报错？

**A:** NumericUpDown.Value是decimal类型，需要显式转换：

```csharp
// ❌ 错误
_ageNumericUpDown.Bind(model, m => m.Age); // Age是int

// ✅ 正确
_ageNumericUpDown.Bind(model, m => (decimal)m.Age);
```

### Q5: 如何绑定集合数据（如List）？

**A:** 对于集合，使用ObservableCollection并绑定到DataSource：

```csharp
// Model
public ObservableCollection<string> Items { get; set; }

// View
_listBox.DataSource = model.Items;
```

**注意：** 集合绑定不使用扩展方法，直接设置DataSource。

---

## 总结

### 扩展方法的优势

1. **类型安全** - 使用Lambda表达式，编译时检查
2. **智能提示** - IDE自动完成
3. **简洁** - 一行代码完成绑定
4. **双向绑定** - 自动同步Model和UI

### 使用原则

1. **Model必须继承BindableBase** - 否则不会触发PropertyChanged
2. **使用SetProperty设置属性** - 自动触发通知
3. **类型匹配** - 注意控件属性类型和Model属性类型
4. **及时清除绑定** - 避免内存泄漏

### 扩展性

如果需要绑定框架未提供的控件，使用**BindProperty**通用方法：

```csharp
customControl.BindProperty(model, m => m.CustomProperty, "CustomPropertyName");
```

祝您使用愉快！🎉
