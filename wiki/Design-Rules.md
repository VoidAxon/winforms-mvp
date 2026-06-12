# MVP 設計ルール

このページは、本フレームワークの **MVP コードが満たすべき 17 のルール** を一覧します。
ルールは [Design Rules for Model-View-Presenter](https://kjellsj.blogspot.com/2008/05/design-rules-for-model-view-presenter.html) (Kjell-Sverre Jerijærvi) を本フレームワーク向けに翻案・拡張したものです。

> **使い方**: コードレビューのチェックリストとして使うのが想定です。違反は単なる「美的問題」ではなく、テスタビリティ・保守性に直結する設計上の根拠があります。

---

## 目次

| カテゴリ | ルール |
|---------|------|
| **命名規約** | [Rule 1](#rule-1-view-命名規約) · [Rule 2](#rule-2-presenter-命名規約) · [Rule 5](#rule-5-イベントハンドラの-onxxx-命名) · [Rule 11](#rule-11-長く意味のある名前) · [Rule 15](#rule-15-ドメイン駆動の命名) |
| **責務分離** | [Rule 3](#rule-3-責務分離) · [Rule 4](#rule-4-view-インターフェイスと-presenter-に-ui-型を入れない) · [Rule 13](#rule-13-すべてのデータは-model-に) |
| **インターフェイス設計** | [Rule 6](#rule-6-view--presenter-の呼び出しを最小化) · [Rule 7](#rule-7-presenter-メソッドの戻り値を持たせない) · [Rule 12](#rule-12-プロパティよりメソッドを優先する) · [Rule 14](#rule-14-インターフェイスに-ui-コントロール名を入れない) |
| **依存関係** | [Rule 8](#rule-8-view-にはインターフェイス越しにのみアクセス) · [Rule 9](#rule-9-view-メソッドの可視性) · [Rule 10](#rule-10-view-にアクセスするのは-presenter-だけ) |
| **Presenter の公開 API** | [Rule 16](#rule-16-presenter-メソッドの可視性) · [Rule 17](#rule-17-presenter-イベントの可視性) |

---

## Rule 1: View 命名規約

> **View インターフェイスは `IXxxView`、View 実装は `XxxForm` (Form) または `XxxControl` (UserControl) と命名する。**

```csharp
// ✅ Good
public interface IUserEditorView : IWindowView { ... }
public class UserEditorForm : Form, IUserEditorView { ... }

public interface ISearchPanelView : IViewBase { ... }
public class SearchPanelControl : UserControl, ISearchPanelView { ... }

// ❌ Bad
public interface IUserEditor { ... }              // View であることが不明
public class UserEditor : Form, IUserEditor { ... } // Form 接尾辞がない
```

**Why**: 命名の一貫性がフレームワークの自動スキャン (`RegisterFromAssembly`) と相性がよく、コードジャンプも直感的になる。

---

## Rule 2: Presenter 命名規約

> **Presenter は `XxxPresenter` と命名する。対応する View と同じ接頭辞を使う。**

```csharp
// ✅ Good — View と Presenter の対応が明確
IUserEditorView   ⟷ UserEditorPresenter
ISearchPanelView  ⟷ SearchPanelPresenter

// ❌ Bad
IUserEditorView ⟷ UserController         // Controller でなく Presenter
IUserEditorView ⟷ UserEditorService      // Service ではない
```

**Why**: 命名で対応関係が明確になり、F12 ナビと結果のフィルタリングが楽になる。

---

## Rule 3: 責務分離

> **Presenter はユースケースロジックのみを扱う。UI の見せ方は View が担う。**

```csharp
// ❌ Bad — Presenter が UI の詳細を指示
private void OnSave()
{
    SaveData();
    View.SaveButton.BackColor = Color.Green;  // ← UI 詳細を指示
    View.StatusLabel.Visible = true;
    View.StatusLabel.Text = "Saved!";
}

// ✅ Good — Presenter は「何を伝えるか」だけ、見せ方は View が決める
private void OnSave()
{
    SaveData();
    View.ShowSuccess("Saved successfully!");   // ← 抽象的なメソッド
}
```

**Why**: View の見せ方 (色・レイアウト・コントロール種別) は変わるが、業務処理 (検証 → 保存 → 通知) は変わらない。後者だけ Presenter に置く。

---

## Rule 4: View インターフェイスと Presenter に UI 型を入れない

> **`System.Windows.Forms` 名前空間の型をどちらにも露出させない。`MessageBox` も同じ。**

これは [3 つの鉄則](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) の鉄則 1 と鉄則 2 を統合したもの。

```csharp
// ❌ Bad — View インターフェイスに UI 型
public interface IMyView : IWindowView
{
    Button SaveButton { get; }
    DialogResult ShowConfirm();
    event EventHandler<FormClosingEventArgs> Closing;
}

// ❌ Bad — Presenter が UI API を直接呼ぶ
private void OnSave()
{
    SaveData();
    MessageBox.Show("Saved!");
}

// ✅ Good
public interface IMyView : IWindowView
{
    string UserName { get; set; }
    ViewActionBinder ActionBinder { get; }
    // IWindowView には WinForms 型は含まれない。クローズ制御は Presenter の CanClose override で行う。
}

// Presenter
private void OnSave()
{
    SaveData();
    Messages.ShowInfo("Saved!");   // ← IMessageService 経由
}
```

**禁止される型の例**: `Form` / `Control` / `Button` / `TextBox` / `DialogResult` / `FormBorderStyle` / `MessageBox` / `MessageBoxButtons` / `MessageBoxIcon` / `FormClosingEventArgs` / `Type` (Form 型を表す用途で)。

**Why**: テスト時に WinForms 環境が不要になる。View 差し替え (他 UI フレームワークへの移植) も理論上可能になる。

---

## Rule 5: イベントハンドラの `OnXxx` 命名

> **View イベントを受ける Presenter 側のメソッドは `OnXxx` プレフィックスを使う。**

```csharp
// ✅ Good
protected override void OnViewAttached()
{
    View.SelectionChanged += OnSelectionChanged;
    View.DataChanged      += OnDataChanged;
}

private void OnSelectionChanged(object sender, EventArgs e) { ... }
private void OnDataChanged(object sender, EventArgs e) { ... }

// ❌ Bad — 何のメソッドか不明
private void HandleSelection(...) { ... }
private void ProcessData(...) { ... }
```

**Why**: イベントハンドラと業務メソッドが視覚的に区別できる。コードの意図が読みやすい。

---

## Rule 6: View → Presenter の呼び出しを最小化

> **View は Presenter のメソッドを呼ばない。イベントまたは ViewAction で通信する。**

```csharp
// ❌ Bad — View が Presenter を呼ぶ
public class MyForm : Form, IMyView
{
    private MyPresenter _presenter;

    private void btnSave_Click(object sender, EventArgs e)
    {
        _presenter.Save();              // ← 鉄則 3 違反
    }
}

// ✅ Good — View はイベント / ViewAction だけ
public class MyForm : Form, IMyView
{
    public event EventHandler SaveRequested;

    private void btnSave_Click(object sender, EventArgs e)
        => SaveRequested?.Invoke(this, EventArgs.Empty);
}

// または ViewAction
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(StandardActions.Save, btnSave);
}
```

**Why**: **依存の反転** ([鉄則 3](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を保つ。ここで一方向なのは「通信の流れ」ではなく「型依存」です — 上の Good 例でも `SaveRequested` イベントは View → Presenter へ流れますが、View は Presenter の具象型を知らず、誰がそのイベントを聞いているかも知りません。View が `presenter.Save()` を直接呼ぶと、この型依存の反転が崩れます。

---

## Rule 7: Presenter メソッドの戻り値を持たせない

> **Presenter のメソッドは `void` を返す。データは `View.Xxx = ...` のように View プロパティに書き込む。**

```csharp
// ❌ Bad — Presenter が値を返す
public List<User> LoadUsers()
{
    return _repository.GetAll();
}

// ✅ Good — Presenter は View を更新するだけ
private void OnRefresh()
{
    View.Users = _repository.GetAll();
}
```

**Why**: 「Tell, Don't Ask」原則。Presenter が能動的に View を更新するモデルに統一すると、View からの戻り値経路 (Ask) を必要としない。

ただし基底 `RequestClose(result, status)` によるクローズ要求は例外 (フレームワークが `InteractionResult<TResult>` に変換して呼び出し元に返す。View への戻り値ではない)。詳しくは [ウィンドウクローズモデル](Concept-Window-Closing-Model)。

---

## Rule 8: View にはインターフェイス越しにのみアクセス

> **Presenter は具象 Form ではなく、`IXxxView` インターフェイスを通じてだけ画面を操作する。**

```csharp
// ❌ Bad
public class MyPresenter : WindowPresenterBase<MyForm>   // ← 具象クラス
{
    private void OnSave()
    {
        this.View.lblStatus.Text = "Saved";   // ← 内部コントロールに直接アクセス
    }
}

// ✅ Good
public class MyPresenter : WindowPresenterBase<IMyView>  // ← インターフェイス
{
    private void OnSave()
    {
        View.Status = "Saved";   // ← インターフェイスのプロパティ越し
    }
}
```

**Why**: 単体テストでは Mock View を注入したい。具象クラスに依存していると Mock 化が困難。

---

## Rule 9: View メソッドの可視性

> **View インターフェイスのメソッドは `public`、Form 内部の UI コントロールは `private`。**

```csharp
public class MyForm : Form, IMyView
{
    private TextBox _nameTextBox;     // ← private
    private Button _saveButton;       // ← private

    public string UserName            // ← public (インターフェイス実装)
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }

    public ViewActionBinder ActionBinder { get; private set; }
}
```

**Why**: 内部コントロールが `public` だと、Form の外から `myForm.SaveButton.PerformClick()` のような操作が可能になり、MVP の責務境界が崩れる。

---

## Rule 10: View にアクセスするのは Presenter だけ

> **アプリ内で View を操作するのは Presenter のみ。他の Presenter から子 View の参照を持たない。**

```csharp
// ❌ Bad — 親 Presenter が子 View に直接アクセス
public class ParentPresenter
{
    private ChildView _childView;

    private void OnUpdate()
    {
        _childView.Status = "Updated";  // ← Presenter を飛ばして View に書き込む
    }
}

// ✅ Good — 子 View には触れず、子 Presenter にコマンドを送る。
// 性質で選ぶ: 一回的コマンド → 直接、共有・観測される状態 → 共有 Store/Model。
public class ChildPresenter : ControlPresenterBase<IChildView>
{
    // 既定形: internal メソッド。同一アセンブリの親からのみ呼べ、公開面を広げない。
    internal void UpdateStatus(string status) => View.Status = status;
    // 他の handler / helper は private のまま
}

public class ParentPresenter
{
    private readonly ChildPresenter _child;   // 親が具象の子をコンポジションで所有

    private void OnUpdate()
    {
        _child.UpdateStatus("Updated");   // 子 View には触れず、子 Presenter に命令
        // 共有・観測される状態なら、命令ではなく: _statusStore.Set("Updated");
    }
}

// 差し替え/テストの「縫い目」が要るときだけ、コマンドインターフェイスへ引き上げる:
//   public interface IChildCommands { void UpdateStatus(string status); }
//   class ChildPresenter : ..., IChildCommands { public void UpdateStatus(...) {...} }
//   class ParentPresenter { ParentPresenter(IChildCommands child) {...} }   // mock 可能
```

**Why**: 各 View の状態は対応する Presenter が排他的に管理する。他の Presenter が子 View を直接触ると整合性管理が困難。

**まず共有 Model / イベントを検討し、直接命令は本物の一回的コマンドに限る窄い例外**として扱います。その直接命令を出す形式は **`internal` メソッドを軽い既定** とします — 同一アセンブリ・親が具象の子を所有する通常ケースでは、これで十分かつ公開面を広げません。差し替えやテストで親を mock したい「縫い目」が要るときだけ **コマンドインターフェイス** へ引き上げます (`IChildCommands` を mock すれば親を単体テストできる)。**接口は必須ではなく、その縫い目が要るときの選択肢**です。いずれの形式でも子の handler / helper は `private` のまま ([Rule 16](#rule-16-presenter-メソッドの可視性))。なお『その操作に第三の Presenter が関心を持つ』『子の状態を問い合わせたい』なら、それはもう一回的コマンドではなく**共有・観測される状態**です — 直接命令ではなく共有 Model / Store に載せます (**性質で選ぶ: 状態 → Store、一回的コマンド → 直接**)。

> **方向の非対称性**: 「親 → 子」はコマンド (既定 `internal` メソッド) でよいが、「子 → 親」は直接呼び出し禁止 — 所有関係が逆転し循環するため。子から親への通知はイベントか `IEventAggregator` を使う。**下りはコマンド、上りはイベント**。直接命令か EventAggregator かの選び分け (所有関係の有無で決める) は [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) を参照。

---

## Rule 11: 長く意味のある名前

> **省略形を避け、何を表しているか名前から分かるようにする。**

```csharp
// ❌ Bad
public interface IUsrEdtVw : IWindowView { string Usr { get; } }

// ✅ Good
public interface IUserEditorView : IWindowView { string UserName { get; } }
```

**Why**: 5 文字短くする代わりに 1 か月後の自分が読めなくなるのは割に合わない。

---

## Rule 12: プロパティよりメソッドを優先する

> **View インターフェイスでは「単に値を取得/設定する」ものはプロパティ、「処理を起こす」ものはメソッドにする。**

```csharp
public interface IMyView : IWindowView
{
    // データ — プロパティ
    string UserName { get; set; }
    IReadOnlyList<User> Users { get; set; }

    // 処理を起こすもの — メソッド
    void ShowValidationErrors(IReadOnlyList<string> errors);
    void ClearFieldErrors();
    void HighlightField(string fieldName);
}
```

**Why**: プロパティはコスト不可視 (アクセスしただけで重い処理が走らないはず) という暗黙の前提がある。重い処理はメソッドの形で明示。

---

## Rule 13: すべてのデータは Model に

> **View が表示しているデータは、内部に保持するのではなく Model (Presenter が管理する状態) に保持し、View はそのコピーを表示する。**

```csharp
// ❌ Bad — UI コントロールが Single Source of Truth になっている
private void OnSave()
{
    var name = _nameTextBox.Text;        // ← データが UI から読まれる
    var email = _emailTextBox.Text;
    _repository.Save(new User { Name = name, Email = email });
}

// ✅ Good — Model (UserModel) が Single Source of Truth
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private UserModel _user;

    protected override void OnInitialize()
    {
        _user = _repository.GetById(_userId);
        View.Bind(_user);
    }

    private void OnSave()
    {
        _user.Name  = View.UserName;
        _user.Email = View.Email;
        _repository.Save(_user);
    }
}
```

**Why**: UI コントロールに状態を持たせると、複数の表示や検証ルールでの整合性管理が困難。Model に置けば一元化できる。

---

## Rule 14: インターフェイスに UI コントロール名を入れない

> **View インターフェイスのプロパティ名は、内部で使うコントロール名と独立させる。**

```csharp
// ❌ Bad — TextBox の名前がインターフェイスに漏れている
public interface IUserEditorView : IWindowView
{
    string NameTextBox { get; set; }       // ← TextBox 接尾辞
    string EmailTextBox { get; set; }
}

// ✅ Good — 業務概念で命名
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
}
```

**Why**: コントロール種別はあとから変わる (TextBox → ComboBox 等)。インターフェイスは変えたくない。

---

## Rule 15: ドメイン駆動の命名

> **メソッド名は技術的な操作ではなく、業務的な意図を表す。**

```csharp
// ❌ Bad — 技術的な命名
View.SetText("Save successful");
View.UpdateGrid(orders);

// ✅ Good — 業務的な命名
View.ShowSuccessMessage("Save successful");
View.DisplayOrders(orders);
```

**Why**: 業務的な命名は読み手の理解を早める。技術的な命名 (`SetText`、`UpdateGrid`) はコード詳細を露出させる。

---

## Rule 16: Presenter メソッドの可視性

> **Presenter を、外部から命令的に突けるサービスにしない。公開面の既定はコンストラクタ・`Initialize`・`Dispose` のみ。ViewAction の handler / helper は必ず `private`。**

「公開メンバーの白名単」ではなく **1 つの不変量** (*Presenter のユースケース行動は正当な通路 = ViewAction + ライフサイクル (構築 / `Initialize` / `Dispose`) からのみ駆動される*) で、**入站と出站で非対称に**適用します:

- **入站 = 命令面 → 厳しく圧縮する。** handler / helper は必ず `private` (非協商)。`public Save()` は Dispatcher の `CanExecute`・ミドルウェアを迂回します。
- **出站 = 通知面 → 本ルールの対象外** ([Rule 17](#rule-17-presenter-イベントの可視性) が担う)。クローズ結果は基底 `RequestClose(result, status)` を呼ぶだけ — インターフェイス実装も `public` なイベント宣言も不要。
- **親→子の狭いコマンドは許容される例外。** 露出形式は `internal` 既定、縫い目が要るときだけコマンドインターフェイス — **接口は必須ではありません** ([Rule 10](#rule-10-view-にアクセスするのは-presenter-だけ))。

> 📍 **本文・可視性テーブル・NG/OK 例の正準は [Reference: Presenter 基底クラス](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ)** にあります。Design-Rules 側はこのルール文のみを保持します。

**Why**: 入站の公開面が増えるほど契約が広がり、フレームワークの「View → Action → Dispatcher → Handler」経路を迂回する誘惑が生まれる。

---

## Rule 17: Presenter イベントの可視性

> **Presenter のイベントは「上向きの出力ポート / 単発の結果通知」に限る。共有・観測可能なステートを Presenter のイベントで通知しない (それは Model / Service の責務)。**

これは [Rule 16](#rule-16-presenter-メソッドの可視性) の不変量の**出站側の系**です。ウィンドウのクローズ結果は基底 `RequestClose(result, status)` を呼ぶことで返します — インターフェイス実装も `public` なイベント宣言も不要で、フレームワークが `InteractionResult<TResult>` に変換して呼び出し元へ返します。禁じるのは Presenter を「観測されるステート保持者」にすること — `IsDirtyChanged` のような状態イベントを生やすと、本来 Model / Service が持つべき観測点が Presenter に漏れます。

通知の性質ごとに手段を選びます:

| 通知したいこと | 使う手段 |
|------------|------|
| ウィンドウの結果 (上向き・単発) | 基底 `RequestClose(result, status)` を呼ぶ (インターフェイス実装不要。フレームワークが `InteractionResult<TResult>` に変換して呼び出し元へ返す) |
| 親子間の連携 (親 → 子) | 親が子 Presenter にコマンドを送る (既定 `internal` メソッド、縫い目が要るときコマンドインターフェイス。[Rule 10](#rule-10-view-にアクセスするのは-presenter-だけ) 参照) |
| 共有・観測されるステート | Service / Store に持たせて、その変更通知イベントを発行 |
| モジュール横断 | `IEventAggregator.Publish` |

```csharp
// ❌ Bad — Presenter が状態通知用のイベントを露出
public class BadPresenter : WindowPresenterBase<IMyView>
{
    public event EventHandler IsDirtyChanged;        // 内部状態の露出
    public event EventHandler SelectionChanged;      // View イベントの再発行
}

// ✅ Good — 公開イベントはなし。結果はフレームワーク経由で返る。
public class GoodPresenter : WindowPresenterBase<IMyView>
{
    private void OnSave() => RequestClose(BuildResult(), InteractionStatus.Ok);
}
```

**Why**: Presenter は単発の業務処理を担うコンポーネントで、ステート観測対象ではない。観測されたいステートは Service に置く。

詳細は [Presenter 基底クラス](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ) と [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters)。

---

## 自動検証 (Roslyn Analyzer)

これらのルールの **一部は、すでに Roslyn Analyzer によってコンパイル時に自動検証されています**。
アナライザは `WinformsMVP` パッケージに同梱されており (`analyzers/dotnet/cs`)、メインパッケージを参照するだけで消費側のビルドでも自動的に走ります — 別途インストールは不要です。

| 診断 ID | タイトル | 対応ルール |
|---------|---------|----------|
| `MVP001` | View インターフェイスは `View` で終える | [Rule 1](#rule-1-view-命名規約) |
| `MVP002` | Presenter クラスは `Presenter` で終える | [Rule 2](#rule-2-presenter-命名規約) |
| `MVP003` | Presenter で UI コントロールを生成しない | [Rule 3](#rule-3-責務分離) |
| `MVP004` | View / Presenter に UI 型を露出させない | [Rule 4](#rule-4-view-インターフェイスと-presenter-に-ui-型を入れない) |
| `MVP006` | 公開 Presenter メソッドは `void` を返す | [Rule 7](#rule-7-presenter-メソッドの戻り値を持たせない) |
| `MVP007` | View は具象 Form ではなくインターフェイス越しに扱う | [Rule 8](#rule-8-view-にはインターフェイス越しにのみアクセス) |
| `MVP008` | 公開 View メソッドはインターフェイスに定義する | [Rule 9](#rule-9-view-メソッドの可視性) |
| `MVP013` | インターフェイスメソッド名に UI コントロール型を入れない | [Rule 14](#rule-14-インターフェイスに-ui-コントロール名を入れない) |

> 既定の重大度はすべて **Warning** です (フレームワークをインストールしただけでビルドが壊れないように)。チームで厳格化したい場合は `.editorconfig` で個別に `error` へ引き上げられます。

それ以外のルール (Rule 5・6・10〜12・15〜17) はアナライザ化されていないため、引き続き **コードレビューのチェックリスト** として使ってください。

---

## クイックリファレンスチェックリスト

コードレビュー時に使う一覧。

### View インターフェイス
- [ ] `IXxxView` という命名 (Rule 1)
- [ ] WinForms 型 (`Button`、`DialogResult` 等) を持たない (Rule 4)
- [ ] UI コントロール名がプロパティ名に混入していない (Rule 14)
- [ ] プロパティは「データ」、メソッドは「処理」(Rule 12)
- [ ] `ViewActionBinder ActionBinder { get; }` を公開している

### Form (View 実装)
- [ ] `XxxForm` または `XxxControl` という命名 (Rule 1)
- [ ] 内部コントロールは `private` (Rule 9)
- [ ] Presenter の業務メソッドを呼ばない (Rule 6) ※ 参照保持・AttachView/Initialize/Dispose 等のライフサイクル呼び出しは可
- [ ] `InitializeActionBindings()` は UI バインドだけ (業務ロジック禁止)

### Presenter
- [ ] `XxxPresenter` という命名 (Rule 2)
- [ ] 業務ロジックのみで UI の見せ方を指示しない (Rule 3)
- [ ] WinForms 型を扱わない (Rule 4)
- [ ] イベントハンドラは `OnXxx` 命名 (Rule 5)
- [ ] メソッドは `void` (Rule 7)
- [ ] View には `IXxxView` 越しにアクセス (Rule 8)
- [ ] 公開メンバーはコンストラクタのみ (Rule 16・17)

### モデル / データ
- [ ] 状態は Presenter の Model フィールドに、UI ではなく Model が Single Source of Truth (Rule 13)
- [ ] メソッド名は業務語彙、技術用語ではない (Rule 15)
- [ ] 命名は省略しない (Rule 11)

---

## 関連ページ

- [MVP パターンとは](Concept-MVP-Pattern) — 鉄則 3 つの概要
- [Presenter の責務と肥大化の防止](Concept-Presenter-Responsibilities) — Rule 3・13・16・17 を貫く「責務の線引き」の指針
- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — Rule 16・17 の詳細
- [ViewAction システム](Reference-ViewAction-System) — Rule 6 の実装手段
- [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — Rule 10・17 の実装方針
- [Platform Services](Reference-Platform-Services) — Rule 4 を満たすサービス層
