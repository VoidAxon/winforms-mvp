# HowTo: 従来の WinForms から移行する

このページでは、既存の (MVP ではない) WinForms アプリを **段階的にこのフレームワークに移行する** 手順を示します。
原則:**一気に全部書き換えない**。1 画面ずつ・1 機能ずつ移行し、移行済み画面と従来画面が共存できる状態を保ちながら進めます。

---

## 目次

- [移行の全体戦略](#移行の全体戦略)
- [Step 1: 準備 — フレームワーク参照とサービス構成](#step-1-準備--フレームワーク参照とサービス構成)
- [Step 2: 最初の画面を移行する](#step-2-最初の画面を移行する)
- [Step 3: `MessageBox.Show()` の置換](#step-3-messageboxshow-の置換)
- [Step 4: `OpenFileDialog` 等の置換](#step-4-openfiledialog-等の置換)
- [Step 5: ボタンイベントを ViewAction へ](#step-5-ボタンイベントを-viewaction-へ)
- [Step 6: ウィンドウナビゲーションの置換](#step-6-ウィンドウナビゲーションの置換)
- [Step 7: 既存ウィンドウを開く Presenter からの呼び出し](#step-7-既存ウィンドウを開く-presenter-からの呼び出し)
- [移行中の注意点](#移行中の注意点)
- [移行完了の判定](#移行完了の判定)
- [関連ページ](#関連ページ)

---

## 移行の全体戦略

| 戦略 | 説明 |
|------|----|
| **小さい画面から** | About ダイアログ・設定画面等の小さい画面で MVP の感覚を掴む |
| **新規機能だけ MVP** | 既存画面はそのまま、新規追加は MVP で書く |
| **段階的にリファクタ** | 移行コストが大きい画面は後回し。優先度を付ける |
| **共存可能な状態を維持** | 移行中も全機能が動く状態を保つ。big-bang 移行は避ける |

---

## Step 1: 準備 — フレームワーク参照とサービス構成

### 1.1 NuGet 参照を追加

```bash
dotnet add package Kibinago
```

(`Kibinago.Core` は依存として自動的に入ります。プロジェクト参照も可。移行先のプロジェクトは .NET Framework 4.7.2 以降、または .NET 10 (Windows) が必要です)

### 1.2 `Program.cs` にサービス構成を追加

```csharp
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // ← ここから追加
        var register = new ViewMappingRegister();
        register.RegisterFromAssembly(Assembly.GetExecutingAssembly());

        MvpApplication.Configure(services: reg =>
        {
            reg.RegisterInstance<IViewMappingRegister>(register);
            reg.RegisterInstance<Kibinago.Logging.ILoggerFactory>(new DebugLoggerFactory());
        });
        // ← ここまで追加

        Application.Run(new MainForm());      // ← 既存のメインフォームをそのまま実行
    }
}
```

これで **既存コードに影響なく** フレームワーク機能が使える状態になります。

---

## Step 2: 最初の画面を移行する

最初の移行は **シンプルなダイアログ** が最適。例: About ダイアログ。

### Before — 従来コード

```csharp
public partial class AboutForm : Form
{
    public AboutForm()
    {
        InitializeComponent();
        lblVersion.Text = $"Version {GetVersion()}";
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}

// 呼び出し
new AboutForm().ShowDialog();
```

### After — MVP

```csharp
// View インターフェイス
public interface IAboutView : IWindowView
{
    string Version { get; set; }
}

// Presenter
public class AboutPresenter : WindowPresenterBase<IAboutView>
{
    protected override void OnInitialize()
    {
        View.Version = $"Version {GetVersion()}";
    }

    protected override void RegisterViewActions()
    {
        // The Form's btnClose_Click => Close() becomes an action: RequestClose goes through
        // CanClose like X, so the Form writes no closing code.
        Dispatcher.Register(StandardActions.Close, () => RequestClose(InteractionStatus.Cancel));
    }
}

// Form
public partial class AboutForm : Form, IAboutView
{
    private ViewActionBinder _binder;
    public IViewActionBinder ActionBinder => _binder;

    public AboutForm()
    {
        InitializeComponent();
        _binder = new ViewActionBinder();
        _binder.Add(StandardActions.Close, btnClose);
    }

    public string Version
    {
        get => lblVersion.Text;
        set => lblVersion.Text = value;
    }
}

// 呼び出し
ServiceLocator.Current.Resolve<IWindowNavigator>()
    .ShowWindowAsModal(new AboutPresenter());
```

詳しい書き方は [はじめに](Getting-Started) を参照。

---

## Step 3: `MessageBox.Show()` の置換

これは最も簡単な置換で、Presenter で実装するときに自然に書き換わります。

### Before

```csharp
private void btnSave_Click(object sender, EventArgs e)
{
    try
    {
        SaveData();
        MessageBox.Show("Saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

### After — Service Locator (最少の変更)

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private void OnSave()
    {
        try
        {
            SaveData();
            Messages.ShowInfo("Saved!", "Success");
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

### After — Constructor Injection (テスタブル)

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private readonly IMessageService _messages;

    public MyPresenter(IMessageService messages)
    {
        _messages = messages;
    }

    private void OnSave()
    {
        try
        {
            SaveData();
            _messages.ShowInfo("Saved!", "Success");
        }
        catch (Exception ex)
        {
            _messages.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

詳しい DI パターンの選び方は [Dependency Injection](Reference-DependencyInjection) 参照。

---

## Step 4: `OpenFileDialog` 等の置換

### Before

```csharp
private void btnOpen_Click(object sender, EventArgs e)
{
    using (var dialog = new OpenFileDialog())
    {
        dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadFile(dialog.FileName);
        }
    }
}
```

### After

```csharp
private void OnOpenFile()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
    });

    if (result.IsOk)
        LoadFile(result.Value);
}
```

`InteractionResult<string>` は `IsOk` / `IsCancelled` / `IsError` で分岐できます。詳しくは [Platform Services](Reference-Platform-Services) を参照。

---

## Step 5: ボタンイベントを ViewAction へ

### Before

```csharp
public partial class MyForm : Form
{
    public MyForm()
    {
        InitializeComponent();
        btnSave.Click   += btnSave_Click;
        btnCancel.Click += btnCancel_Click;
        btnDelete.Click += btnDelete_Click;
    }

    private void btnSave_Click(object sender, EventArgs e) { /* save */ }
    private void btnCancel_Click(object sender, EventArgs e) { /* cancel */ }
    private void btnDelete_Click(object sender, EventArgs e) { /* delete */ }
}
```

### After

```csharp
// ActionKey 定義
public static class MyActions
{
    private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("My");

    public static readonly ViewAction Save   = Factory.Create(nameof(Save));
    public static readonly ViewAction Cancel = Factory.Create(nameof(Cancel));
    public static readonly ViewAction Delete = Factory.Create(nameof(Delete));
}

// Form (View 実装)
public partial class MyForm : Form, IMyView
{
    private ViewActionBinder _binder;
    public IViewActionBinder ActionBinder => _binder;

    public MyForm()
    {
        InitializeComponent();
        _binder = new ViewActionBinder();
        _binder.Add(MyActions.Save,   btnSave);
        _binder.Add(MyActions.Cancel, btnCancel);
        _binder.Add(MyActions.Delete, btnDelete);
    }
}

// Presenter
public class MyPresenter : WindowPresenterBase<IMyView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(MyActions.Save,   OnSave);
        Dispatcher.Register(MyActions.Cancel, OnCancel);
        Dispatcher.Register(MyActions.Delete, OnDelete);
    }

    private void OnSave()   { /* save */ }
    private void OnCancel() { /* cancel */ }
    private void OnDelete() { /* delete */ }
}
```

`CanExecute` で自動 Enabled 制御も追加できます。詳しくは [ViewAction システム](Reference-ViewAction-System) 参照。

レガシーアプリが既に基底 Form (`AppForm` など) を持っているなら、そこに `ViewActionBinder` を置けば、各画面は `new ViewActionBinder()` も `ActionBinder` の実装も書かずに `Binder.Add(...)` だけで済みます。手順は [ViewAction システム § アプリに基底 Form があるとき](Reference-ViewAction-System#アプリに基底-form-があるとき-binder-を基底クラスに置く) を参照。基底クラスの継承関係は変えません。

---

## Step 6: ウィンドウナビゲーションの置換

### Before

```csharp
private void btnEdit_Click(object sender, EventArgs e)
{
    var dialog = new EditUserForm(_userId);
    var result = dialog.ShowDialog();
    if (result == DialogResult.OK)
    {
        Reload();
    }
}
```

### After

```csharp
private void OnEdit()
{
    var presenter = new EditUserPresenter();
    var parameters = new EditUserParameters { UserId = _userId };

    var result = Navigator.For(presenter)
                          .WithParam(parameters)
                          .ShowAsModal<UserResult>();

    if (result.IsOk)
        Reload();
}
```

詳しくは [WindowNavigator](Reference-WindowNavigator) 参照。

---

## Step 7: 既存ウィンドウを開く Presenter からの呼び出し

移行途中、**移行済み Presenter から未移行の従来 Form を開きたい** 場合があります。
Presenter からは WinForms 型を扱えないので、従来 Form を開く処理は View 側か中継サービスに置きます。結果はどちらも `InteractionResult<T>` (Ok / Cancel / Error) で返します。

| | 方法 A: View のメソッド (手軽) | 方法 B: 中継サービス |
|---|---|---|
| 向いている場合 | その画面だけが開く従来 Form | 複数の画面から同じ従来 Form を開く |
| 書くもの | View インターフェイスに 1 メソッド、Form にその実装 | インターフェイス + 実装 + 登録 |
| テスト | モック View がその結果を返す | 偽のサービスを渡す |

### 方法 A: View のメソッドで開く (インターフェイス不要)

View インターフェイスに「何をするか」を表すメソッドを 1 つ足し、Form の実装の中で従来 Form を開きます。Presenter は「いつ開くか」と結果の扱いだけを決め、従来 Form の型は見ません。

```csharp
public interface ICustomerView : IWindowView
{
    /// 従来の編集画面で current (null = 新規) を編集し、結果を返す
    InteractionResult<CustomerData> EditCustomer(CustomerData current);
    void ShowCustomer(CustomerData customer);
}

// Form: 生成・値の受け渡し・破棄はここで完結する
public InteractionResult<CustomerData> EditCustomer(CustomerData current)
{
    using (var legacy = new LegacyCustomerForm())
    {
        legacy.CustomerName = current?.Name;          // 従来 Form は公開プロパティで値を受け渡すことが多い
        if (legacy.ShowDialog(this) != DialogResult.OK)
            return InteractionResult<CustomerData>.Cancel();
        return InteractionResult<CustomerData>.Ok(new CustomerData { Name = legacy.CustomerName });
    }
}

// Presenter
private void OnEditCustomer()
{
    var result = View.EditCustomer(_customer);
    if (!result.IsOk) return;
    _customer = result.Value;                         // 検証・保存はここ
    View.ShowCustomer(_customer);
}
```

- メソッド名は「従来 Form で」ではなく、やること (`EditCustomer`) にします。あとで従来 Form を MVP に置き換えても、Presenter とテストは変わりません。
- テストではモック View が `InteractionResult<CustomerData>.Ok(...)` / `Cancel()` / `Error(...)` を返すだけです。
- モードレスで開く場合は `Task<InteractionResult<CustomerData>> EditCustomerAsync(CustomerData current, CancellationToken ct)` とし、Form が閉じたときに結果を返します (Presenter は非同期アクションで `await`)。
- サンプル: [`samples/Kibinago.Samples/LegacyPopupDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/LegacyPopupDemo)。

### 方法 B: 中継サービス (ILegacyFormService)

### ILegacyFormService の定義

```csharp
public interface ILegacyFormService
{
    InteractionResult<T> OpenForm<T>(string formKey);
}

public class LegacyFormService : ILegacyFormService
{
    public InteractionResult<T> OpenForm<T>(string formKey)
    {
        switch (formKey)
        {
            case "LegacyCustomerEditor":
                using (var dialog = new LegacyCustomerEditorForm())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                        return InteractionResult<T>.Ok((T)(object)dialog.Result);
                    return InteractionResult<T>.Cancel();
                }
            default:
                return InteractionResult<T>.Error($"Unknown form: {formKey}");
        }
    }
}
```

### Presenter からの呼び出し

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private readonly ILegacyFormService _legacyForms;

    public MyPresenter(ILegacyFormService legacyForms)
    {
        _legacyForms = legacyForms;
    }

    private void OnOpenLegacyEditor()
    {
        var result = _legacyForms.OpenForm<CustomerData>("LegacyCustomerEditor");

        if (result.IsOk)
            View.ShowCustomer(result.Value);
    }
}
```

このサービスは **移行が完了したら削除** します。それまでの一時的な橋渡しです。

---

## 移行中の注意点

### 既存 Form の `using` ステートメントに注意

```csharp
// ❌ Bad — using で囲むと早期 Dispose で WindowNavigator のイベント購読が解除される
using (var presenter = new MyPresenter())
{
    Navigator.For(presenter).ShowAsModal();
}

// ✅ Good — WindowNavigator がライフサイクルを管理
var presenter = new MyPresenter();
Navigator.For(presenter).ShowAsModal();
```

### 既存ロジックを Presenter に **そのまま** コピーしない

```csharp
// ❌ Bad — Form 由来のロジックをそのまま Presenter にコピー
private void OnSave()
{
    if (this.WindowState == FormWindowState.Minimized) { ... }   // ← UI 型
    var grid = this.dataGridView1;                                // ← UI 型
}

// ✅ Good — UI 依存を View インターフェイスに切り出す
private void OnSave()
{
    if (View.IsMinimized) { ... }
    var data = View.GridData;
}
```

### 部分的に MVP 化した画面の見分け方

```csharp
// 移行済みの Form は IXxxView を実装している
public class MyForm : Form, IMyView { /* ... */ }

// 未移行の Form はそのまま
public class LegacyForm : Form { /* ... */ }
```

`grep -l "IWindowView" *.cs` 等で移行済み画面を確認できます。

---

## 移行完了の判定

すべての画面が以下を満たせば移行完了です。

- ☐ すべての Form が `IWindowView` を継承するインターフェイスを実装
- ☐ すべての Form 起動が `WindowNavigator` 経由
- ☐ Presenter 内に `MessageBox.Show()` / `new OpenFileDialog()` 等の WinForms 直接呼び出しがゼロ
- ☐ Presenter 内に `using System.Windows.Forms;` ディレクティブがゼロ (View 実装にだけ存在)
- ☐ `ILegacyFormService` のような移行用ブリッジを削除
- ☐ 単体テストが各 Presenter に対して書かれている

最後の項目はベストエフォートでもよいですが、最初の 5 つは必須です。

---

## 関連ページ

- [はじめに (Getting Started)](Getting-Started) — 最初の MVP アプリの作り方
- [MVP パターンとは](Concept-MVP-Pattern) — 思想の理解
- [Platform Services](Reference-Platform-Services) — MessageBox / Dialog の置き換え
- [ViewAction システム](Reference-ViewAction-System) — ボタンイベントの宣言的バインド
- [WindowNavigator](Reference-WindowNavigator) — ウィンドウ表示の置き換え
- [Dependency Injection](Reference-DependencyInjection) — Service Locator / Constructor Injection の選び方
- [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) — 移行後のテスト追加
