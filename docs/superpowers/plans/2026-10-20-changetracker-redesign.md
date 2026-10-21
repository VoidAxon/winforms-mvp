# ChangeTracker 重新设计 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 `ChangeTracker<T>` 只做变更检测：跟踪 Presenter 自己的对象（引用）+ 一张照片，支持"同一个对象"和"每次 new"两种写法，删除数据容器式的 API。

**Architecture:** Task 1 重写 `ChangeTracker<T>` 本体并迁移所有 Core 测试（API 删除会让旧测试无法编译，所以必须一起做）；Task 2 把 EmailDemo 改为写法 2；Task 3 更新其余示例与文档。

**Tech Stack:** C#（Core: netstandard2.0），xUnit 2.9.3（测试项目 net48）。

**Spec:** `docs/superpowers/specs/2026-10-20-changetracker-redesign-design.md`（执行者必须先读）

## Global Constraints

- 分支 `changetracker-redesign`（已从 master 建好），完成后开 PR；不打 tag、不发布。
- **安全规则**：不得在 Windows 侧执行任何东西（不调用 `/mnt/c/...` 的 `dotnet.exe`、`powershell.exe`、`cmd.exe`，不启动示例，不截屏）。net48 测试只在 GitHub Actions 上跑（推送分支 + PR 后看 `gh run watch`）；推送前须征得用户同意（本计划完成后再推送）。
- 本地检查（Linux）：
  1. `dotnet build winforms-mvp.sln --no-incremental`：只允许原有的 8 个 xUnit1031 警告；
  2. Core 测试：在 git 忽略的 `.superpowers/sdd/2026-10-20-changetracker-redesign/coretests/` 重建临时项目（net10.0，`AssemblyName` = `WinformsMVP.Samples.Tests` 以获得 `InternalsVisibleTo`，引用 `src/WinformsMVP.Core`，xunit 2.9.3 / xunit.runner.visualstudio 3.1.4 / Microsoft.NET.Test.Sdk 17.13.0，`Nullable` disable），用 `files.props` 链接这些测试文件：`Services/DefaultServiceProviderTests.cs`、`Services/ServiceLocatorTests.cs`、`Services/ServiceLocatorBuiltInsCoreTests.cs`、`Common/ChangeTrackerTests.cs`、`Common/ChangeTrackerContentCompareTests.cs`、`Common/ChangeTrackingPolicyTests.cs`、`Common/ChangeTrackerIgnoreTests.cs`、`Common/ObjectClonerTests.cs`、`Common/ObjectComparerTests.cs`、`Common/EventAggregatorTests.cs`、`Common/EventAggregatorLoggerTests.cs`、`Presenters/TrackChangesTests.cs`、`TestHelpers/CapturingLogger.cs`，`cd` 进去 `dotnet test`；
  3. `dotnet test tests/WinformsMVP.Analyzers.Tests/WinformsMVP.Analyzers.Tests.csproj`。
  （`CommonHardeningTests.cs` 用到 WinForms，不能放进临时项目，只做编译检查。）
- 每个任务提交后运行 `codex review --commit <sha>`，处理意见后复核。
- 提交信息结尾：`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。
- 不兼容变更写进 `CHANGELOG.md` 的 `[Unreleased]`（日文，沿用现有格式）。

## Review Focus

1. **写法 2 里误用 `AcceptChanges()`（无参数）**：照片变成过时的初始对象，下一次 `IsChangedWith` 报告有改动。无法在运行时阻止，靠 XML 注释和文档说明。→ Task 1 测试 `AcceptChangesWithValue_ThenSameContent_IsNotChanged` 钉住正确用法；Task 3 文档写明。
2. **`AcceptChanges(x)` 之后调用方继续修改 x**：照片必须独立，不受影响。→ Task 1 `AcceptChangesWithValue_SnapshotIsIndependentOfValue`。
3. **异步保存期间用户把内容改回原值**：`AcceptChanges(保存的数据)` 之后必须仍是有改动。→ Task 1 `AcceptChangesWithSaved_EditBackToOriginalDuringSave_IsStillChanged`。
4. **传入 null**：构造函数、`AcceptChanges(null)`、`IsChangedWith(null)` 都抛 `ArgumentNullException`，不能当成"无改动"。→ Task 1 `NullArguments_Throw`。
5. **跟踪对象是 `List<T>` 根**：原列表的增、删、改都要检测到。→ Task 1 `ListRoot_AddRemoveEdit_AreDetected`。

---

### Task 1: ChangeTracker 本体 + Core 测试迁移

**Files:**
- Modify: `src/WinformsMVP.Core/Common/ChangeTracker.cs`、`src/WinformsMVP.Core/MVP/Presenters/PresenterBase.cs`（只改 `TrackChanges` 的 XML 注释）
- Modify tests: `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerTests.cs`、`ChangeTrackingPolicyTests.cs`、`ChangeTrackerContentCompareTests.cs`、`ChangeTrackerIgnoreTests.cs`、`CommonHardeningTests.cs`（ChangeTracker 区域）、`tests/WinformsMVP.Samples.Tests/Presenters/TrackChangesTests.cs`
- Create test: `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerUsageStyleTests.cs`

**Interfaces:**
- Produces（`ChangeTracker<T> : IChangeTracking where T : class`，类改为 `sealed`）：
  ```csharp
  public ChangeTracker(T value);
  public ChangeTracker(T value, Func<T, T, bool> comparer);
  public ChangeTracker(T value, IEqualityComparer<T> comparer);
  public ChangeTracker(T value, ChangeTrackingPolicy<T> policy);
  public bool IsChanged { get; }            // !compare(snapshot, tracked)
  public void AcceptChanges();              // snapshot = clone(tracked)
  public bool IsChangedWith(T value);       // !compare(snapshot, value)；value 为 null 抛 ArgumentNullException
  public void AcceptChanges(T value);       // snapshot = clone(value)；value 为 null 抛 ArgumentNullException；tracked 不变
  // Ignores / Ignore / Ignore<TOwner> / IgnoreMember / IgnoreType：签名不变
  ```
- 删除：`CurrentValue`、`UpdateCurrentValue`、`RejectChanges`、`GetOriginalValue`、`IsChangedChanged`、`IRevertibleChangeTracking`、内部 `_lock` 与 `Mutate` / `_lastReportedIsChanged`。字段：`_tracked`（构造时传入的引用，不复制）、`_snapshot`（构造时 `clone(value)`）。
- `Ignore*` 方法保留"抛出 / 返回 this"的行为，只是不再触发事件。

- [ ] **Step 1: 重建 Core 临时测试项目**（见 Global Constraints），跑一次，记录基线（预期 161 个通过）。
- [ ] **Step 2: 写失败测试** `ChangeTrackerUsageStyleTests.cs`（模型：`class Order { string Name; List<string> Tags = new List<string>(); }`）
  - 写法 1：
    - `TracksTheObjectItself_InPlaceEditIsChanged`：`var o = new Order{Name="a"}; var t = new ChangeTracker<Order>(o); o.Name = "b"; Assert.True(t.IsChanged);`
    - `EditBackToOriginal_IsNotChanged`：改成 "b" 再改回 "a" → `False`。
    - `AcceptChanges_ThenIsNotChanged_AndLaterEditsAreSeen`：`o.Name="b"; t.AcceptChanges(); Assert.False(t.IsChanged); o.Name="c"; Assert.True(t.IsChanged);`
    - `AcceptChangesWithSaved_EditDuringSave_IsStillChanged`：`var saved = new Order{Name="b"}; o.Name="c"; t.AcceptChanges(saved); Assert.True(t.IsChanged); o.Name="b"; Assert.False(t.IsChanged);`
    - `AcceptChangesWithSaved_EditBackToOriginalDuringSave_IsStillChanged`：基准 "a"，保存 `saved{Name="b"}`，期间 `o.Name="a"`，`t.AcceptChanges(saved)` → `Assert.True(t.IsChanged)`。
    - `ListRoot_AddRemoveEdit_AreDetected`：`ChangeTracker<List<Order>>`，分别 `Add`、`RemoveAt(0)`、`list[0].Name = "x"` 后 `IsChanged == true`（每种情况新建 tracker）。
  - 写法 2：
    - `IsChangedWith_NewInstance_ComparesContent`：`t = new ChangeTracker<Order>(new Order{Name="a"})`；`IsChangedWith(new Order{Name="a"}) == false`，`IsChangedWith(new Order{Name="b"}) == true`。
    - `AcceptChangesWithValue_ThenSameContent_IsNotChanged`：`t.AcceptChanges(new Order{Name="b"}); Assert.False(t.IsChangedWith(new Order{Name="b"})); Assert.True(t.IsChangedWith(new Order{Name="a"}));`
    - `AcceptChangesWithValue_SnapshotIsIndependentOfValue`：`var v = new Order{Name="b"}; t.AcceptChanges(v); v.Name="z"; Assert.False(t.IsChangedWith(new Order{Name="b"}));`
    - `AcceptChangesWithValue_DoesNotChangeTheTrackedObject`：`var o = new Order{Name="a"}; t = new ChangeTracker<Order>(o); t.AcceptChanges(new Order{Name="b"}); Assert.Equal("a", o.Name); Assert.True(t.IsChanged);`
    - `IsChangedWith_HasNoSideEffects`：调用 `IsChangedWith(new Order{Name="z"})` 后 `IsChanged == false`、`IsChangedWith(new Order{Name="a"}) == false`。
  - 共通：
    - `Snapshot_IsIndependentOfTheTrackedObject`：`o.Tags.Add("t")` 后 `IsChanged == true`（构造时照片是深拷贝）。
    - `NullArguments_Throw`：`new ChangeTracker<Order>(null)`、`t.AcceptChanges(null)`、`t.IsChangedWith(null)` 均 `ArgumentNullException`。
    - `ImplementsIChangeTrackingOnly`：`Assert.IsAssignableFrom<IChangeTracking>(t); Assert.False(t is IRevertibleChangeTracking);`
- [ ] **Step 3: 运行确认失败** — 临时项目 `dotnet test`，预期多个断言失败（构造仍复制、`AcceptChanges(T)` 仍替换当前值、仍实现 `IRevertibleChangeTracking`）。
- [ ] **Step 4: 实现** 上述接口。XML 注释写明两种写法、各自的"判断 / 确认"成对关系与保证（`AcceptChanges()` 后 `IsChanged == false`；`AcceptChanges(x)` 后 `IsChangedWith(x) == false`）、写法 2 不要用无参 `AcceptChanges()`、串行使用（UI 线程）约定、"只做检测，数据归调用方"。类级注释删除"线程安全"的表述。
- [ ] **Step 5: 迁移旧测试**（编译通过且语义正确，不是简单删除）：
  - `ChangeTrackerTests.cs`：`Constructor_CreatesIndependentCopy` 改为 `Constructor_TracksTheObject_SnapshotIsIndependent`（修改原对象后 `IsChanged == true`）；删除 `UpdateCurrentValue`、`IsChangedChanged Event`、`RejectChanges`、`Thread Safety` 区域；`AcceptChanges`、`Deep Copy`、`IsChangedWith`、`Custom Comparer`、`IsChanged Caching` 区域改为修改传入的对象（不再读写 `CurrentValue`）；`GetOriginalValue_ReturnsIndependentCopy` 删除。
  - `ChangeTrackingPolicyTests.cs`、`ChangeTrackerContentCompareTests.cs`、`TrackChangesTests.cs`、`ChangeTrackerIgnoreTests.cs`：所有 `tracker.CurrentValue.X = ...` 改为修改构造时传入的对象；`RejectChanges` 断言删除或改为 `IsChangedWith`；`Ignore_RaisesIsChangedChanged_WhenTheDirtyStateFlips` 改为 `Ignore_AfterAnEdit_MakesItNotChanged`（原地修改被忽略的成员后 `Ignore` → `IsChanged == false`）。
  - `CommonHardeningTests.cs` 的 ChangeTracker 区域：`ChangeTracker_InPlaceEdit_IsSeenByIsChanged` 改为修改传入对象；删除两个 `IsChangedChanged` 测试。
- [ ] **Step 6: 运行通过** — 临时项目全部通过；`dotnet build winforms-mvp.sln --no-incremental` 只剩 8 个 xUnit1031 警告（此时 EmailDemo 会编译失败，属于 Task 2：先在 EmailDemo 中做**最小**的编译修复——`UpdateCurrentValue(x)` → 暂存到字段并在判断处用 `IsChangedWith`，`RejectChanges()` → `AcceptChanges(当前画面的内容)`，事件订阅删除并在数据变化处直接设置 `View.IsDirty`——完整改写在 Task 2）；分析器测试通过。
- [ ] **Step 7: 提交** — `feat(core)!: ChangeTracker tracks the caller's object and only detects changes`；`codex review --commit <sha>`。

---

### Task 2: EmailDemo 改为写法 2

**Files:**
- Modify: `samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs`、`samples/WinformsMVP.Samples/EmailDemo/Models/EmailMessage.cs`（第 11 行附近提到 `RejectChanges` 的注释）
- Modify test: `tests/WinformsMVP.Samples.Tests/Presenters/ComposeEmailPresenterTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `IsChangedWith(T)`、`AcceptChanges(T)`。
- Produces（Presenter 私有）：`private sealed class EmailEditState { public string To; public string Subject; public string Body; }`（用自动属性）；`private EmailEditState ReadEditState()`（从 View 读三个字段）；`private bool HasUnsavedChanges => _changeTracker.IsChangedWith(ReadEditState());`；`ChangeTracker<EmailEditState> _changeTracker`。

- [ ] **Step 1: 写失败测试**（`ComposeEmailPresenterTests`，沿用现有 mock view / mock repository 的写法）
  - `SaveDraft_EditDuringSave_RemainsDirty`：让 mock repository 的 `SaveDraftAsync` 返回一个由测试控制的 `TaskCompletionSource`；派发 SaveDraft 后修改 `View.Subject` 并触发 DataChanged，再完成保存 → `Assert.True(_mockView.IsDirty)`。
  - `SaveDraft_NoEditDuringSave_BecomesClean`：同上但保存期间不修改 → `Assert.False(_mockView.IsDirty)`。
  - `Discard_AfterConfirm_ClosesWithoutSecondPrompt`：修改后派发 Discard，`ConfirmYesNo` 返回 `true` → `host.CloseRequested`，且 `host.CanClose()` 为 `true`（不再提示；`MockMessageService` 的确认调用只发生一次）。
  - 现有依赖 `IsChangedChanged` 的断言（第 425 行附近的注释）改为断言 `View.IsDirty` 的最终值。
- [ ] **Step 2: 编译确认**（WinForms 测试只能编译，RED 在 CI 上确认；本地只确认编译）。
- [ ] **Step 3: 实现**
  - `OnInitialize`：显示到画面后 `_changeTracker = new ChangeTracker<EmailEditState>(ReadEditState())`（或 `TrackChanges(ReadEditState())`）；初始 `View.IsDirty = false`。
  - DataChanged：`View.IsDirty = HasUnsavedChanges; Dispatcher.RaiseCanExecuteChanged();`
  - SaveDraft：`var submitted = ReadEditState();` 用它生成请求；成功后 `_changeTracker.AcceptChanges(submitted); View.IsDirty = HasUnsavedChanges;`，返回 `!View.IsDirty`。删除手写的"保存期间画面是否变化"比较。
  - Send 成功：`_changeTracker.AcceptChanges(sentState)` 后 `RequestClose(true, Ok)`。
  - Discard：确认后 `_changeTracker.AcceptChanges(ReadEditState())`，再 `RequestClose(Cancel)`。
  - `CanCloseAsync`、各 `canExecute`：`_changeTracker.IsChanged` 全部改为 `HasUnsavedChanges`。
- [ ] **Step 4: 本地检查**（编译 + 临时项目 + 分析器）。
- [ ] **Step 5: 提交** — `samples(email): track an edit-state projection with IsChangedWith / AcceptChanges(x)`；`codex review`。

---

### Task 3: 其余示例与文档

**Files:**
- Modify: `samples/WinformsMVP.Samples/CustomizationDemo/CustomizationDemoPresenter.cs`（已是写法 2，只核对注释）
- Modify: `wiki/Reference-ChangeTracker.md`（API 表、两种写法各一个完整例子与对照表、删除 `RejectChanges` / `UpdateCurrentValue` / `GetOriginalValue` / `IsChangedChanged` / "スレッドセーフ" 的说明、修正第 370 行附近"Push 关闭会跳过 `CanClose`"的错误描述）、`wiki/HowTo-Handle-Window-Closing.md`、`wiki/Tutorial-Building-Your-First-App.md`（`IsChangedChanged` 订阅改为在操作处理里刷新 `UpdateDirtyState()`）、`wiki/HowTo-Customize-Framework.md`（如提到这些 API）、`CLAUDE.md`（ChangeTracker 一节）、`CHANGELOG.md`
- Update: 使用指南 Artifact https://claude.ai/artifact/Wm6sXUYXdTJRZ8bc3Runvy（用 Artifact 工具 `action: "read"` 取回后修改，第 3 步与"变更追踪"一节改为两种写法）

- [ ] **Step 1:** `grep -rn "CurrentValue\|UpdateCurrentValue\|RejectChanges\|GetOriginalValue\|IsChangedChanged\|IRevertibleChangeTracking" --include=*.cs --include=*.md . | grep -v "/obj/\|/bin/\|docs/superpowers/"`，逐一更新（CHANGELOG 迁移表除外）。预期最终只剩 CHANGELOG 中的迁移说明。
- [ ] **Step 2:** CHANGELOG `[Unreleased]`：Changed（BREAKING）一条说明新语义与两种写法；Removed 列出删除的 API；迁移表追加：`CurrentValue` → 调用方自己的对象；`UpdateCurrentValue(x)` → `IsChangedWith(x)`（写法 2）；`AcceptChanges(x)` 含义变化；`RejectChanges()` → 关闭前 `AcceptChanges()` / `AcceptChanges(当前内容)`，留在画面上撤销则重新读取；`IsChangedChanged` → 在数据变化处读 `IsChanged`；`GetOriginalValue()` → 调用方自己保留。并修正 `[Unreleased]` 里已有的、提到 `UpdateCurrentValue` / `RejectChanges` / `IsChangedChanged` 的旧条目。
- [ ] **Step 3:** 本地检查 + `tests/templates/verify-templates.sh`。
- [ ] **Step 4: 提交** — `docs/samples: ChangeTracker detection-only API, two usage styles`；`codex review`。
- [ ] **Step 5: 使用指南 Artifact**（`action: "read"` 取回线上版本后修改，中日两种语言都改）：
  - 第 3 步与"变更追踪"一节改为新 API，两种写法各一个例子（写法 1：`IsChanged` / `AcceptChanges()`；写法 2：`IsChangedWith(x)` / `AcceptChanges(x)`），注明写法 2 不要用无参 `AcceptChanges()`。
  - 新增一节"选择被动视图还是监督控制器"：Fowler / 微软 p&p 的分工（简单同步交给数据绑定、Presenter 负责操作和复杂画面逻辑）、取舍对比表、与本框架功能的对应表；同一个"客户编辑"画面分别写两种模式的 View 接口 / Form / Presenter（监督控制器版：`void ShowCustomer(Customer)` + `textBox.Bind(model, x => x.Name)`，Model 继承 `NotifyingObject`，ChangeTracker 写法 1，`Validation.Validate(_customer)`）；注明"五步做对话框"用的是被动视图。注明数据绑定现有的限制（类型转换失败没有提示）。
  - ViewAction 一节新增"第三方控件（C1 Input 等）"：未注册时退回 `Control.Click`；用 `RegisterStrategy<T>(attach, detach)` 注册值变化事件，非 `EventHandler` 事件用 `RegisterStrategy<T>(Func<T, Delegate, Action>)`；`Add(action, control, c => 值)` 带值；`canExecute` 的可用切换对所有 `Control` 有效；建议应用里写 `AppActionBinder : ViewActionBinder` 在构造函数集中注册。C1 的事件名注明"以所用版本的文档为准"（经典 `C1.Win.C1Input` 为 `ValueChanged`）。
  - 目录加入新的两节；发布到原链接。
