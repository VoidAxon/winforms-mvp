# MessageBox Positioning - Native API vs Custom Form

> ⚠️ **使用范围 / Scope**
>
> `PositionableMessageBox` 是 **View 层工具**（Form / UserControl 事件处理器、遗留代码迁移场景）。
> 它返回 `System.Windows.Forms.DialogResult` 并依赖 WinForms API，**不要在 Presenter 中直接调用**。
>
> - ✅ View 实现层（Form / UserControl）：可以直接使用
> - ✅ Service 实现层（`MessageService` 内部）：可以直接使用
> - ✅ 遗留代码迁移阶段：作为过渡可以使用
> - ❌ Presenter：请改用 `IMessageService.ShowInfoAt / ConfirmYesNoAt / ConfirmYesNoCancelAt`，
>   它会返回抽象的 `bool` / `ConfirmResult`，不会把 WinForms 类型泄露到业务逻辑

## 问题背景

WinForms的`MessageBox.Show()`默认只能在屏幕中央显示，无法指定位置。这在某些场景下不够灵活（例如：在特定控件旁边显示提示）。

## 解决方案对比

### ❌ 旧方案：自定义Form模拟MessageBox

**文件:** `PositionedMessageBox.cs`（已弃用）

**实现方式:**
- 创建一个自定义Form
- 手动绘制按钮、图标、文本
- 设置`FormStartPosition.Manual`指定位置

**问题:**
- ✖️ 图标只是Unicode字符（ℹ ⚠ ✖），不是系统图标
- ✖️ 样式可能和原生MessageBox不一致
- ✖️ 需要自己维护键盘支持（Tab、Enter、Esc等）
- ✖️ 需要自己处理按钮布局和本地化
- ✖️ 无法使用系统主题（Dark Mode等）
- ✖️ 代码量大（~170行）

**代码示例:**
```csharp
// 旧方式 - 使用自定义Form
using (var dialog = new PositionedMessageBox(
    "Are you sure?",
    "Confirm",
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question,
    new Point(100, 100)))
{
    dialog.ShowDialog();
    return dialog.Result == DialogResult.Yes;
}
```

### ✅ 新方案：Windows API Hook

**文件:** `PositionableMessageBox.cs`

**实现方式:**
- 使用Windows API的**CBT Hook**（Computer-Based Training Hook）
- 拦截原生MessageBox的创建事件
- 在MessageBox显示前设置位置
- 立即移除Hook

**优点:**
- ✅ 使用原生Windows MessageBox（完全一致的外观）
- ✅ 系统图标、系统主题、系统声音
- ✅ 自动支持键盘快捷键（Tab、Enter、Esc、Alt+Y等）
- ✅ 自动支持多语言和本地化
- ✅ 支持Dark Mode等系统主题
- ✅ 代码简洁（~200行，但包含完整API）

**代码示例:**
```csharp
// 新方式 - 使用Windows API Hook
PositionableMessageBox.Show(
    "Are you sure?",
    "Confirm",
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question,
    new Point(100, 100));
```

## 技术原理

### CBT Hook工作流程

```
1. 调用 SetWindowsHookEx(WH_CBT, ...)  安装Hook
2. 调用原生 MessageBox.Show(...)      触发创建
3. Hook回调 HCBT_ACTIVATE             MessageBox即将激活
4. 调用 SetWindowPos(...)             设置位置
5. 调用 UnhookWindowsHookEx(...)      移除Hook
6. MessageBox正常显示                  在指定位置
```

### 关键API

```csharp
// 1. 安装CBT Hook
IntPtr hookHandle = SetWindowsHookEx(
    WH_CBT,              // Hook类型：CBT
    hookCallback,        // 回调函数
    IntPtr.Zero,         // 当前模块
    GetCurrentThreadId() // 当前线程
);

// 2. Hook回调中设置位置
private static IntPtr CBTHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode == HCBT_ACTIVATE)  // MessageBox即将激活
    {
        IntPtr hWnd = wParam;    // MessageBox窗口句柄

        // 设置位置（保持大小和Z-order）
        SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

        // 立即移除Hook
        UnhookWindowsHookEx(hookHandle);
    }

    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
}
```

## 使用示例

### 基本用法

```csharp
// 在指定位置显示信息
PositionableMessageBox.ShowInfo("Operation completed!", new Point(200, 200), "Success");

// 在指定位置显示警告
PositionableMessageBox.ShowWarning("File already exists!", new Point(300, 150), "Warning");

// 在指定位置显示错误
PositionableMessageBox.ShowError("Failed to save file!", new Point(100, 100), "Error");
```

### 确认对话框

```csharp
// Yes/No确认
bool confirmed = PositionableMessageBox.ConfirmYesNo(
    "Delete this item?",
    new Point(150, 150),
    "Confirm Delete");

if (confirmed)
{
    DeleteItem();
}

// OK/Cancel确认
bool proceed = PositionableMessageBox.ConfirmOkCancel(
    "Continue with operation?",
    new Point(200, 200),
    "Confirm");
```

### 完整API

```csharp
// 完整参数版本
DialogResult result = PositionableMessageBox.Show(
    text: "Your message here",
    caption: "Title",
    buttons: MessageBoxButtons.YesNoCancel,
    icon: MessageBoxIcon.Question,
    location: new Point(100, 100)
);

// 带Owner窗口（模态对话框）
DialogResult result = PositionableMessageBox.Show(
    owner: this,  // 父窗口
    text: "Your message here",
    caption: "Title",
    buttons: MessageBoxButtons.YesNo,
    icon: MessageBoxIcon.Warning,
    location: new Point(100, 100)
);
```

### 在IMessageService中使用

```csharp
public class MessageService : IMessageService
{
    // 标准MessageBox（屏幕中央）
    public void ShowInfo(string text, string caption = "")
    {
        MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // 定位MessageBox（使用PositionableMessageBox）
    public void ShowInfoAt(string text, Point location, string caption = "")
    {
        PositionableMessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information, location);
    }
}
```

## 实际应用场景

### 场景1：在控件旁边显示提示

```csharp
private void textBox1_Validating(object sender, CancelEventArgs e)
{
    if (string.IsNullOrWhiteSpace(textBox1.Text))
    {
        // 在TextBox下方显示错误
        Point location = textBox1.PointToScreen(new Point(0, textBox1.Height));
        PositionableMessageBox.ShowError(
            "Name is required!",
            location,
            "Validation Error");

        e.Cancel = true;
    }
}
```

### 场景2：在鼠标位置显示确认

```csharp
private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
{
    // 在鼠标当前位置显示确认对话框
    Point mousePos = Control.MousePosition;

    bool confirmed = PositionableMessageBox.ConfirmYesNo(
        "Open this item?",
        mousePos,
        "Confirm");

    if (confirmed)
    {
        OpenItem(e.RowIndex);
    }
}
```

### 场景3：在特定屏幕区域显示

```csharp
private void btnSave_Click(object sender, EventArgs e)
{
    try
    {
        SaveData();

        // 在按钮下方显示成功消息
        Point location = btnSave.PointToScreen(
            new Point(0, btnSave.Height + 5));

        PositionableMessageBox.ShowInfo(
            "Data saved successfully!",
            location,
            "Success");
    }
    catch (Exception ex)
    {
        PositionableMessageBox.ShowError(ex.Message, Control.MousePosition, "Error");
    }
}
```

## 性能和安全性

### 线程安全性

- ✅ Hook只在当前线程安装（`GetCurrentThreadId()`）
- ✅ Hook在使用后立即移除
- ✅ 使用`try-finally`确保Hook总是被移除

### 性能影响

- ✅ Hook只存在几毫秒（MessageBox显示期间）
- ✅ 不影响其他线程或窗口
- ✅ 与原生MessageBox性能几乎相同

### 错误处理

```csharp
try
{
    // 安装Hook并显示MessageBox
    result = MessageBox.Show(...);
}
finally
{
    // 无论如何都要移除Hook
    if (hookHandle != IntPtr.Zero)
    {
        UnhookWindowsHookEx(hookHandle);
    }
}
```

## 限制和注意事项

1. **仅支持Windows平台**
   - 使用Windows API，不支持跨平台
   - .NET Framework 4.8 / .NET 6+ Windows

2. **位置可能被调整**
   - Windows可能调整位置确保MessageBox完全可见
   - 如果指定位置超出屏幕，Windows会自动修正

3. **多显示器支持**
   - 使用屏幕坐标（Screen coordinates）
   - 确保位置在有效的显示器范围内

## 迁移指南

### 从PositionedMessageBox迁移

```csharp
// 旧代码
using (var dialog = new PositionedMessageBox(
    text, caption, buttons, icon, location))
{
    dialog.ShowDialog();
    return dialog.Result == DialogResult.Yes;
}

// 新代码（更简洁！）
return PositionableMessageBox.Show(
    text, caption, buttons, icon, location) == DialogResult.Yes;
```

### MessageService更新

```csharp
// 旧实现（使用PositionedMessageBox）
public void ShowInfoAt(string text, Point location, string caption = "")
{
    using (var dialog = new PositionedMessageBox(
        text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information, location))
    {
        dialog.ShowDialog();
    }
}

// 新实现（使用PositionableMessageBox）
public void ShowInfoAt(string text, Point location, string caption = "")
{
    PositionableMessageBox.Show(
        text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information, location);
}
```

## 总结

| 特性 | 旧方案（PositionedMessageBox） | 新方案（PositionableMessageBox） |
|------|------------------------------|---------------------|
| **外观** | 自定义样式 | 原生Windows样式 |
| **图标** | Unicode字符 | 系统图标 |
| **主题** | 不支持 | 自动支持系统主题 |
| **本地化** | 需手动处理 | 自动支持 |
| **键盘快捷键** | 需手动实现 | 自动支持 |
| **声音** | 无 | 系统声音 |
| **代码复杂度** | 高（自定义Form） | 低（Windows API） |
| **维护成本** | 高 | 低 |
| **用户体验** | 不一致 | 完全一致 |

**推荐:** 使用新的`PositionableMessageBox`基于Windows API Hook的实现。
