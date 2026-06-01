# MVP模式对比示例 (MVP Pattern Comparison Demo)

这个Demo展示了MVP模式的两种实现方式：**被动视图(Passive View)** 和 **控制监视(Supervising Controller)**。

## 功能描述

两个版本实现了完全相同的用户信息编辑功能：
- 编辑用户姓名、邮箱、年龄
- 设置用户是否活跃
- 实时验证输入
- 保存和重置功能

## 两种模式对比

### 1. 被动视图 (Passive View)

**特点：**
- Presenter拥有**完全控制权**
- View是**完全被动的**
- **没有数据绑定** - 所有属性都需要手动同步
- Presenter通过接口显式读写所有View属性
- 最大的可测试性

**代码结构：**
```
PassiveView/
├── IUserEditorView.cs         - View接口（暴露所有属性和方法）
├── UserEditorPresenter.cs     - Presenter（控制所有逻辑）
└── UserEditorForm.cs          - Form实现（简单的属性包装）
```

**优点：**
- ✓ 完全控制和最大可测试性
- ✓ 没有隐藏行为（所有逻辑都是显式的）
- ✓ 数据流清晰易懂

**缺点：**
- ✗ 更多的样板代码
- ✗ Presenter需要手动同步View状态
- ✗ 简单场景下代码量较大

**关键代码示例：**

```csharp
// View接口 - 暴露所有属性
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
    void ShowValidationErrors(string errors);
}

// Presenter - 手动读写所有属性
private void OnSave()
{
    var name = View.UserName;      // 手动读取
    var email = View.Email;        // 手动读取
    var age = View.Age;            // 手动读取

    var errors = ValidateUserData(name, email, age);  // Presenter验证
    if (!string.IsNullOrEmpty(errors))
    {
        View.ShowValidationErrors(errors);  // 手动显示错误
        return;
    }
}

// Form - 简单的属性包装
public string UserName
{
    get => _nameTextBox.Text;
    set => _nameTextBox.Text = value;  // 没有数据绑定，手动设置
}
```

### 2. 控制监视 (Supervising Controller)

**特点：**
- 使用 **BindableBase** (实现INotifyPropertyChanged)
- View通过**数据绑定**连接到Model
- Model包含简单的验证逻辑
- Presenter专注于**协调和复杂逻辑**
- 更少的样板代码

**代码结构：**
```
SupervisingController/
├── IUserEditorView.cs         - View接口（暴露Model）
├── UserModel.cs               - Model（继承BindableBase）
├── UserEditorPresenter.cs     - Presenter（协调逻辑）
└── UserEditorForm.cs          - Form实现（设置数据绑定）
```

**优点：**
- ✓ 更少的样板代码
- ✓ 通过数据绑定自动同步UI
- ✓ Presenter专注于业务逻辑
- ✓ 简单场景下代码更简洁

**缺点：**
- ✗ 某些行为隐藏在数据绑定中
- ✗ 需要理解绑定机制
- ✗ Model必须实现INotifyPropertyChanged

**关键代码示例：**

```csharp
// Model - 继承BindableBase，包含数据和简单验证
public class UserModel : BindableBase
{
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))  // 自动触发PropertyChanged
            {
                ValidateAndNotify();  // Model自己验证
            }
        }
    }

    public string ValidationErrors { get; private set; }
    public bool IsValid => string.IsNullOrEmpty(ValidationErrors);
}

// View接口 - 只暴露Model
public interface IUserEditorView : IWindowView
{
    UserModel Model { get; set; }  // 暴露整个Model，不是单个属性
    void UpdateStatus(string message);
}

// Presenter - 使用Model，无需读取单个属性
private void OnSave()
{
    var model = View.Model;  // 直接使用Model

    if (!model.IsValid)  // Model已经自动验证
    {
        return;
    }

    // 使用model的属性
    SaveUser(model.Name, model.Email, model.Age, model.IsActive);
}

// Form - 设置数据绑定
private void SetupDataBindings()
{
    // 使用扩展方法设置双向绑定
    _nameTextBox.Bind(_model, m => m.Name);
    _emailTextBox.Bind(_model, m => m.Email);
    _ageNumericUpDown.Bind(_model, m => (decimal)m.Age);
    _errorLabel.BindProperty(_model, m => m.ValidationErrors, nameof(_errorLabel.Text));

    // 当Model属性变化时，UI自动更新！
    // 当用户编辑UI时，Model自动更新！
}
```

## 如何运行

1. 构建解决方案：
   ```bash
   dotnet build winforms-mvp.sln
   ```

2. 运行示例程序：
   ```bash
   dotnet run --project src/MinformsMVP.Samples
   ```

3. 在主窗口中选择 "MVP Pattern Comparison" 菜单项

4. 点击相应的按钮打开两种模式的实现：
   - **"Open Passive View Demo"** - 打开被动视图版本
   - **"Open Supervising Controller Demo"** - 打开控制监视版本

## 使用建议

### 何时使用被动视图 (Passive View)

- 需要最大可测试性
- 复杂的UI逻辑需要完全控制
- 团队不熟悉数据绑定
- 需要明确的数据流

### 何时使用控制监视 (Supervising Controller)

- 简单的CRUD表单
- 希望减少样板代码
- 团队熟悉数据绑定
- 有大量简单的属性需要同步

## 技术要点

### BindableBase的使用

`BindableBase` 是框架提供的基类，位于 `WinformsMVP.Core.Models` 命名空间：

```csharp
public class UserModel : BindableBase
{
    private string _name;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);  // 自动触发PropertyChanged
    }
}
```

### 数据绑定扩展方法

框架提供了方便的绑定扩展方法，位于 `WinformsMVP.Common.Extensions` 命名空间：

```csharp
// 文本框绑定
_nameTextBox.Bind(_model, m => m.Name);

// 复选框绑定
_isActiveCheckBox.Bind(_model, m => m.IsActive);

// 数值框绑定
_ageNumericUpDown.Bind(_model, m => (decimal)m.Age);

// 通用属性绑定
_errorLabel.BindProperty(_model, m => m.ValidationErrors, nameof(_errorLabel.Text));
```

## 总结

两种模式各有优劣：

- **被动视图**适合需要完全控制和最大可测试性的场景
- **控制监视**适合简单场景，可以减少大量样板代码

在实际项目中，可以根据具体场景混合使用这两种模式。
