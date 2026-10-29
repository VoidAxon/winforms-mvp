# MVP パターンとは

このページでは、本フレームワークの土台となる **MVP (Model-View-Presenter)** パターンの考え方を、なぜそうするのか・どう違うのかという観点から解説します。
API の詳細を知りたい方は、各 [リファレンス](Home) ページを参照してください。

---

## 一言で言うと

> **画面 (View) と業務ロジック (Presenter) を厳格に切り離し、Presenter から WinForms 型への依存をゼロにする。**

これが MVP の核心です。フレームワーク全体の設計は、すべてこの一文を実現するための仕組みです。

---

## 3 つの構成要素

```
    ┌───────────────────────────────────────────────────────────────┐
    │                        MVP Pattern                            │
    └───────────────────────────────────────────────────────────────┘

    ┌──────────────┐         ┌──────────────┐         ┌──────────────┐
    │              │         │              │         │              │
    │    Model     │ ◄────── │  Presenter   │ ──────► │     View     │
    │              │         │              │         │ (Interface)  │
    │  Data & BL   │         │  Use cases   │         │              │
    └──────────────┘         └──────────────┘         └──────────────┘
           ▲                        ▲                        ▲
           │                        │                        │
    ┌──────┴───────┐         ┌──────┴───────┐         ┌──────┴────────┐
    │ Repositories │         │   Services   │         │ Form/Control  │
    │ DTOs/Entities│         │  Validation  │         │ (WinForms UI) │
    └──────────────┘         └──────────────┘         └───────────────┘
```

### Model — 業務データと業務ルール

| | |
|---|---|
| **何を持つか** | エンティティ、DTO、ドメインロジック |
| **典型例** | `Customer`、`Order`、`UserProfile` |
| **依存しないもの** | UI、Forms、Controls |

### View — ユーザーインターフェイス

| | |
|---|---|
| **何を持つか** | Form や UserControl の見た目と入出力 |
| **典型例** | `UserEditorForm`、`CustomerListControl` |
| **公開するもの** | データプロパティ・イベントを持つ **インターフェイス** (`IXxxView`) |
| **公開しないもの** | `Button` / `TextBox` 等の WinForms 型 |
| **依存しないもの** | 業務ロジック、データ検証 |

### Presenter — ユースケースのオーケストレーター

| | |
|---|---|
| **何を持つか** | バリデーション → 業務処理 → 通知の流れ (ユースケースロジック) |
| **典型例** | `UserEditorPresenter`、`CustomerListPresenter` |
| **連携するもの** | View インターフェイス、Service、Model |
| **依存しないもの** | `Button` / `TextBox` / `MessageBox` 等の WinForms 型 |

---

## ユーザー操作の流れ

```
1. ユーザーがボタンをクリック
        ↓
2. View が ViewAction を発火 (または従来のイベント)
        ↓
3. Presenter のハンドラが呼ばれる
        ↓
4. Presenter が Model / Service を呼び出す
        ↓
5. Presenter が View インターフェイス経由で結果を反映
        ↓
6. View が更新されてユーザーに見える
```

Presenter は手順 5 で `Button.Enabled = false` のようなことは **書きません**。
代わりに `View.HasSelectedItem` 等のインターフェイスプロパティ越しに状態を制御し、UI への反映は Form の責任です。これによって Presenter は単体テスト可能になります。

---

## なぜ MVC ではなく MVP か (Supervising Controller)

このフレームワークは MVP の **Supervising Controller** という変種を採用しています。同じ GUI アーキテクチャの系譜にはいくつかの変種があるので、まず整理しておきます。

| 観点 | 古典的 GUI MVC (Smalltalk 系) | Passive View MVP | Supervising Controller MVP |
|------|-------|-------|-------|
| **View の賢さ** | 単純 — UI コントロールに直接アクセス | 完全に "dumb" — プロパティとメソッドだけ持つ | 賢い — 単純なデータバインドは View 内で完結 |
| **Controller / Presenter の責務** | ユースケース + View ロジック (両方) | あらゆる UI 更新 (最も単純な代入も含む) | **ユースケース + 複雑な View ロジック のみ** |
| **データバインド** | 手動 (Controller が setter を呼ぶ) | View 抽象越しに Presenter が全プロパティを書く | 単純な field-to-property は View が直接バインド、Presenter は触らない |
| **可測性** | △ Controller と View が密結合 | ◎ View 完全モック可 | ○ View 抽象越しに複雑ロジックはテスト可 (単純バインドはテスト対象外) |
| **ボイラープレート** | 多い | 多い (代入だけのためのインターフェイスメソッドが大量) | 中程度 |

> 💡 「MVC」と一口に言っても **Smalltalk MVC・Web MVC (Rails 等)・iOS MVC** など語義が大きく異なります。ここで比較しているのは **GUI リッチクライアント** に適用される Smalltalk 起源の系譜です。Web MVC とは別物として読んでください。

### 核心となる原則

> **Presenter handles use-case logic only, not view logic.**
>
> 現代の WinForms View はデータバインド・イベント処理に十分賢いので、
> Presenter は「検証 → 保存 → 通知」という **業務の流れ** だけを書きます。
> エラーをどう赤字で出すか・どのコントロールを使うか等の **UI の見た目の詳細** は View が持ちます。

ただしこれは「Presenter が View を一切触らない」という意味では **ありません**。Presenter は **View 抽象 (`IXxxView` インターフェイス)** を通じて、**複雑な UI 操作** (条件付き表示、動的な有効/無効化、エラーメッセージ表示等) を指示します。**触らないのは具体的なコントロール (Button・TextBox 等)** であって、View そのものではありません。

| 例 | 担当 | 理由 |
|---|---|---|
| `txtName.Text` ↔ `Model.Name` の単純な双方向バインド | View | データバインドで十分 |
| 入力エラー時に該当フィールドを赤くハイライト | View | 「どう赤くするか」は UI 詳細 |
| 「入力された名前が DB に存在しない」というドメイン検証結果 | Presenter → `View.ShowValidationError(...)` | 業務判断 |
| 「未保存変更がある時に保存ボタンを有効化」 | Presenter → `canExecute: () => _changeTracker.IsChanged` | 業務状態の判定 |

### 使い分けの目安 (フォームごと・フォーム内、どちらで混ぜても OK)

Passive View と Supervising Controller は **対立する 2 つの選択** ではなく、**同じ MVP の連続スペクトラム** 上の 2 つの目印です。Martin Fowler 自身も "the boundaries are fluid" と述べており、同じプロジェクト内でフォームごとに違うスタイルを採ること、さらには同じフォーム内で両者を混在させることも、まったく問題ありません (実務では「基本フィールドはバインド、検証エラーや活性制御は明示メソッド」というハイブリッドが最も多い)。

このフレームワークの API (データバインド・ChangeTracker・ViewAction の `canExecute` 等) は **デフォルトの "recipe"** としては Supervising Controller 寄りに設計されています。理由は、WinForms のデータバインド機構が十分成熟しており、単純な field-to-property を Presenter に書くのは記述量の割に得るものが少ないためです。テスト容易性が重要な箇所だけ局所的に Passive View 寄りに倒す方が、トータルの開発コストが低いという判断です。

Passive View 寄りに書くことも可能で、必要に応じて `View.SetXxx(...)` 形式のメソッドを追加し、Presenter から明示的に呼ぶスタイルに切り替えられます。

判断の目安:

| そのフォームで重視したいこと | 寄せる先 | 理由 |
|---|---|---|
| Presenter 単体で複雑な業務ロジックを完全にテストしたい | Passive View 寄り | UI 副作用も全部 View 抽象越しなら、テストで挙動が完全に観測できる |
| クロスフィールド検証・条件付き表示・段階的ウィザード | Passive View 寄り | View ロジックが複雑なほど Presenter で書いた方がテスト容易 |
| データバインドで済む単純なフォーム | Supervising Controller 寄り | `BindingSource` で済む話を Presenter に書くのは過剰 |
| 一覧表示・詳細表示・読み取り専用画面 | Supervising Controller 寄り | UI 更新を全部 setter にする意味が薄い |

> ⚠️ **避けたいパターン**: 同じフォーム内で **原則なく** 両スタイルが混在 —「ある項目はバインド、似た別項目は setter」のような一貫性のない書き方は保守性が下がります。**「混ぜる」と「無秩序に混ぜる」は別物** であり、チーム内で判断基準を明文化しておくことが重要です。

入力項目の多い編集画面を Supervising Controller で組む具体的な手順は [入力項目の多い編集画面を作る](HowTo-Build-Large-Edit-Screen) にまとめています。

つまり「**モデル名は語彙であってドグマではない**」が本フレームワークの立場です。Passive View と Supervising Controller は、コードレビューや設計議論で「このフォームはどっち寄りにする?」を相手に伝えるための共通言語として使ってください。

> 📚 詳しくは [Martin Fowler — GUI Architectures](https://martinfowler.com/eaaDev/uiArchs.html) (Supervising Controller / Passive View の原典) を参照してください。

---

## なぜ WinForms では MVVM ではなく MVP か

先に結論を書きます。**WinForms でも MVVM はきちんと書けます。** 本フレームワークが MVP を選んでいるのは MVVM が劣るからではありません。業務画面に毎回必要になる部品 (閉じる前の確認、別ウィンドウと結果の受け渡し、テストの入口など) を、**チームが自分で作るか、最初から用意されたものを使うか** の違いです。

### 現代の WinForms でも MVVM は成立する

- **.NET 8 から**、`ButtonBase.Command` / `CommandParameter` と `ToolStripItem.Command` が正式な API になりました (.NET 7 ではプレビュー)。ボタンは `ICommand.CanExecuteChanged` を受けて `CanExecute` を問い合わせ、`Enabled` を自動で切り替えます。
- `Control.DataContext` (.NET 7 以降) や、従来からある `BindingSource` / `DataBindings` で、プロパティを ViewModel にバインドできます。
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) の `[ObservableProperty]` / `[RelayCommand]` を使えば、`private` メソッドからコマンドが生成されます。ViewModel が `public` な業務メソッドを並べる必要はありません。

つまり「WinForms はバインドが弱いから MVVM は無理」ではありません。

### WinForms で MVVM が崩れやすいところ

バインドとコマンドが扱うのは **値** と **ボタン** です。業務画面には、そのどちらでもないものが必ず出てきます。

| やりたいこと | WinForms MVVM での一般的な形 |
|---|---|
| ✕ / Alt+F4 / 親ウィンドウ / シャットダウンで閉じる前の確認 | Form が `FormClosing` を拾って ViewModel に問い合わせるアダプター。答えが非同期なら一度キャンセルし、あとで `Close()` し直す |
| 別ウィンドウを開いて結果を受け取る | ダイアログ / ナビゲーションのサービスを自作 |
| エラーのある項目へフォーカスを移す | ViewModel から View へ伝える仕組み (Messenger、イベント) を自作 |
| 待機表示、ウィンドウを閉じたら非同期処理を止める | 画面ごとに `CancellationTokenSource` と後始末を書く |
| ViewModel をテストで動かす | コマンドの直接呼び出し (次の節の注意点あり) とサービスの偽物を自作 |

これらのアダプターは、どのチームでも一度は書くことになります。用意できていないと、穴を埋めるためにコードビハインドが ViewModel の `public` メソッド (`vm.Save()`、`vm.CanClose()`) を直接呼び始め、ViewModel が「画面から呼ばれるメソッドの寄せ集め」に崩れていきます。崩れるのはアダプターが足りないときであり、MVVM そのものの欠点ではありません。

### Kibinago が最初から用意しているもの

| 必要なもの | Kibinago での形 |
|---|---|
| 閉じる前の確認 | Presenter の `CanClose` / `CanCloseAsync` の 1 か所。✕・Alt+F4・親ウィンドウが閉じる・`RequestClose` のどれも同じゲートを通る。シャットダウン / タスクマネージャーは問い合わせずに許可、非同期の答えを待つ間の再度の ✕ は抑える。Form に閉じる処理は書かない ([ウィンドウクローズモデル](Concept-Window-Closing-Model)) |
| 操作と有効状態 | `Dispatcher.Register(key, handler, canExecute)`。Dispatcher は実行前に必ず `canExecute` を確認する。非同期ハンドラーの実行中は自動で無効になり、二度押しは無視される |
| 画面遷移と結果 | `Navigator.For(presenter).WithParam(x).ShowAsModal<T>()` が `InteractionResult<T>` を返す。`key` で 1 レコード 1 ウィンドウ ([WindowNavigator](Reference-WindowNavigator)) |
| ライフタイム | `Lifetime` (閉じたらキャンセル)、`ShowBusy()`、`SubscribeViewEvent` / `Disposables` の自動解除 |
| テスト | `PresenterTestHost` と、メッセージ / ダイアログ / ナビゲーターの偽物 (`TestServices.CreateFakes()`) |
| 既存 Form の移行 | `presenter.Connect(form)` で 1 画面ずつ ([既存 WinForms からの移行](HowTo-Migrate-From-Legacy-WinForms)) |
| .NET Framework | `net472` と `net10.0-windows` で同じ API。WinForms 標準の `Command` は .NET 8 以降のみで、.NET Framework にはない |
| ルールの強制 | Roslyn アナライザーが Presenter からの WinForms 型の参照などをコンパイル時に検出 |

なお、Presenter の `public` が「コンストラクターだけ」というのは **業務の入口** の話です。`Dispose` などのライフサイクル用メンバーは `public` です。

### 同じ入口でテストする

CommunityToolkit.Mvvm の `RelayCommand.Execute` と `AsyncRelayCommand.ExecuteAsync` は、それ自体では `CanExecute` を確認しません ([ソース](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.Mvvm/Input/RelayCommand.cs))。画面ではボタンが無効になるので問題になりませんが、テストやショートカットキーの処理からコマンドを直接呼ぶと、画面ではできない操作が実行されます。

```csharp
// CommunityToolkit.Mvvm
await vm.SaveCommand.ExecuteAsync(null);            // CanSave() が false でも保存が走る
if (vm.SaveCommand.CanExecute(null))                 // 画面と同じ条件にするには呼ぶ側で確認する
    await vm.SaveCommand.ExecuteAsync(null);

// Kibinago: ボタンと同じ Dispatcher を通る
await view.Binder.DispatchAsync(StandardActions.Save);   // canExecute が false なら何もしない
Assert.Equal(InteractionStatus.Ok, host.CloseStatus);    // 閉じる結果も同じゲートを通って記録される
```

MVVM でも、チームで小さなヘルパーを書けば揃えられます。違いは **何もしなかったときの既定の経路** がどちらか、です。

### 同じ編集画面で比べる

顧客名を編集して保存すると閉じ、未保存のまま閉じようとすると確認する画面です。どちらもハンドラーは `private` で書きます。

**MVVM (CommunityToolkit.Mvvm)**

```csharp
public partial class CustomerEditViewModel : ObservableObject
{
    private readonly ICustomerRepository _repository;
    private readonly IConfirmService _confirm;       // 自作: 確認ダイアログの抽象
    private readonly int _id;
    private string _savedName;

    [ObservableProperty]
    private string _name;

    public CustomerEditViewModel(ICustomerRepository repository, IConfirmService confirm, Customer customer)
    {
        _repository = repository;
        _confirm = confirm;
        _id = customer.Id;
        _name = _savedName = customer.Name;
    }

    public event EventHandler CloseRequested;        // 自作: 閉じる要求を Form へ伝える

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repository.SaveAsync(new Customer { Id = _id, Name = Name });
        _savedName = Name;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    // Form の FormClosing から呼ばれるので public
    public bool CanClose() => Name == _savedName || _confirm.Ask("変更を破棄して閉じますか?");
}

public partial class CustomerEditForm : Form
{
    private readonly CustomerEditViewModel _vm;
    private bool _closeAccepted;

    public CustomerEditForm(CustomerEditViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        txtName.DataBindings.Add(nameof(TextBox.Text), vm, nameof(vm.Name),
            false, DataSourceUpdateMode.OnPropertyChanged);
        btnSave.Command = vm.SaveCommand;
        vm.CloseRequested += (s, e) => { _closeAccepted = true; Close(); };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closeAccepted && e.CloseReason != CloseReason.WindowsShutDown && !_vm.CanClose())
            e.Cancel = true;
        base.OnFormClosing(e);
    }
}
```

これで動きますが、次のものはまだありません。画面ごとに書くか、チームで基底 Form とサービスを作ることになります。

- 呼び出し側へ保存した `Customer` を返す仕組み
- 非同期の確認 (「保存しますか?」→ 保存を待ってから閉じる): 一度 `e.Cancel` してから `Close()` し直し、その間の ✕ を抑える
- 親ウィンドウや `Application.Exit` による終了を、非同期の確認のあとに続ける
- 閉じたら実行中の保存をキャンセルし、ViewModel を破棄する

**Kibinago**

```csharp
public interface ICustomerEditView : IWindowView
{
    string CustomerName { get; set; }
}

public sealed class CustomerEditPresenter : WindowPresenterBase<ICustomerEditView, Customer>
{
    private readonly ICustomerRepository _repository;
    private int _id;
    private ChangeTracker<Customer> _tracker;

    public CustomerEditPresenter(ICustomerRepository repository) => _repository = repository;

    protected override void RegisterViewActions()
        => Dispatcher.Register(StandardActions.Save, SaveAsync);

    protected override void OnInitialize(Customer customer)
    {
        _id = customer.Id;
        View.CustomerName = customer.Name;
        _tracker = TrackChanges(ReadModel());
    }

    private Customer ReadModel() => new Customer { Id = _id, Name = View.CustomerName };

    private async Task SaveAsync(CancellationToken ct)    // ct はウィンドウを閉じるとキャンセルされる
    {
        var customer = ReadModel();
        await _repository.SaveAsync(customer, ct);
        _tracker.AcceptChanges(customer);
        RequestClose(customer, InteractionStatus.Ok);       // 呼び出し側は InteractionResult<Customer> で受け取る
    }

    // ✕ / Alt+F4 / 親ウィンドウ / RequestClose のすべてがここを通る
    protected override bool CanClose(CloseReason reason)
        => !_tracker.IsChangedWith(ReadModel()) || Messages.ConfirmYesNo("変更を破棄して閉じますか?");
}

public partial class CustomerEditForm : Form, ICustomerEditView
{
    private readonly ViewActionBinder _binder = new ViewActionBinder();

    public CustomerEditForm()
    {
        InitializeComponent();
        _binder.Add(StandardActions.Save, btnSave);
    }

    public IViewActionBinder ActionBinder => _binder;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CustomerName { get => txtName.Text; set => txtName.Text = value; }
}
```

Form に閉じる処理はありません。結果の受け渡し、非同期の確認 (`CanCloseAsync` に変えるだけ)、親ウィンドウやシャットダウンとの関係、閉じたときのキャンセルと破棄は、フレームワークが受け持ちます。逆に MVVM の方が短いのは、値のバインド (`DataBindings` 1 行 対 プロパティ 1 つ) です。項目が多い画面で View のプロパティが増えるのは MVP の費用で、[入力項目の多い編集画面を作る](HowTo-Build-Large-Edit-Screen) で減らし方を説明しています。

### MVVM を選ぶべき場合

次のような場合は、MVVM の方が合っています。

- **観測可能な状態を表示することが中心の画面** (監視画面、ダッシュボードなど)。閉じる確認や結果の受け渡しが少なく、バインドだけで大半が済む
- **ViewModel を WPF / WinUI / .NET MAUI と共有したい**。Kibinago の Presenter は WinForms の View インターフェイスを前提にしている
- **すでに MVVM でうまく回っているプロジェクト**。上のアダプターをチームで持っているなら、移行する理由はありません

Kibinago が向いているのは、入力・確認・保存・閉じるを繰り返す業務画面が多く、.NET Framework の既存 Form を少しずつ整理していきたい、MVVM の作法を持ち込みたくないチームです。

---

## 3 つの鉄則 (Three Iron Rules)

このフレームワークで「MVP に違反していないか」を判定する 3 つの絶対基準です。
[MVP 設計ルール](Design-Rules) 全 17 条の根幹となるルールでもあります。

| 鉄則 | 内容 |
|------|------|
| **1. View インターフェイスは UI 型を露出させない** | プロパティ・メソッド・イベント引数すべてに `System.Windows.Forms` 名前空間の型を含めない |
| **2. Presenter は UI 型を直接扱わない** | `MessageBox.Show()` 禁止。すべてのサービスは `IMessageService` 等の抽象経由 |
| **3. View は Presenter の業務メソッドを直接呼ばない** | ユーザー操作は ViewAction (アクションキー) かイベントで Presenter に仲介する。`presenter.Save()` のような業務メソッドの直接呼び出しは禁止。理想は View が Presenter を一切参照しないこと (ViewAction システムがこの方向を仲介するため成立する設計)。レガシー統合では参照保持・ライフサイクル配線までは妥協として許容するが、業務メソッド呼び出しは不可 |

具体例・違反例・許容範囲の詳細は [MVP 設計ルール](Design-Rules) を参照してください。本ページでは「なぜこの 3 つが必要か」を以下の比較で示します。

---

## 従来の WinForms との比較

### ❌ 従来の WinForms

```csharp
public class UserForm : Form
{
    private void btnSave_Click(object sender, EventArgs e)
    {
        // 全部 Form の中で混ざっている
        if (string.IsNullOrEmpty(txtName.Text))
        {
            MessageBox.Show("Name required!");   // UI と業務ロジックが密結合
            return;
        }

        var user = new User { Name = txtName.Text };
        _repository.Save(user);                  // データアクセスも混在
        MessageBox.Show("Saved!");
        Close();
    }
}
```

**問題点:**
- 単体テスト不可 (Form のインスタンスが必要)
- 業務ロジックを再利用できない
- `MessageBox` への密結合
- リポジトリをモックできない

### ✅ MVP

```csharp
// View インターフェイス (UI 型なし)
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
}

// Presenter (WinForms 知識ゼロ・テスト可能)
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IUserRepository _repository;

    public UserEditorPresenter(IUserRepository repository)
    {
        _repository = repository;
    }

    protected override void RegisterViewActions()
        => Dispatcher.Register(StandardActions.Save, OnSave);

    private void OnSave()
    {
        if (string.IsNullOrEmpty(View.UserName))
        {
            Messages.ShowError("Name required!");
            return;
        }

        _repository.Save(new User { Name = View.UserName, Email = View.Email });
        Messages.ShowInfo("Saved!");
    }
}
```

**得られるもの:**
- ✅ 完全に単体テスト可能
- ✅ 業務ロジックを View と独立して再利用できる
- ✅ サービスをモックして検証可能
- ✅ WinForms から独立した Presenter

---

## このパターンが解決するもの

| 問題 | MVP による解決 |
|------|-------------|
| 🧪 **テスタビリティ** | Presenter は WinForms 不要で単体テスト可能。View はインターフェイス越しにモックする |
| 🔧 **保守性** | 関心の分離により、影響範囲が局所化される |
| ♻️ **再利用性** | Presenter ロジックを異なる View で再利用可能 |
| 🎨 **柔軟性** | UI を変えても業務ロジックは無傷 |
| 👥 **協業** | デザイナーは View、開発者は Presenter で並行作業可能 |

---

## このフレームワークでの実装方針

Supervising Controller MVP の実現のため、本フレームワークは以下を提供します。
詳細は各リンク先を参照してください。

| 仕組み | 役割 | 詳細 |
|--------|------|----|
| `WindowPresenterBase<TView>` 系 | Presenter の基底クラス (4 種類) | [Presenter 基底クラス](Reference-Presenter-Base-Classes) |
| ViewAction システム | UI イベントを宣言的アクションに変換 | [ViewAction システム](Reference-ViewAction-System) |
| `IMessageService` / `IDialogProvider` / `IToastService` | UI 操作のサービス抽象 | [Platform Services](Reference-Platform-Services) |
| `WindowNavigator` | ウィンドウのライフサイクル管理 | [WindowNavigator](Reference-WindowNavigator) |
| `RequestClose` / `CanClose` override | ウィンドウクローズモデル (X も `RequestClose` も `CanClose` ゲートを通る) | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |

---

## 次のステップ

| 目的 | 読むべきページ |
|------|------------|
| フレームワーク全体の仕組みを知りたい | [アーキテクチャ概観](Concept-Architecture-Overview) |
| ウィンドウクローズの設計を理解したい | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |
| 17 条の設計ルールをまとめて読みたい | [MVP 設計ルール](Design-Rules) |
| 実際にコードを書き始めたい | [はじめに (Getting Started)](Getting-Started) |
