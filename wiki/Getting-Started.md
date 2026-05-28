# はじめに (Getting Started)

このページは **5 分で動くサンプルを作る** ための最短経路です。
フレームワークの全体像を体感したら、[チュートリアル: 最初のアプリを作る](Tutorial-Building-Your-First-App) で本格的な機能に進んでください。

---

## 必要なもの

| 項目 | バージョン |
|------|----------|
| .NET Framework | 4.0 または 4.8 |
| 言語 | C# 7.3 以上 |
| IDE | Visual Studio 2019 以降 / JetBrains Rider / VS Code |
| 知識 | C# と WinForms の基本 |

---

## 1. プロジェクトを準備する

リポジトリをクローンして、サンプルアプリをまず動かしてみることをおすすめします。

```bash
git clone https://github.com/VoidAxon/winforms-mvp.git
cd winforms-mvp
dotnet build src/winforms-mvp.sln
dotnet run --project src/WinformsMVP.Samples/WinformsMVP.Samples.csproj
```

サンプルメニューが表示されたら準備は完了です。

---

## 2. 最小サンプルを作る (Hello MVP)

ここからは、新しい WinForms プロジェクトに `WinformsMVP` を参照した状態で、最小の MVP 構成を組み立てていきます。
構成要素は **3 つだけ**:

| 役割 | 何をするか |
|------|----------|
| **View インターフェイス** | Presenter が「画面に何を見せたいか」を定義する契約 |
| **Presenter** | ユースケースのロジック (Form 知識ゼロ) |
| **Form** | View インターフェイスを実装した、実際の画面 |

### 2.1 View インターフェイス

```csharp
using WinformsMVP.MVP.Views;

namespace MyFirstMvpApp
{
    public interface IMainView : IWindowView
    {
        string WelcomeMessage { get; set; }
    }
}
```

**ポイント**

- `IWindowView` を継承する (Form 用)。UserControl の場合は `IViewBase`。
- 公開するのは **データと振る舞いだけ**。`Button` や `TextBox` 等の WinForms 型は絶対に露出させない。

### 2.2 Presenter

```csharp
using WinformsMVP.MVP.Presenters;

namespace MyFirstMvpApp
{
    public class MainPresenter : WindowPresenterBase<IMainView>
    {
        protected override void OnInitialize()
        {
            View.WelcomeMessage = "Hello MVP!";
        }
    }
}
```

**ポイント**

- `WindowPresenterBase<TView>` を継承するだけで、Form 向け Presenter になる。
- `OnInitialize()` は View アタッチ直後・表示前に 1 回呼ばれる初期化フック。
- `View` プロパティで画面を操作する (画面そのものではなくインターフェイス越し)。

### 2.3 Form (View の実装)

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace MyFirstMvpApp
{
    public class MainForm : Form, IMainView
    {
        private readonly Label _welcomeLabel;

        public MainForm()
        {
            Text = "My First MVP App";
            Size = new Size(480, 240);
            StartPosition = FormStartPosition.CenterScreen;

            _welcomeLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(_welcomeLabel);
        }

        public string WelcomeMessage
        {
            get => _welcomeLabel.Text;
            set => _welcomeLabel.Text = value;
        }
    }
}
```

**ポイント**

- `_welcomeLabel` は `private`。Presenter からは見えない。
- `WelcomeMessage` プロパティ越しに `Label.Text` を読み書きする。
- Form は UI の詳細 (フォント、レイアウト、コントロールの種類) をすべて自分の中に閉じ込める。

### 2.4 エントリーポイント

```csharp
using System;
using System.Windows.Forms;

namespace MyFirstMvpApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var view = new MainForm();
            var presenter = new MainPresenter();

            presenter.AttachView(view);
            presenter.Initialize();

            Application.Run(view);
        }
    }
}
```

実行すると、中央に "Hello MVP!" と表示されたウィンドウが立ち上がります。これが MVP の最小構成です。

---

## 3. ボタンとアクションを足す

次に **ボタンを 1 つ追加し、クリックで挨拶メッセージを更新** します。
この拡張で、フレームワークの中核機能である **ViewAction システム** に触れます。

### 3.1 アクションを定義する

```csharp
using WinformsMVP.MVP.ViewActions;

namespace MyFirstMvpApp
{
    public static class MainActions
    {
        public static readonly ViewAction Greet = ViewAction.Create("Main.Greet");
    }
}
```

`ViewAction` は不変な「アクション識別子」です。文字列リテラルをそのまま使うのではなく、必ず静的クラスで定数として宣言します。

### 3.2 View インターフェイスを拡張する

```csharp
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

namespace MyFirstMvpApp
{
    public interface IMainView : IWindowView
    {
        string WelcomeMessage { get; set; }
        string UserName { get; set; }
        bool HasUserName { get; }

        ViewActionBinder ActionBinder { get; }
    }
}
```

`ActionBinder` プロパティは Form が内部で構成した「コントロール ⇔ アクション」のバインディングを Presenter に渡す窓口です (Presenter 側からはバインドの中身を直接見ない)。

### 3.3 Presenter にハンドラを登録する

```csharp
public class MainPresenter : WindowPresenterBase<IMainView>
{
    protected override void OnInitialize()
    {
        View.WelcomeMessage = "Enter your name and press the button.";
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            MainActions.Greet,
            OnGreet,
            canExecute: () => View.HasUserName);

        // The framework automatically calls View.ActionBinder.Bind(Dispatcher)
        // after this method returns. No manual binding required.
    }

    private void OnGreet()
    {
        View.WelcomeMessage = $"Hello, {View.UserName}!";
    }
}
```

**ポイント**

- `RegisterViewActions()` でアクションキーとハンドラ・`CanExecute` 条件を登録するだけ。
- Presenter は `Button` を知らない。「`Greet` アクションが起きたら何をするか」だけを書く。

### 3.4 Form を更新する

```csharp
public class MainForm : Form, IMainView
{
    private readonly Label _welcomeLabel;
    private readonly TextBox _nameTextBox;
    private readonly Button _greetButton;
    private readonly ViewActionBinder _binder;

    public MainForm()
    {
        Text = "My First MVP App";
        Size = new Size(480, 280);
        StartPosition = FormStartPosition.CenterScreen;

        _welcomeLabel = new Label { /* ... */ };
        _nameTextBox = new TextBox { /* ... */ };
        _greetButton = new Button { Text = "Greet" };

        Controls.Add(_welcomeLabel);
        Controls.Add(_nameTextBox);
        Controls.Add(_greetButton);

        _binder = new ViewActionBinder();
        _binder.Add(MainActions.Greet, _greetButton);

        // Notify the dispatcher when the input changes so CanExecute reruns.
        _nameTextBox.TextChanged += (s, e) => UserNameChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler UserNameChanged;

    public string WelcomeMessage
    {
        get => _welcomeLabel.Text;
        set => _welcomeLabel.Text = value;
    }

    public string UserName
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }

    public bool HasUserName => !string.IsNullOrWhiteSpace(_nameTextBox.Text);

    public ViewActionBinder ActionBinder => _binder;
}
```

### 3.5 状態変化を Presenter に伝える

`HasUserName` が変わったら `CanExecute` を再評価する必要があります。Presenter で View のイベントを購読しましょう。

```csharp
protected override void OnViewAttached()
{
    View.UserNameChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
}
```

これで、テキストボックスが空のときは **Greet ボタンが自動的に無効化** され、何か入力すると有効になります。手動の `Enabled = false` 制御は一切不要です。

---

## 4. サービスを使う

最後に **メッセージダイアログ** を表示します。
Presenter から `MessageBox.Show()` を直接呼ぶのは **MVP 違反** です。代わりに `Messages` プロパティ越しに `IMessageService` を使います。

```csharp
private void OnGreet()
{
    Messages.ShowInfo($"Hello, {View.UserName}!", "Greeting");
}
```

**Presenter で使える主要サービス**

| プロパティ | 型 | 用途 |
|----------|----|----|
| `Messages` | `IMessageService` | メッセージダイアログ、確認ダイアログ |
| `Dialogs` | `IDialogProvider` | OpenFile / SaveFile / FolderBrowser |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | 構造化ロギング |

これらは `PlatformServices.Default` から自動的に注入されます。コンストラクタ引数は不要です。

詳しくは [Platform Services](Reference-Platform-Services) を参照してください。

---

## 次のステップ

| 目的 | 読むべきページ |
|------|--------------|
| もう少し本格的なアプリを作りたい | [チュートリアル: 最初のアプリを作る](Tutorial-Building-Your-First-App) |
| なぜ MVP なのかを理解したい | [MVP パターンとは](Concept-MVP-Pattern) |
| フレームワークの全体像を掴みたい | [アーキテクチャ概観](Concept-Architecture-Overview) |
| ViewAction を深掘りしたい | [ViewAction システム](Reference-ViewAction-System) |
| Presenter をテストしたい | [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) |
| 既存の WinForms アプリを段階的に移行したい | [HowTo: 従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) |

---

## トラブルシューティング

**View インターフェイス IXxxView に対応する実装型が見つかりません**

`WindowNavigator` を使っている場合、View 実装型が `IViewMappingRegister` に登録されていません。次のいずれかを試してください。

```csharp
// 明示的に登録
register.Register<IMainView, MainForm>();

// または、アセンブリからの自動スキャン
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());
```

詳しくは [ViewMappingRegister](Reference-ViewMappingRegister) を参照してください。

**ボタンが常に無効のまま (`CanExecute` が `false` 固定)**

`CanExecute` が依存する状態 (例: `View.HasUserName`) が変化したことを Dispatcher に伝えていません。View のイベントを購読し、`Dispatcher.RaiseCanExecuteChanged()` を呼んでください (本ページ 3.5 節を参照)。

**`MessageBox.Show()` を Presenter から呼んでテストできない**

Presenter は WinForms 型に直接依存してはいけません。`Messages.ShowInfo()` 等のサービス経由に書き換えてください。テスト時は `MockPlatformServices` を注入することでダイアログの呼び出しを検証できます。
