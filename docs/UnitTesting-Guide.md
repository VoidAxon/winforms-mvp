# 单元测试指南 - Unit Testing Guide

## 📖 概述

本文档展示如何为WinForms MVP应用编写单元测试，使用Mock服务注入来测试Presenter逻辑。

---

## 🎯 测试策略

### MVP三层测试策略

1. **Presenter测试**（本文重点） - 测试业务逻辑
2. **Model测试** - 测试数据验证和业务规则
3. **View测试**（可选） - 集成测试或手动测试

**为什么只需测试Presenter？**
- Presenter包含所有业务逻辑
- View只是UI渲染（WinForms自己测试过了）
- 通过Mock View和Mock Services，可以完全独立测试Presenter

---

## 🔧 Mock服务架构

### Mock服务层次结构

```
MockCommonServices (聚合所有Mock)
├── MockMessageService (记录消息调用)
├── MockDialogProvider (模拟对话框)
└── MockFileService (内存文件系统)
```

### Mock服务的作用

| Mock类 | 作用 | 关键功能 |
|--------|------|---------|
| **MockCommonServices** | 聚合所有常用服务 | 一次注入所有Mock |
| **MockMessageService** | 记录所有消息调用 | 验证ShowInfo/ShowWarning等是否被调用 |
| **MockDialogProvider** | 模拟文件对话框 | 控制对话框返回值 |
| **MockFileService** | 内存文件系统 | 无需真实文件系统 |
| **MockToDoView** | 模拟View | 记录方法调用，模拟View状态 |

---

## 📝 完整测试示例

### 步骤1：创建Mock服务

```csharp
public class ToDoDemoPresenterTests
{
    private MockCommonServices _mockServices;
    private MockToDoView _mockView;
    private ToDoDemoPresenter _presenter;

    public ToDoDemoPresenterTests()
    {
        SetupTest();
    }

    private void SetupTest()
    {
        // 1. 创建Mock服务
        _mockServices = new MockCommonServices();

        // 2. 创建Presenter，注入Mock服务
        _presenter = new ToDoDemoPresenter(_mockServices);

        // 3. 创建Mock视图
        _mockView = new MockToDoView();

        // 4. 附加视图并初始化
        _presenter.AttachView(_mockView);
        _presenter.Initialize();

        // 清除初始化调用记录
        _mockServices.Reset();
        _mockView.MethodCalls.Clear();
    }
}
```

**关键点**：
- ✅ 每个测试都有独立的Mock实例
- ✅ 构造函数中初始化，确保每个测试隔离
- ✅ 注入MockCommonServices而不是真实服务

### 步骤2：编写测试 - 验证View调用

```csharp
[Fact]
public void AddTask_WithValidText_AddsTaskToView()
{
    // Arrange - 准备测试数据
    _mockView.TaskText = "Buy groceries";

    // Act - 执行操作
    _presenter.Dispatch(ToDoDemoActions.AddTask);

    // Assert - 验证结果
    Assert.Contains("AddTaskToList(Buy groceries)", _mockView.MethodCalls);
    Assert.Equal(1, _mockView.TaskCount);
    Assert.Equal("Buy groceries", _mockView.GetTask(0));
}
```

**验证内容**：
- ✅ Presenter调用了View的方法（`AddTaskToList`）
- ✅ View的内部状态正确（TaskCount = 1）
- ✅ 数据正确传递

### 步骤3：验证服务调用

```csharp
[Fact]
public void AddTask_WithValidText_ShowsSuccessMessage()
{
    // Arrange
    _mockView.TaskText = "Buy groceries";

    // Act
    _presenter.Dispatch(ToDoDemoActions.AddTask);

    // Assert - 验证MessageService被调用
    Assert.True(_mockServices.MessageService.InfoMessageShown);
    Assert.True(_mockServices.MessageService.HasCall(
        MessageType.Info,
        messageContains: "Buy groceries"));
}
```

**验证内容**：
- ✅ Presenter调用了`MessageService.ShowInfo()`
- ✅ 消息内容包含任务名称
- ✅ Mock记录了所有调用

### 步骤4：控制Mock返回值

```csharp
[Fact]
public void RemoveTask_WithConfirmYes_RemovesTask()
{
    // Arrange
    _mockView.TaskText = "Task to delete";
    _presenter.Dispatch(ToDoDemoActions.AddTask);
    _mockView.SelectTask(0);

    // 控制Mock返回值
    _mockServices.MessageService.ConfirmYesNoResult = true;

    // Act
    _presenter.Dispatch(ToDoDemoActions.RemoveTask);

    // Assert
    Assert.True(_mockServices.MessageService.ConfirmDialogShown);
    Assert.Contains("RemoveSelectedTask()", _mockView.MethodCalls);
    Assert.Equal(0, _mockView.TaskCount);  // 任务被删除了
}

[Fact]
public void RemoveTask_WithConfirmNo_DoesNotRemoveTask()
{
    // Arrange
    _mockView.TaskText = "Task to keep";
    _presenter.Dispatch(ToDoDemoActions.AddTask);
    _mockView.SelectTask(0);

    // 用户点击"No"
    _mockServices.MessageService.ConfirmYesNoResult = false;

    // Act
    _presenter.Dispatch(ToDoDemoActions.RemoveTask);

    // Assert
    Assert.True(_mockServices.MessageService.ConfirmDialogShown);
    Assert.DoesNotContain("RemoveSelectedTask()", _mockView.MethodCalls);
    Assert.Equal(1, _mockView.TaskCount);  // 任务没有被删除
}
```

**验证内容**：
- ✅ 测试不同的用户选择分支
- ✅ 验证Presenter根据返回值执行不同逻辑

### 步骤5：集成测试

```csharp
[Fact]
public void CompleteWorkflow_AddEditSave_WorksCorrectly()
{
    // 1. Add first task
    _mockView.TaskText = "Task 1";
    _presenter.Dispatch(ToDoDemoActions.AddTask);

    // 2. Add second task
    _mockView.TaskText = "Task 2";
    _presenter.Dispatch(ToDoDemoActions.AddTask);

    // 3. Complete first task
    _mockView.SelectTask(0);
    _presenter.Dispatch(ToDoDemoActions.CompleteTask);

    // 4. Delete second task
    _mockView.SelectTask(1);
    _mockServices.MessageService.ConfirmYesNoResult = true;
    _presenter.Dispatch(ToDoDemoActions.RemoveTask);

    // 5. Save all changes
    _presenter.Dispatch(ToDoDemoActions.SaveAll);

    // Assert - verify final state
    Assert.Equal(1, _mockView.TaskCount);
    Assert.StartsWith("[Done]", _mockView.GetTask(0));
    Assert.False(_mockView.HasPendingChanges);

    // Verify messages shown
    Assert.True(_mockServices.MessageService.InfoMessageShown);
    Assert.True(_mockServices.MessageService.ConfirmDialogShown);
}
```

**验证内容**：
- ✅ 测试完整的用户工作流
- ✅ 验证多个操作的组合效果
- ✅ 确保状态正确传递

---

## 🚀 运行测试

### 命令行运行

```bash
# 运行所有测试
dotnet test src/WindowsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj

# 详细输出
dotnet test src/WindowsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --logger "console;verbosity=detailed"

# 只运行特定测试类
dotnet test --filter "FullyQualifiedName~ToDoDemoPresenterTests"

# 只运行特定测试方法
dotnet test --filter "FullyQualifiedName~AddTask_WithValidText_AddsTaskToView"
```

### Visual Studio运行

1. 打开测试资源管理器（Test Explorer）
2. 点击"Run All"运行所有测试
3. 右键单击测试 → "Debug Test"进行调试

---

## 📊 测试结果示例

```
テストの実行に成功しました。
テストの合計数: 14
     成功: 14
合計時間: 2.1秒

测试列表：
✅ AddTask_WithValidText_AddsTaskToView
✅ AddTask_WithValidText_ClearsTaskText
✅ AddTask_WithValidText_ShowsSuccessMessage
✅ AddTask_WithEmptyText_ShowsWarning
✅ AddTask_WithWhitespaceText_ShowsWarning
✅ RemoveTask_WithConfirmYes_RemovesTask
✅ RemoveTask_WithConfirmNo_DoesNotRemoveTask
✅ CompleteTask_MarksTaskAsCompleted
✅ SaveAll_ClearsPendingChanges
✅ SaveAll_ShowsSuccessMessage
✅ CompleteWorkflow_AddEditSave_WorksCorrectly
✅ SelectionChanged_TriggersCanExecuteUpdate
✅ Presenter_UsesInjectedMockServices
✅ MultipleTests_DontInterfere
```

---

## 🛠️ MockCommonServices API

### 创建和重置

```csharp
// 创建Mock服务
var mockServices = new MockCommonServices();

// 重置所有Mock状态（清除调用记录）
mockServices.Reset();

// 访问各个Mock服务
mockServices.MessageService  // MockMessageService
mockServices.DialogProvider  // MockDialogProvider
mockServices.FileService     // MockFileService
```

### MockMessageService API

```csharp
// 控制返回值
mockServices.MessageService.ConfirmYesNoResult = true;  // 默认true
mockServices.MessageService.ConfirmOkCancelResult = true;
mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Yes;

// 验证调用
Assert.True(mockServices.MessageService.InfoMessageShown);
Assert.True(mockServices.MessageService.WarningMessageShown);
Assert.True(mockServices.MessageService.ErrorMessageShown);
Assert.True(mockServices.MessageService.ConfirmDialogShown);

// 检查特定消息
Assert.True(mockServices.MessageService.HasCall(
    MessageType.Info,
    messageContains: "保存成功"));

// 获取调用记录
var calls = mockServices.MessageService.Calls;  // List<MessageCall>
var lastCall = mockServices.MessageService.GetLastCall();

// 清除记录
mockServices.MessageService.Clear();
```

### MockDialogProvider API

```csharp
// 控制对话框返回值
mockServices.DialogProvider.OpenFileDialogResult = "C:\\test.txt";
mockServices.DialogProvider.SaveFileDialogResult = "C:\\output.txt";
mockServices.DialogProvider.FolderBrowserDialogResult = "C:\\myfolder";

// 返回空表示用户取消
mockServices.DialogProvider.OpenFileDialogResult = null;  // 用户取消
```

### MockFileService API

```csharp
// 添加内存文件
mockServices.FileService.AddFile("C:\\test.txt", "file content");

// 读取
var content = mockServices.FileService.ReadAllText("C:\\test.txt");

// 写入
mockServices.FileService.WriteAllText("C:\\output.txt", "data");

// 检查存在
bool exists = mockServices.FileService.Exists("C:\\test.txt");

// 清除所有文件
mockServices.FileService.Clear();
```

### MockView API

```csharp
// 模拟用户操作
_mockView.TaskText = "New task";
_mockView.SelectTask(0);
_mockView.ClearSelection();

// 验证方法调用
Assert.Contains("AddTaskToList(Buy groceries)", _mockView.MethodCalls);
Assert.Contains("UpdateStatus(Ready)", _mockView.MethodCalls);

// 验证状态
Assert.True(_mockView.HasSelectedTask);
Assert.True(_mockView.HasPendingChanges);
Assert.Equal(3, _mockView.TaskCount);

// 清除记录
_mockView.MethodCalls.Clear();
_mockView.StatusMessages.Clear();
```

---

## ✅ 最佳实践

### 1. 每个测试独立

```csharp
// ✅ 好 - 每个测试都创建新的Mock
public class MyTests
{
    private MockCommonServices _mockServices;

    public MyTests()
    {
        _mockServices = new MockCommonServices();  // 每个测试都是新实例
    }
}

// ❌ 避免 - 共享Mock导致测试相互影响
private static MockCommonServices _mockServices = new MockCommonServices();
```

### 2. 使用AAA模式

```csharp
[Fact]
public void TestName()
{
    // Arrange - 准备数据
    _mockView.TaskText = "Test";
    _mockServices.MessageService.ConfirmYesNoResult = true;

    // Act - 执行操作
    _presenter.Dispatch(MyActions.DoSomething);

    // Assert - 验证结果
    Assert.True(_mockServices.MessageService.InfoMessageShown);
}
```

### 3. 测试命名清晰

```csharp
// ✅ 好 - 清晰描述测试内容
AddTask_WithValidText_AddsTaskToView()
AddTask_WithEmptyText_ShowsWarning()
RemoveTask_WithConfirmYes_RemovesTask()

// ❌ 避免 - 含糊不清
Test1()
TestAddTask()
TestSomething()
```

### 4. 一个测试一个断言主题

```csharp
// ✅ 好 - 专注于一个验证点
[Fact]
public void AddTask_ShowsSuccessMessage()
{
    _presenter.Dispatch(AddTask);
    Assert.True(_mockServices.MessageService.InfoMessageShown);
}

[Fact]
public void AddTask_AddsToView()
{
    _presenter.Dispatch(AddTask);
    Assert.Equal(1, _mockView.TaskCount);
}

// ⚠️ 可接受 - 相关断言
[Fact]
public void AddTask_UpdatesViewAndShowsMessage()
{
    _presenter.Dispatch(AddTask);
    Assert.Equal(1, _mockView.TaskCount);
    Assert.True(_mockServices.MessageService.InfoMessageShown);
}
```

### 5. 清理测试状态

```csharp
// ✅ 好 - 清除初始化时的调用
private void SetupTest()
{
    // ... 初始化代码 ...

    // 清除初始化时产生的调用记录
    _mockServices.Reset();
    _mockView.MethodCalls.Clear();
}
```

---

## 🎓 总结

### 核心优势

1. **无需UI线程** - 测试在后台线程运行，快速
2. **完全隔离** - 不依赖真实文件系统、数据库等
3. **可重复** - 每次运行结果一致
4. **易调试** - 可以断点调试Presenter逻辑

### Mock服务的价值

- ✅ **记录调用** - 验证Presenter是否调用了正确的服务方法
- ✅ **控制返回** - 模拟不同的用户选择和系统状态
- ✅ **无副作用** - 不会弹出真实对话框或修改文件
- ✅ **快速执行** - 14个测试在2秒内完成

### 注入Mock的关键

```csharp
// 生产环境 - 使用默认服务
var presenter = new MyPresenter();  // 自动使用CommonServices.Default

// 测试环境 - 注入Mock
var mockServices = new MockCommonServices();
var presenter = new MyPresenter(mockServices);  // 使用Mock服务
```

**这就是依赖注入的价值** - 同一个Presenter类，生产环境用真实服务，测试环境用Mock服务！

---

## 📚 参考资料

- **测试代码位置**: `src/WindowsMVP.Samples.Tests/Presenters/ToDoDemoPresenterTests.cs`
- **Mock服务位置**: `src/WindowsMVP.Samples.Tests/Mocks/`
- **服务注入文档**: `docs/ServiceInjectionPatterns.md`
- **xUnit文档**: https://xunit.net/

Happy Testing! 🧪✨
