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
    event EventHandler<WindowClosingEventArgs> Closing;  // 自社型
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
    _binder.Add(CommonActions.Save, btnSave);
}
```

**Why**: 依存方向の一方向性 ([鉄則 3](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules)) を保つ。View は誰がそのイベントを聞いているか知らなくてよい。

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

ただし `IRequestClose<TResult>.CloseRequested` イベントは例外 (これは外部への通知であり、View への戻り値ではない)。詳しくは [ウィンドウクローズモデル](Concept-Window-Closing-Model)。

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

// ✅ Good — 共有 Service または子 Presenter 経由
public class ParentPresenter
{
    private ChildPresenter _childPresenter;

    private void OnUpdate()
    {
        _childPresenter.UpdateStatus("Updated");  // 子 Presenter を呼ぶ
        // または: _sharedService.SetStatus("Updated");
    }
}
```

**Why**: 各 View の状態は対応する Presenter が排他的に管理する。他の Presenter が直接触ると整合性管理が困難。
詳細は [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters)。

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
    bool HasUnsavedChanges { get; }
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

> **Presenter の公開メンバーは、コンストラクタとインターフェイス契約だけに限定する。ハンドラやヘルパーは `private`。**

| メソッドの種類 | 可視性 |
|--------------|------|
| コンストラクタ | `public` (DI が要求) |
| インターフェイス契約 (例: `IRequestClose<T>.CloseRequested`) | `public` (契約が要求) |
| ライフサイクルフック (`OnInitialize`、`RegisterViewActions`、`Cleanup`) | `protected override` |
| ViewAction ハンドラ (`OnSave`、`OnCancel`、...) | `private` |
| View イベントハンドラ (`OnViewClosing`、...) | `private` |
| ヘルパー (`RaiseClose`、検証、フォーマッタ) | `private` |

```csharp
// ❌ Bad — Presenter を "サービス" にしている
public class BadPresenter : WindowPresenterBase<IMyView>
{
    public void Save() { ... }                       // Dispatcher / CanExecute を迂回
    public bool CanSave => _changeTracker.IsChanged; // Tell-Don't-Ask 違反
}

// ✅ Good — コンストラクタ + 契約イベントだけ
public class GoodPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;

    public GoodPresenter(IMyRepository repo) { ... }

    protected override void RegisterViewActions() { ... }
    private void OnSave() { ... }
    private void RaiseClose(MyResult r, InteractionStatus s) => ...;
}
```

**Why**: 公開メンバーが増えるほど契約が広がり、フレームワークの「View → Action → Dispatcher → Handler」経路を迂回する誘惑が生まれる。

---

## Rule 17: Presenter イベントの可視性

> **Presenter が公開するイベントは `IRequestClose<TResult>.CloseRequested` を除いてゼロにする。**

他の通知ニーズには別の手段を使う:

| 通知したいこと | 使う手段 |
|------------|------|
| ウィンドウの結果 | `IRequestClose<T>.CloseRequested` を実装 |
| 親子間の連携 | 親が子のメソッドを直接呼ぶ |
| 共有ステート | Service に持たせて、Service のイベントを発行 |
| モジュール横断 | `IEventAggregator.Publish` |

```csharp
// ❌ Bad — Presenter が状態通知用のイベントを露出
public class BadPresenter : WindowPresenterBase<IMyView>
{
    public event EventHandler IsDirtyChanged;        // 内部状態の露出
    public event EventHandler SelectionChanged;      // View イベントの再発行
}

// ✅ Good — 契約イベントのみ
public class GoodPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;
}
```

**Why**: Presenter は単発の業務処理を担うコンポーネントで、ステート観測対象ではない。観測されたいステートは Service に置く。

詳細は [Presenter 基底クラス](Reference-Presenter-Base-Classes#公開-api-は最小限に保つ) と [HowTo: Presenter 間の通信方法](HowTo-Communicate-Between-Presenters)。

---

## 自動検証 (Roslyn Analyzer)

将来的にこれらルールの一部は Roslyn Analyzer で自動検証される予定です。
現時点では code review でのチェックリストとして使ってください。

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
- [ ] メソッドは `void` (`IRequestClose` 等の例外を除く) (Rule 7)
- [ ] View には `IXxxView` 越しにアクセス (Rule 8)
- [ ] 公開メンバーはコンストラクタと契約イベントだけ (Rule 16・17)

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
