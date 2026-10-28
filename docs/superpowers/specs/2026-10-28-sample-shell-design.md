# 示例主画面重做（Sample Shell）设计

日期：2026-10-28　状态：已确认（2026-10-28），第 1 步实施中

## 目标

把 `samples/Kibinago.Samples` 的启动器（按钮列表 → 各自开窗口）换成一个**最大化的主画面**：左边侧边栏列大项，右边 tab 放该大项的说明、demo 和关键代码。所有特性（包括 #58–#70 新加的）都要在示例里看得到。

用户决定（2026-10-28）：
- 最大化（保留标题栏），不是无边框全屏。
- 画面文字用日语。
- 现有 demo 改写成 `UserControl` + `ControlPresenterBase`，嵌入 tab；关于"窗口本身"的特性在 tab 里放说明和「開く」按钮。
- 每个大项除 demo 外还有「説明」（含 wiki 链接）和「コード」tab。
- 关于窗口本身的 demo：tab 里只放说明和按钮，按钮打开原来的 Form（不传 owner：模态时 WinForms 以激活窗口为 owner；非模态窗口独立，示例可接受）。
- Customization：另开进程（`Kibinago.Samples.exe --profile custom`），运行中的主画面从不重新配置。

## 主画面结构

```
┌ Kibinago サンプル ──────────────────────────────────────── _ □ × ┐
│ ┌──────────────┐ ┌ 説明 ┬ ToDo ┬ CheckBox ┬ … ┬ コード ┐            │
│ │ はじめに     │ │                                      │            │
│ │▶ViewAction   │ │   （选中 tab 的 UserControl）        │            │
│ │ 非同期・処理中│ │                                      │            │
│ │ ウィンドウ    │ │                                      │            │
│ │ …            │ │                                      │            │
│ └──────────────┘ └──────────────────────────────────────┘            │
└──────────────────────────────────────────────────────────────────────┘
```

- `ShellForm`（`IShellView : IWindowView`）+ `ShellPresenter`（`WindowPresenterBase`）。`Program` 里 `MvpApplication.Configure(...)` 一次，然后 `MvpApplication.Run(shell)`；以后不再有 demo 改全局 `ServiceLocator`。
- 侧边栏：`ListBox`（自绘，大项名 + 一行说明）。选择 → `ShellPresenter` → `View.ShowCategory(category)`。
- 右边：`TabControl`。每个 tab 是一个 `UserControl`；**第一次选中时才创建**并由 `ShellPresenter` 用 `AttachChild` 挂上，**切换大项时释放**（演示 `AttachChild` 的句柄释放、`Lifetime` 取消）。

### tab 的生命周期（codex 讨论后补充）

- View 接口是语义操作：`IXxxView AddDemo(DemoId)`、`SelectDemo`、`ClearDemos()`；控件由 View 创建，`ShellPresenter` 和目录表不碰 WinForms 类型、不认识具体的 Form/Control。
- 每个创建的 demo 保留一个 `AttachChild` 句柄。切换大项：先释放所有句柄（presenter），再由 View 释放、移除控件（`AttachChild` 不释放 View）。初始化失败时也要释放控件。
- 同一大项内切 tab 保留实例；回到某大项时重新创建。画面上用日语提示「大項目を切り替えるとデモはリセットされます」。
- demo 的计时器、订阅都随 presenter 释放（`DisposeWith` / `SubscribeViewEvent`）；不再依赖 `FormClosed`（如 ToastDemo 的轮询计时器）。异步处理要看 `Lifetime`。
- tab 里的 `ShowBusy()` 会锁住整个主画面（包括侧边栏），demo 里写明，并用有时限、可取消的处理。MDI 的 demo 单独开窗口，演示只锁子窗口。
- 变更跟踪的 demo：切换大项时直接丢弃编辑（子 presenter 不参与关闭确认），demo 里说明这一点。
- toast 等服务属于应用，tab 释放时不释放它们。
- 「説明」tab：日语简短说明 + 关联 FEATURES/wiki 链接（`LinkLabel`，打开 GitHub 上的 wiki 页）。
- 「コード」tab：左边文件列表、右边只读等宽文本，默认显示最后选中的 demo 的文件。显示的是 demo 自己的 `.cs` 源码（编译时以 `EmbeddedResource` 嵌入，`LogicalName` = 规范化的相对路径，排除 bin/obj/生成文件；和编译时的源码一致）。包括 interface、模型、设置、子对话框、相关的 Designer.cs。懒加载。加一个测试：目录表里的每个资源都存在。

大项目录写成一个数据表（`SampleCatalog`：大项 → 说明、wiki 链接、demo 列表（标题 + 工厂）、代码文件列表），主画面只读这张表。

## 大项与内容（★ = 新 demo）

| 大项 | tab |
|---|---|
| はじめに | 概要（Kibinago 是什么、示例怎么看、特性对照表）、MVP 比較（MVPComparisonDemo） |
| 起動・DI | 説明 + コード：`MvpApplication.Configure`、服务注册与优先级、View 映射、模块、`AddKibinago` / `IPresenterFactory`；链接 `MultiProjectDemo.*` |
| ViewAction | ToDo、CheckBox/RadioButton、一括バインド、値バインド、明示イベント（+パラメーター）、★ミドルウェア、★型付きアクション |
| 非同期・処理中 | 非同期操作（AsyncDemo）、★ShowBusy（同期 / await / ワーカー、オーバーレイ、フォント）、Debounce/Throttle |
| ウィンドウ | Navigator（モーダル・モードレス・結果・キー）、閉じる確認（WindowClosingDemo）、★PrepareWindow、レガシーポップアップ、★MDI と ShowBusy ※全部是「開く」按钮 |
| ダイアログ・通知 | MessageBox、トースト、アンカー表示、★ファイル/フォルダー/色ダイアログ（+ フォント・印刷は View 側の例） |
| バインド | ★モデルとコントロールの双方向バインド（null 許容日付、コンボ、ラジオグループ、`NotifyingObject`） |
| 入力検証 | 検証（ValidationDemo）、★FirstError + FocusError |
| 変更追跡 | ★ChangeTracker（2 つの使い方、Ignore、保存確認） |
| 画面の組み立て | ★AttachChild（動的な行・タブ）、マスター/詳細、Cascade、★Cascade.Combine（3 段） |
| 一覧 | ★SortableBindingList（見出しクリック・複数列・KeepSorted）、★行の移動 |
| Presenter 間の連携 | サービス経由、EventAggregator、注文管理（ComplexInteractionDemo）——定位为对比：同一画面的面板之间推荐共享模型 |
| カスタマイズ | 設定の書き方（説明 + コード + 「この設定で別プロセスを起動」(`--profile custom`)）、ロギング |
| テスト・ツール | 説明 + コード：`PresenterTestHost`、fake、アナライザー（MVP001–018）、テンプレート |
| 総合アプリ | Email（「開く」按钮，完整应用） |

## 改写原则

- 嵌入 tab 的 demo：`IXxxView : IActionableView`（用 ViewAction 时；否则 `IViewBase`），`XxxControl : UserControl`，`XxxPresenter : ControlPresenterBase<IXxxView>`。Presenter 逻辑尽量不动，名字尽量保留；view 事件订阅改成 `SubscribeViewEvent`。
- 本来就是关于窗口的 demo（Navigator、关闭、PrepareWindow、MDI、LegacyPopup、Email）保持 Form，tab 里放说明 + 「開く」。
- `tests/Kibinago.Samples.Tests` 引用了 EmailDemo、ToDoDemo、WindowClosingDemo、LegacyPopupDemo、AnchoredMessageDemo、LoggingDemoExample 的类型；改写时同步改测试。文字改成日语时，断言英语字符串的测试要一起改。
- 旧的 `SampleLauncherForm`、`Form1`、各 demo 自带的 `Program.Run` 删掉。

## 分几步（每步一个 PR）

1. **主画面骨架**：`ShellForm` / `ShellPresenter` / `SampleCatalog`、説明 tab、コード tab、启动配置一次、`--profile custom`（Customization 在这一步就改成另开进程）；先搬 ToDo 一个 demo 验证方式；其余大项暂时放「開く」按钮指向旧 Form（不丢功能）。测试：懒创建、反复切换大项、释放与 `Lifetime` 取消、资源完整。
2. **搬现有 demo**：按大项逐个改写成 UserControl（ViewAction → 非同期 → メッセージ → 検証 → 画面の組み立て → 連携 → はじめに）。
3. **补新特性 demo**（★ 的部分）。
4. 收尾：删旧启动器、更新 README/wiki 里提到示例的地方。

每步都在 Windows 上由用户确认后再进下一步。

## 另记：框架的缺口（不在本设计范围）

`IWindowNavigator` 的 `owner` 是 `IWindowView`，窗口里的面板（子 presenter，`IViewBase`）无法把"所在的窗口"作为非模态子窗口的 owner。以后单独讨论是否让 owner 接受 `IViewBase`（框架内部找所在 Form）。
